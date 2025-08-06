using McpSamples.Shared.Configurations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpSamples.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppSettings<T>(this IServiceCollection services, IConfiguration config, string[] args) where T : AppSettings, new()
    {
        services.AddSingleton<T>(_ =>
        {
            var settings = AppSettings.Parse<T>(config, args);

            return settings;
        });

        return services;
    }
}
