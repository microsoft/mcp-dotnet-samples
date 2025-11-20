using System.Diagnostics;
using System.Text.Json;

namespace McpSamples.OnedriveDownload.HybridApp;

public class DebugAuthTest
{
    public static async Task TestGetHomeTenantIdAsync()
    {
        Console.WriteLine("=== Testing GetHomeTenantIdFromAzureCliAsync ===");

        try
        {
            Console.WriteLine("Step 1: Running 'az account show --output json'");
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "az",
                    Arguments = "account show --output json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string errorOutput = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            Console.WriteLine($"Exit Code: {process.ExitCode}");
            
            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"✓ Azure CLI Output:\n{output}");
            }
            
            if (!string.IsNullOrEmpty(errorOutput))
            {
                Console.WriteLine($"✗ Azure CLI Error:\n{errorOutput}");
            }

            if (process.ExitCode != 0)
            {
                Console.WriteLine("✗ Azure CLI failed");
                return;
            }

            // Parse JSON
            Console.WriteLine("\nStep 2: Parsing JSON response");
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("homeTenantId", out var homeTenantIdElement))
            {
                var homeTenantId = homeTenantIdElement.GetString();
                Console.WriteLine($"✓ homeTenantId found: {homeTenantId}");
            }
            else if (root.TryGetProperty("tenantId", out var tenantIdElement))
            {
                var tenantId = tenantIdElement.GetString();
                Console.WriteLine($"⚠ homeTenantId not found, using tenantId: {tenantId}");
            }
            else
            {
                Console.WriteLine("✗ No tenant ID found in response");
                Console.WriteLine($"Available properties: {string.Join(", ", root.EnumerateObject().Select(p => p.Name))}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Exception: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
