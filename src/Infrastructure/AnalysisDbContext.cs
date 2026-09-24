using System.Text.Json;
using Core.Entities;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure;

public sealed class AnalysisDbContext(DbContextOptions<AnalysisDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DbSet<AnalysisJobEntity> Jobs => Set<AnalysisJobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var job = modelBuilder.Entity<AnalysisJobEntity>();

        job.ToTable("analysis_jobs");
        job.HasKey(x => x.Id);
        job.Property(x => x.SourceUrl).HasMaxLength(2048);
        job.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        job.Property(x => x.FailureReason).HasMaxLength(AnalysisJobEntity.FailureReasonMaxLength);
        job.Property(x => x.AcquiredAudioPath).HasMaxLength(1024);
        job.Property(x => x.PreprocessedAudioPath).HasMaxLength(1024);

        // Track and Result are read and written whole, never queried by their
        // inner fields, so jsonb keeps the schema flat as the shape evolves.
        job.Property(x => x.Track).HasConversion(JsonConverterFor<Track>()).HasColumnType("jsonb");
        job.Property(x => x.Result).HasConversion(JsonConverterFor<AnalysisResult>()).HasColumnType("jsonb");

        job.HasIndex(x => new { x.Status, x.CreatedAt });
    }

    private static ValueConverter<T?, string?> JsonConverterFor<T>()
        where T : class
        => new(
            value => JsonSerializer.Serialize(value, JsonOptions),
            json => JsonSerializer.Deserialize<T>(json!, JsonOptions));
}
