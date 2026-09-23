using Core.Repositories;
using Core.Services;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AnalysisDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres"))
                .UseSnakeCaseNamingConvention());

        services.Configure<YtDlpOptions>(configuration.GetSection(YtDlpOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAnalysisJobRepository, EfAnalysisJobRepository>();
        services.AddSingleton<IAudioAcquisitionService, YtDlpAudioAcquisitionService>();

        return services;
    }
}
