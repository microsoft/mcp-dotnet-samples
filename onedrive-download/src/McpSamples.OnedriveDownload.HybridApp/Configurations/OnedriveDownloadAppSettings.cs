namespace McpSamples.OnedriveDownload.HybridApp.Configurations
{
    public class OnedriveDownloadAppSettings
    {
        public EntraIdSettings EntraId { get; set; } = new();
    }

    public class EntraIdSettings
    {
        public string? Instance { get; set; }
        public string? TenantId { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? Scopes { get; set; }
        public string? CallbackPath { get; set; }
    }
}
