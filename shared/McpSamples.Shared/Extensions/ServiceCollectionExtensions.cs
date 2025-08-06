using McpSamples.Shared.Configurations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McpSamples.Shared.Extensions;

/// <summary>
/// This represents the extension entity for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the application settings of the specified type to the service collection.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> instance.</param>
    /// <param name="config"><see cref="IConfiguration"/> instance.</param>
    /// <param name="args">List of arguments passed from the command line.</param>
    /// <typeparam name="T">Type inheriting <see cref="AppSettings"/>.</typeparam>
    /// <returns>Returns the <see cref="IServiceCollection"/> instance.</returns>
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
