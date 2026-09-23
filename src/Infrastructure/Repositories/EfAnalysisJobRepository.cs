using Core.Entities;
using Core.Models;
using Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class EfAnalysisJobRepository(AnalysisDbContext db, TimeProvider clock) : IAnalysisJobRepository
{
    public async Task<AnalysisJobEntity> CreateAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var job = new AnalysisJobEntity
        {
            Id = Guid.CreateVersion7(),
            SourceUrl = request.SourceUrl,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public Task<AnalysisJobEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task UpdateAsync(AnalysisJobEntity job, CancellationToken cancellationToken = default)
    {
        job.UpdatedAt = clock.GetUtcNow();
        db.Jobs.Update(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnalysisJobEntity?> ClaimNextPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // SKIP LOCKED lets several workers poll the same queue without handing
        // the same job to two of them or blocking on each other's rows.
        var job = await db.Jobs
            .FromSql($"""
                SELECT * FROM analysis_jobs
                WHERE status = {nameof(AnalysisStatus.Pending)}
                ORDER BY created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return null;
        }

        job.Status = AnalysisStatus.Acquiring;
        job.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return job;
    }

    public async Task<IReadOnlyList<AnalysisJobEntity>> ListInProgressAsync(CancellationToken cancellationToken = default)
        => await db.Jobs
            .Where(x => x.Status == AnalysisStatus.Acquiring
                || x.Status == AnalysisStatus.Preprocessing
                || x.Status == AnalysisStatus.Analyzing)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
}
