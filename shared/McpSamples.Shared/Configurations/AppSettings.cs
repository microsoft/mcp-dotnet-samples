using System.Collections;

using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;

namespace McpSamples.Shared.Configurations;

/// <summary>
/// This represents the base class for application settings.
/// </summary>
public abstract class AppSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to use HTTP for the MCP server or not.
    /// </summary>
    public bool UseHttp { get; set; }

    /// <summary>
    /// Gets or sets the OpenAPI configuration for the MCP server.
    /// </summary>
    public virtual OpenApiInfo OpenApi { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to display help information or not.
    /// </summary>
    public bool Help { get; set; }

    /// <summary>
    /// Parses the configuration settings and command line arguments for additional settings.
    /// </summary>
    /// <param name="config"><see cref="IConfiguration"/> instance</param>
    /// <param name="args">List of arguments passed from the command line.</param>
    /// <typeparam name="T">Type inheriting <see cref="AppSettings"/>.</typeparam>
    /// <returns>Returns an instance of <typeparamref name="T"/>.</returns>
    protected virtual T ParseMore<T>(IConfiguration config, string[] args) where T : AppSettings, new()
    {
        var settings = new T();
        config.Bind(settings);

        if (args.Length == 0)
        {
            return settings;
        }

        if (args.Length == 1)
        {
            settings.Help = true;
            return settings;
        }

        return settings;
    }

    /// <summary>
    /// Parses the configuration settings and command line arguments.
    /// </summary>
    /// <param name="config"><see cref="IConfiguration"/> instance</param>
    /// <param name="args">List of arguments passed from the command line.</param>
    /// <typeparam name="T">Type inheriting <see cref="AppSettings"/>.</typeparam>
    /// <returns>Returns an instance of <typeparamref name="T"/>.</returns>
    public static T Parse<T>(IConfiguration config, string[] args) where T : AppSettings, new()
    {
        var settings = new T();
        config.Bind(settings);

        settings = settings.ParseMore<T>(config, args);

        if (args.Length == 0)
        {
            return settings;
        }

        if (args.Length == 1)
        {
            settings.Help = true;
            return settings;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--http":
                    settings.UseHttp = true;
                    break;

                case "--help":
                default:
                    settings.Help = true;
                    break;
            }
        }

        return settings;
    }

    /// <summary>
    /// Checks whether to use streamable HTTP or not.
    /// </summary>
    /// <param name="env"><see cref="IDictionary"/> instance representing environment variables.</param>
    /// <param name="args">List of arguments passed from the command line.</param>
    /// <returns>Returns <c>True</c> if streamable HTTP is enabled; otherwise, <c>False</c>.</returns>
    public static bool UseStreamableHttp(IDictionary env, string[] args)
    {
        var useHttp = env.Contains("UseHttp") &&
                      bool.TryParse(env["UseHttp"]?.ToString()?.ToLowerInvariant(), out var result) && result;
        if (args.Length == 0)
        {
            return useHttp;
        }

        useHttp = args.Contains("--http", StringComparer.InvariantCultureIgnoreCase);

        return useHttp;
    }
}
