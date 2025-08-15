# GitHub Copilot Instructions for MCP .NET Samples

## Project Overview

This repository contains Model Context Protocol (MCP) .NET sample implementations showcasing how to build MCP servers using .NET 9.0. The project includes three main samples: Awesome Copilot (GitHub integration), Markdown to HTML converter, and Todo List management.

## Coding Standards and Practices

### .NET Development Standards
- **Target Framework**: Use .NET 9.0 as specified in Directory.Build.props
- **Language Version**: Use latest C# language features
- **Nullability**: Enable nullable reference types (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Leverage implicit global usings (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Async/Await**: Prefer async/await patterns for I/O operations, especially for MCP server implementations

### Project Structure Conventions
- Each sample should be self-contained with its own solution file (`.sln`)
- Use the naming pattern: `McpSamples.{SampleName}.{ComponentType}`
- Organize projects with `src/` directories containing the main implementation
- Include `infra/` directories for Azure deployment (Bicep files)
- Provide Docker support with appropriate Dockerfiles

### MCP Server Implementation Guidelines
- Implement MCP servers using the shared `McpSamples.Shared` library
- Use proper dependency injection patterns
- Follow the MCP protocol specifications for tools, prompts, and resources
- Implement proper error handling and logging
- Use descriptive attributes for MCP server components:
  - `[McpServerPromptType]` for prompt classes
  - `[McpServerPrompt]` for prompt methods
  - `[Description]` attributes for parameters and methods

### Code Style Guidelines
- Follow Microsoft C# coding conventions
- Use meaningful variable and method names
- Implement comprehensive XML documentation comments
- Use readonly fields where appropriate
- Prefer record types for immutable data models
- Use required properties for essential data

### Azure Integration Standards
- Use Bicep for Infrastructure as Code
- Follow Azure naming conventions with abbreviations
- Implement proper Azure identity and security practices
- Use Azure Container Apps for deployment
- Configure Application Insights for monitoring

### Testing Requirements
- Write unit tests for all business logic
- Use descriptive test method names following Given_When_Then pattern
- Mock external dependencies appropriately
- Test MCP server functionality including tools and prompts
- Ensure tests cover error scenarios

## Code Review Guidelines

### Security Checklist
- [ ] Verify no hardcoded secrets or connection strings
- [ ] Ensure proper input validation and sanitization
- [ ] Check for proper authentication and authorization
- [ ] Validate Azure resource security configurations
- [ ] Review Docker image security practices

### Performance Considerations
- [ ] Check for efficient async/await usage
- [ ] Verify proper disposal of resources (using statements)
- [ ] Review memory allocation patterns
- [ ] Ensure proper caching where appropriate
- [ ] Validate container resource requirements

### MCP Protocol Compliance
- [ ] Verify correct MCP server registration and initialization
- [ ] Check tool and prompt implementations follow MCP specifications
- [ ] Ensure proper error handling and response formatting
- [ ] Validate resource management and cleanup

## Pull Request Guidelines

### Commit Message Format
Use conventional commit format:
- `feat: add new MCP tool for file operations`
- `fix: resolve connection timeout in MCP server`
- `docs: update README with deployment instructions`
- `refactor: optimize MCP message handling`

### PR Description Template
Include the following in your PR description:
1. **Summary**: Brief description of changes
2. **Changes**: List of specific modifications
3. **Testing**: How the changes were tested
4. **Azure**: Any infrastructure changes
5. **Breaking Changes**: List any breaking changes
6. **Screenshots**: For UI changes (if applicable)

### Required Checks
- [ ] Code builds successfully for .NET 9.0
- [ ] All existing tests pass
- [ ] New features include appropriate tests
- [ ] Documentation is updated
- [ ] Azure deployment scripts are validated
- [ ] Docker images build successfully
- [ ] MCP server functionality is verified

## Preferred Technologies and Libraries

### Core Technologies
- **.NET 9.0** - Primary target framework
- **C# Latest** - Use latest language features
- **ASP.NET Core** - For HTTP-based MCP servers
- **System.Text.Json** - Preferred JSON serialization
- **Microsoft.Extensions.*** - Use built-in dependency injection and configuration

### Azure Services
- **Azure Container Apps** - Primary deployment target
- **Azure Container Registry** - For container images
- **Application Insights** - For monitoring and telemetry
- **Azure Identity** - For authentication

### Development Tools
- **Visual Studio Code** - Recommended IDE
- **Docker Desktop** - For local container development
- **Azure CLI** - For Azure operations
- **Azure Developer CLI (azd)** - For deployment automation

## Sample-Specific Guidelines

### Awesome Copilot Sample
- Focus on GitHub integration and file system operations
- Implement proper metadata handling for Copilot instructions
- Use structured search and filtering capabilities

### Markdown to HTML Sample
- Ensure proper HTML sanitization and security
- Support various markdown extensions
- Implement configurable HTML output options

### Todo List Sample
- Use proper data persistence patterns
- Implement CRUD operations efficiently
- Follow RESTful API principles for HTTP endpoints

## Additional Notes

- Always test MCP server functionality with VS Code MCP integration
- Ensure compatibility with both local and containerized deployments
- Follow Microsoft Open Source guidelines and code of conduct
- Update documentation when adding new features or changing existing behavior
- Consider backward compatibility when making changes to MCP interfaces