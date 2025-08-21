# AGENTS.md - MCP .NET Samples

## Project Overview

The Model Context Protocol (MCP) .NET Samples repository demonstrates how to build MCP servers using .NET 9.0. This repository contains four comprehensive sample implementations that showcase different aspects of MCP server development, from GitHub integration to web services, data management, and email communications.

## Project Structure and Architecture

### Repository Organization

```
mcp-dotnet-samples/
├── .github/                    # GitHub configuration and workflows
│   ├── workflows/             # CI/CD pipeline definitions
│   ├── copilot-instructions.md # GitHub Copilot coding standards
│   └── templates/             # Issue and PR templates
├── shared/                    # Common shared libraries
│   └── McpSamples.Shared/     # Shared MCP implementation utilities
├── awesome-copilot/           # GitHub Copilot integration sample
│   ├── src/McpSamples.AwesomeCopilot.HybridApp/
│   ├── infra/                 # Azure Bicep templates
│   └── .vscode/              # VS Code MCP configuration
├── markdown-to-html/          # Markdown conversion sample
│   ├── src/McpSamples.MarkdownToHtml.HybridApp/
│   ├── infra/                 # Azure Bicep templates
│   └── .vscode/              # VS Code MCP configuration
├── outlook-email/             # Email communication sample
│   ├── src/McpSamples.OutlookEmail.HybridApp/
│   ├── infra/                 # Azure Bicep templates
│   └── .vscode/              # VS Code MCP configuration
├── todo-list/                 # Todo management sample
│   ├── src/McpSamples.TodoList.HybridApp/
│   ├── infra/                 # Azure Bicep templates
│   └── .vscode/              # VS Code MCP configuration
├── Dockerfile.*              # Multi-stage Docker configurations
├── Directory.Build.props     # Global MSBuild properties
└── global.json              # .NET SDK configuration
```

### Sample Components

#### 1. Awesome Copilot Sample
- **Purpose**: Integrates with GitHub's awesome-copilot repository to provide Copilot customizations
- **Key Features**: 
  - Search and retrieve GitHub Copilot instructions
  - File metadata processing
  - JSON-based configuration management
- **Technologies**: File I/O, HTTP clients, JSON processing

#### 2. Markdown to HTML Sample  
- **Purpose**: Converts Markdown content to HTML with customizable options
- **Key Features**:
  - Markdown parsing and HTML generation
  - Configurable HTML output options
  - Azure integration for scalable processing
- **Technologies**: Markdown processing, HTML generation, web services

#### 3. Outlook Email Sample
- **Purpose**: Sends emails through Outlook with comprehensive authentication scenarios
- **Key Features**:
  - Email sending capabilities via Microsoft Graph API
  - OAuth authentication with Azure API Management
  - API key authentication with Azure Functions
  - No-authentication local development mode
  - Azure Functions and Container Apps deployment support
- **Technologies**: Microsoft Graph, OAuth, Azure Functions, API Management

#### 4. Todo List Sample
- **Purpose**: Manages todo items with full CRUD operations
- **Key Features**:
  - Create, read, update, delete operations
  - Data persistence
  - RESTful API patterns
- **Technologies**: Data management, API development, persistence

## Coding Conventions and Standards

### C# and .NET Standards

#### Language and Framework
- **Target Framework**: .NET 9.0 (as specified in Directory.Build.props)
- **Language Version**: Latest C# features enabled
- **Nullable Reference Types**: Enabled for all projects
- **Implicit Global Usings**: Enabled to reduce boilerplate

#### Code Organization
```csharp
// Namespace structure
namespace McpSamples.{SampleName}.{ComponentType};

// Class organization
public class ExampleMcpServer : IMcpServer
{
    // Private readonly fields first
    private readonly ILogger<ExampleMcpServer> _logger;
    
    // Constructor
    public ExampleMcpServer(ILogger<ExampleMcpServer> logger)
    {
        _logger = logger;
    }
    
    // Public methods
    [McpServerTool(Name = "example_tool")]
    [Description("Example tool implementation")]
    public async Task<string> ExampleToolAsync(
        [Description("Input parameter")] string input)
    {
        // Implementation
    }
}
```

#### Naming Conventions
- **Classes**: PascalCase with descriptive names
- **Methods**: PascalCase with verb-noun pattern
- **Properties**: PascalCase
- **Private Fields**: _camelCase with underscore prefix
- **Parameters**: camelCase
- **Constants**: UPPER_SNAKE_CASE

### MCP Server Development Standards

#### Server Registration
- Use dependency injection for all services
- Implement proper lifetime management
- Follow the MCP protocol specifications

#### Tool Implementation
```csharp
[McpServerTool(Name = "tool_name")]
[Description("Clear description of what the tool does")]
public async Task<ToolResult> ToolNameAsync(
    [Description("Parameter description")] string parameter)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(parameter))
        throw new ArgumentException("Parameter cannot be null or empty");
    
    // Implement functionality
    var result = await ProcessAsync(parameter);
    
    // Return structured result
    return new ToolResult { Content = result };
}
```

