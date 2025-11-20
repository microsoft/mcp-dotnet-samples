// Test script to check authentication flow
using System.Diagnostics;
using System.Text.Json;

Console.WriteLine("=== Testing GetHomeTenantIdFromAzureCliAsync ===");

// Step 1: Run Azure CLI
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
Console.WriteLine($"Output:\n{output}");
if (!string.IsNullOrEmpty(errorOutput))
{
    Console.WriteLine($"Error Output:\n{errorOutput}");
}

// Step 2: Parse JSON
if (process.ExitCode == 0)
{
    using var doc = JsonDocument.Parse(output);
    var root = doc.RootElement;
    
    if (root.TryGetProperty("homeTenantId", out var homeTenantIdElement))
    {
        Console.WriteLine($"✓ homeTenantId found: {homeTenantIdElement.GetString()}");
    }
    else if (root.TryGetProperty("tenantId", out var tenantIdElement))
    {
        Console.WriteLine($"⚠ homeTenantId not found, using tenantId: {tenantIdElement.GetString()}");
    }
    else
    {
        Console.WriteLine("✗ No tenant ID found in response");
    }
}
else
{
    Console.WriteLine("✗ Azure CLI command failed");
}
