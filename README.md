PoC: OpenAPI spec parsing (JSON & YAML) using NSWAG OpenApiDocument & OpenApiYamlDocument

Overview
- This is a proof-of-concept console app that parses OpenAPI/Swagger specification files in JSON or YAML format and prints relevant information.
- It focuses on summarizing paths/operations, request/response schemas (including inferred component type names), parameters, and a consolidated Schema Usage Map.

What it extracts
- Paths and operations (OperationId and Summary)
- Request bodies: content types, schema details, and best-effort type names (component name when available or inferred)
- Responses: status codes, content types, schema details, and best-effort type names
- Parameters: location (query/path/header/cookie), schema type, name, description
- A final "Schema Usage Map" summarizing which schema is used where (request/response/parameter)

Project layout
- Project: `PoCAPISpecParsing` (.NET 8 console app)
- Sample specs in the project directory: `DevHadrianAPI.json`, `petstore.yaml`, `petstore.json`, `petstore-expanded.yaml`, `petstore-expanded.json`

Assumptions about spec files
- If you pass a relative filename, it must live in the project directory (next to `Program.cs`).
- Absolute paths are also accepted.
- If no argument is provided, the app defaults to `petstore-expanded.json` in the project directory.

Prerequisites
- .NET SDK 8.0+

Quick start
- Build:
```fish
dotnet build PoCAPISpecParsing/PoCAPISpecParsing.csproj
```
- Run with the default spec (`petstore-expanded.json` in the project directory):
```fish
dotnet run --project PoCAPISpecParsing/PoCAPISpecParsing.csproj
```
- Run with another spec in the project directory (e.g., YAML):
```fish
dotnet run --project PoCAPISpecParsing/PoCAPISpecParsing.csproj -- petstore.yaml
```
- Run with an absolute path:
```fish
dotnet run --project PoCAPISpecParsing/PoCAPISpecParsing.csproj -- /absolute/path/to/spec.yaml
```

Expected output
- The program prints the resolved spec path, then a hierarchical summary of paths, operations, request/response content types, and schema details with best-effort type names.
- At the end, it prints a "Schema Usage Map" showing each schema (by name or <anonymous>) and where it is used (request/response/parameter, path, operation, etc.).

Troubleshooting
- File not found: Ensure the file exists next to `Program.cs` (project directory) or pass an absolute path. The error message includes the resolved path.
- Unsupported extension: Only `.json`, `.yaml`, or `.yml` are supported.
- SDK errors: Verify `dotnet --version` reports 8.x and that the .NET 8 SDK is installed.

