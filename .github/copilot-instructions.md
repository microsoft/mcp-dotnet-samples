# Copilot Instructions for mcp-dotnet-samples

Purpose: Enable AI coding agents to work productively in this multi-sample .NET 9 MCP (Model Context Protocol) repository. Keep answers grounded in these concrete patterns; avoid inventing undocumented conventions.

## 1. Big Picture Architecture
- Mono-repo containing independent MCP server samples: `awesome-copilot/`, `markdown-to-html/`, `todo-list/`, (plus emerging samples like `outlook-email/`).
- Each sample follows the same "Hybrid" pattern: a console (STDIO) MCP server that can also run as an HTTP (stateless) endpoint when `--http` is passed ("streamable HTTP").
- Shared cross-cutting code lives in `shared/McpSamples.Shared/` (hosting, DI extensions, OpenAPI transformer, base `AppSettings`). Samples reference this project.
- Runtime selection logic: `AppSettings.UseStreamableHttp(env, args)` chooses between `Host.CreateApplicationBuilder` (STDIO) and `WebApplication.CreateBuilder` (HTTP). The extension `BuildApp(useStreamableHttp)` wires up MCP server transports:
  - STDIO: `.WithStdioServerTransport()`
  - HTTP: `.WithHttpTransport(o => o.Stateless = true)` + maps `/mcp` and (optionally) OpenAPI docs when sample adds OpenAPI services.
- Discovery pattern: Prompts, Resources, Tools are auto-registered via `.WithPromptsFromAssembly(...).WithResourcesFromAssembly(...).WithToolsFromAssembly(...)` scanning the entry assembly.
- HTTP mode adds OpenAPI documents (`swagger`=2.0 + `openapi`=3.0) with a custom `McpDocumentTransformer<T>` to surface MCP over a single POST `/mcp` endpoint including `x-ms-agentic-protocol: mcp-streamable-1.0`.
- `todo-list` adds an in-memory SQLite (single connection, `:memory:`) + EF Core context + repository abstraction; schema created at startup.
- `awesome-copilot` integrates external GitHub content through `IMetadataService` (HTTP client) and exposes search/load tools around large `metadata.json` (contains chatmodes/instructions/prompts metadata).

## 2. Key Directories & Files
- Root Dockerfiles: per sample (`Dockerfile.awesome-copilot`, etc.) build minimal container images.
- Infrastructure-as-code per sample in `*/infra/` with `main.bicep` + `azure.yaml` enabling `azd up` deploy to Azure Container Apps.
- VS Code MCP connection templates: `*/.vscode/mcp.*.json` provide STDIO vs HTTP vs container vs remote variants; copy one to repo root `.vscode/mcp.json` for activation.
- Shared abstractions:
  - `shared/.../Configurations/AppSettings.cs`: parses args (`--http`, `--help`) & environment variable `UseHttp`.
  - `shared/.../Extensions/HostApplicationBuilderExtensions.cs`: core hosting bootstrap.
  - `shared/.../OpenApi/McpDocumentTransformer.cs`: injects protocol metadata.

## 3. Execution & Developer Workflows
- Local STDIO run (example markdown-to-html):
  `dotnet run --project ./markdown-to-html/src/McpSamples.MarkdownToHtml.HybridApp`
- Local HTTP run (adds OpenAPI + /mcp):
  `dotnet run --project ./todo-list/src/McpSamples.TodoList.HybridApp -- --http`
- Container build & run (pattern—swap Dockerfile/sample name):
  `docker build -f Dockerfile.todo-list -t todo-list:latest .` then `docker run -i --rm -p 8080:8080 todo-list:latest [--http]`
