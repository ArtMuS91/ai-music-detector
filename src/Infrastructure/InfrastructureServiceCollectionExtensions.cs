using Core.Repositories;
using Core.Services;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AnalysisDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres"))
                .UseSnakeCaseNamingConvention());

        services.Configure<YtDlpOptions>(configuration.GetSection(YtDlpOptions.SectionName));
        services.Configure<FfmpegOptions>(configuration.GetSection(FfmpegOptions.SectionName));
        services.Configure<GroqOptions>(configuration.GetSection(GroqOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAnalysisJobRepository, EfAnalysisJobRepository>();
        services.AddSingleton<IAudioAcquisitionService, YtDlpAudioAcquisitionService>();
        services.AddSingleton<IAudioPreprocessor, FfmpegAudioPreprocessor>();

        services.AddHttpClient<GroqWebResearchSignalProvider>((provider, client) =>
        {
            var groq = provider.GetRequiredService<IOptions<GroqOptions>>().Value;
            client.BaseAddress = groq.BaseUrl;
            client.Timeout = groq.Timeout;
        });
        services.AddTransient<IDetectionSignalProvider>(provider =>
            provider.GetRequiredService<GroqWebResearchSignalProvider>());

        AddMlDetectors(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers one provider per detector id configured under <c>MlService:Detectors</c>, all
    /// sharing a named client for the Python service.
    /// </summary>
    private static void AddMlDetectors(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MlServiceOptions>(configuration.GetSection(MlServiceOptions.SectionName));
        services.AddHttpClient(MlDetectionSignalProvider.HttpClientName, (provider, client) =>
        {
            var ml = provider.GetRequiredService<IOptions<MlServiceOptions>>().Value;
            client.BaseAddress = ml.BaseUrl;
            client.Timeout = ml.Timeout;
        });

        var detectors = configuration.GetSection(MlServiceOptions.SectionName).Get<MlServiceOptions>()?.Detectors ?? [];
        foreach (var detectorId in detectors.Distinct())
        {
            services.AddTransient<IDetectionSignalProvider>(provider => new MlDetectionSignalProvider(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(MlDetectionSignalProvider.HttpClientName),
                detectorId,
                provider.GetRequiredService<ILogger<MlDetectionSignalProvider>>()));
        }
    }
}
