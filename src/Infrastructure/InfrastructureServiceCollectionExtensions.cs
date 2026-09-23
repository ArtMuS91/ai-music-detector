using Core.Repositories;
using Core.Services;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        return services;
    }
}