- Azure deploy from sample folder:
  `azd auth login` then `azd up` (reads that folder's `azure.yaml` + `infra/main.bicep`). After deploy, fetch FQDN via `azd env get-value AZURE_RESOURCE_<...>_FQDN` (name differs per sample).
- Attach in VS Code Agent Mode: copy appropriate `mcp.*.<variant>.json` to `.vscode/mcp.json`, then run "MCP: List Servers".
- Passing extra sample-specific switches: markdown-to-html adds `-tc` (Tech Community), `-p` (extra paragraphs), `--tags`.

## 4. Conventions & Patterns
- All sample app entry points named `Program.cs` inside `src/<ProjectName>.HybridApp/` with suffix `.HybridApp` signaling dual-mode hosting.
- Command line argument delimiter: arguments after `--` are parsed by custom settings (e.g., `-- --http -tc -p`). Always preserve the `--` when appending custom flags in `mcp.*.json` STDIO configs.
- JSON serialization options (where used) enforce camelCase & case-insensitive property names.
- Repositories (only in todo-list) use EF Core async APIs + idempotent updates via `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for efficiency instead of tracking then saving changes.
- In-memory SQLite lifetime: a single opened `SqliteConnection` registered singleton to keep the in-memory DB alive for the process.
- OpenAPI documents only added in samples that explicitly configure them (todo-list, awesome-copilot). Markdown-to-html currently skips OpenAPI.
- Environment override: `UseHttp=true` env var triggers HTTP mode without passing `--http`.
- Large metadata scanning (awesome-copilot/metadata.json) should avoid loading entire file into memory repeatedly—reuse injected services / streaming where possible (follow existing `IMetadataService`).

## 5. Adding a New Sample (Keep Consistency)
1. Create `<sample-name>/` with `azure.yaml`, `infra/` (copy from an existing sample), `src/<PascalName>.HybridApp/` project referencing `McpSamples.Shared`.
2. Implement `Program.cs` mirroring pattern: parse `useStreamableHttp`, register `AddAppSettings<YourAppSettings>()`, add any HTTP/OpenAPI extras only if needed, then `BuildApp(useStreamableHttp)`.
3. Add `Dockerfile.<sample-name>` at repo root (follow others: multi-stage build with dotnet publish + runtime). Ensure final image exposes port 8080.
4. Provide `.vscode/mcp.*.json` variants (copy & rename from existing sample, adjust URL/args/project path token).
5. Update root `README.md` samples table (install badges reflect GHCR image tag `<sample-name>:latest`).

## 6. Safe Change Guidelines for Agents
- Do NOT refactor shared bootstrapping signatures (`BuildApp`, `AddAppSettings`) without updating all samples simultaneously.
- When adding new tool/prompt/resource classes, ensure they are public and in the entry assembly so automatic discovery picks them up—no manual registration needed.
- Preserve `:memory:` pattern for ephemeral examples; if introducing persistence, isolate it to that sample and do not leak dependencies into `shared/` unless truly cross-cutting.
- Keep Dockerfiles minimal & consistent; if adding libraries, prefer adding via the `dotnet publish` stage rather than ad-hoc runtime installs.

## 7. Common Pitfalls
- Forgetting `--` before custom args results in flags ignored by `AppSettings.Parse`.
- Omitting singleton open SQLite connection causes in-memory DB loss (new connection => empty DB).
- Adding OpenAPI without `AddHttpContextAccessor()` or mapping `/{documentName}.json` yields missing docs.
- Copying `mcp.*.json` but leaving wrong server name or port leads to silent connection failures in VS Code.

## 8. Quick Reference Ports (Local Default)
- awesome-copilot HTTP: 5250
- todo-list HTTP: 5240
- markdown-to-html HTTP: 5280
- Container runs map internal 8080 -> host 8080 (override only if needed).

## 9. Example: Modify todo-list Tool
To add a new tool (e.g., filter completed items):
- Create a public class with appropriate MCP Tool attribute (follow existing tool patterns in project—scan for existing Tool classes) inside the todo-list HybridApp assembly.
- Query `TodoDbContext` via injected repository; return concise results DTO.
- No extra registration needed—automatic assembly scan picks it up.

## 10. When Unsure
Prefer inspecting analogous implementation in another sample (diff minimal surface area) before introducing new abstractions. Ask for clarification if a change would alter cross-sample contracts.

---
Feedback welcome: Identify unclear sections (e.g., new sample onboarding, tool discovery details) so we can refine.
