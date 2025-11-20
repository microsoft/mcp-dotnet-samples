using Microsoft.AspNetCore.Mvc;

namespace McpSamples.OnedriveDownload.HybridApp.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly ILogger<McpController> _logger;

    public McpController(ILogger<McpController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> HandleMcpRequest()
    {
        try
        {
            string requestBody = "";

            if (Request.Method == "POST")
            {
                using (var reader = new StreamReader(Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
            }

            _logger.LogInformation("MCP Request received: {Method}", Request.Method);

            // MCP 요청 처리
            // 간단한 헬스 체크 응답 먼저
            var response = new
            {
                status = "ok",
                message = "MCP Server is running",
                timestamp = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
