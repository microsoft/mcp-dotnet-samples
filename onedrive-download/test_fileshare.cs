using Azure.Storage.Files.Shares;

// Connection String from function app
string connStr = "DefaultEndpointsProtocol=https;AccountName=st34ugypgdcsh76files;AccountKey=kJvxLikJiqJYcgQIsx8ym2jd7R8VU69hVL7EKMb6qBkV5mwlwS6+CdtOysV+Lr8MxP+LF6UrqUXa+AStf2Lz+Q==;EndpointSuffix=core.windows.net";

try
{
    // Test 1: Create ShareClient
    Console.WriteLine("Creating ShareClient...");
    var shareClient = new ShareClient(connStr, "downloads");
    Console.WriteLine("✓ ShareClient created successfully");
    
    // Test 2: Check if share exists
    Console.WriteLine("Checking if share exists...");
    var exists = await shareClient.ExistsAsync();
    Console.WriteLine($"✓ Share exists: {exists.Value}");
    
    // Test 3: Get root directory
    Console.WriteLine("Getting root directory...");
    var rootDir = shareClient.GetRootDirectoryClient();
    Console.WriteLine("✓ Root directory obtained");
    
    // Test 4: Create test file
    Console.WriteLine("Creating test file...");
    var testFile = rootDir.GetFileClient("test_" + DateTime.Now.Ticks + ".txt");
    await testFile.UploadAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content")));
    Console.WriteLine($"✓ Test file created: {testFile.Uri}");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}
