using Analysis.Services;
using Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Analysis;

public static class AnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddAnalysis(this IServiceCollection services)
    {
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IAnalysisPipeline, AnalysisPipeline>();

        return services;
    }
}