#### Prompt Implementation
```csharp
[McpServerPrompt(Name = "prompt_name", Title = "Human-readable title")]
[Description("Description of the prompt's purpose")]
public string GetPrompt([Description("Context parameter")] string context)
{
    return $"Structured prompt template with {context}";
}
```

### Azure and Infrastructure Standards

#### Bicep Template Organization
- Use consistent parameter naming and descriptions
- Implement proper resource tagging
- Follow Azure naming conventions with abbreviations file
- Include monitoring and security configurations

#### Container Configuration
- Multi-stage Docker builds for optimization
- Proper health check implementations
- Environment variable configuration
- Security scanning and minimal base images

## Testing Protocols and Guidelines

### Unit Testing Framework
- **Framework**: MSTest or xUnit (depending on sample requirements)
- **Mocking**: Moq for dependency mocking
- **Coverage**: Minimum 80% code coverage for business logic

### Test Structure
```csharp
[TestClass]
public class ExampleMcpServerTests
{
    private readonly Mock<ILogger<ExampleMcpServer>> _mockLogger;
    private readonly ExampleMcpServer _server;
    
    public ExampleMcpServerTests()
    {
        _mockLogger = new Mock<ILogger<ExampleMcpServer>>();
        _server = new ExampleMcpServer(_mockLogger.Object);
    }
    
    [TestMethod]
    public async Task ExampleTool_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = "test input";
        var expectedOutput = "expected result";
        
        // Act
        var result = await _server.ExampleToolAsync(input);
        
        // Assert
        Assert.AreEqual(expectedOutput, result.Content);
    }
}
```

### Integration Testing
- Test MCP server initialization and registration
- Verify tool and prompt functionality end-to-end
- Test error handling and edge cases
- Validate Azure deployment configurations

### Running Tests
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test ./tests/McpSamples.{Sample}.Tests/
```

## Pull Request Guidelines and Workflow

### Branch Naming Convention
- `feature/description-of-feature`
- `bugfix/description-of-fix`
- `docs/description-of-documentation-change`

### PR Requirements Checklist
- [ ] **Code Quality**: Follows established coding standards
- [ ] **Tests**: Includes appropriate unit and integration tests
- [ ] **Documentation**: Updates relevant documentation
- [ ] **Build**: Successfully builds with .NET 9.0
- [ ] **Azure**: Infrastructure changes are validated
- [ ] **Security**: No hardcoded secrets or vulnerabilities
- [ ] **Performance**: No significant performance regressions
- [ ] **MCP Compliance**: Follows MCP protocol specifications

### PR Description Template
```markdown
## Summary
Brief description of the changes made.

## Changes Made
- List specific changes
- Include any new features or modifications
- Mention any removed functionality

## Testing Performed
- Unit tests: [Pass/Fail]
- Integration tests: [Pass/Fail]
- Manual testing: [Description]
- Azure deployment: [Validated/Not applicable]

## Breaking Changes
List any breaking changes or migration requirements.

## Screenshots
Include screenshots for UI changes (if applicable).

## Additional Notes
Any additional context or considerations for reviewers.
```

### Commit Message Format
Follow conventional commit format:
```
type(scope): description

feat(awesome-copilot): add search functionality for instructions
fix(shared): resolve MCP server initialization issue  
docs(readme): update installation instructions
refactor(todo-list): optimize data access patterns
test(markdown): add unit tests for HTML conversion
```

## Development Environment Setup

### Prerequisites
- .NET 9.0 SDK
- Visual Studio Code with C# Dev Kit extension
- Docker Desktop for containerization
- Azure CLI for Azure operations
- Azure Developer CLI (azd) for deployment

### Local Development Workflow
1. **Clone Repository**: `git clone <repository-url>`
2. **Install Dependencies**: `dotnet restore`
3. **Build Solution**: `dotnet build`
4. **Run Tests**: `dotnet test`
5. **Configure MCP**: Copy appropriate `.vscode/mcp.json` configuration
6. **Start Development**: Use VS Code with MCP integration

### VS Code Configuration
Each sample includes pre-configured VS Code settings for MCP integration:
- `.vscode/mcp.stdio.local.json` - Local STDIO communication
- `.vscode/mcp.http.local.json` - Local HTTP communication
- `.vscode/mcp.stdio.container.json` - Container-based STDIO

The outlook-email sample includes additional configurations for:
- `.vscode/mcp.http.local-func.json` - Local Azure Functions
- `.vscode/mcp.http.remote-func.json` - Remote Azure Functions
- `.vscode/mcp.http.remote-apim.json` - Azure API Management integration

## Deployment and Operations

### Azure Deployment
- Use `azd up` for automated deployment
- Configure environment variables appropriately
- Monitor applications using Application Insights
- Implement proper logging and error handling

### Container Operations
- Build containers using provided Dockerfiles
- Tag images appropriately for Azure Container Registry
- Configure health checks and resource limits
- Implement graceful shutdown handling

### Monitoring and Observability
- Use structured logging with ILogger
- Implement Application Insights telemetry
- Monitor MCP server performance and usage
- Set up alerts for critical failures

This document serves as the primary reference for development practices and standards within the MCP .NET Samples repository. All contributors should familiarize themselves with these guidelines to ensure consistency and quality across the codebase.