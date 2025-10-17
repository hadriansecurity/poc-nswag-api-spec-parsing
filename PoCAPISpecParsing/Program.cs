using NSwag;
using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace PoCAPISpecParsing;

// Data model for schema usage tracking
public class SchemaUsage
{
    public string? SchemaName { get; set; } // e.g., Pet
    public string? RefPath { get; set; } // e.g., #/components/schemas/Pet
    public string? UsageType { get; set; } // e.g., response, request, parameter
    public string? OperationId { get; set; }
    public string? Path { get; set; }
    public string? ParameterName { get; set; } // for parameters
    public string? StatusCode { get; set; } // for responses
}

internal class Program
{
    public static async Task Main(string[] args)
    {
        // Priority: first CLI arg (path or filename) -> default "petstore-expanded.json".
        var specArg = (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            ? args[0]
            : "petstore-expanded.json";

        // Resolve the spec path:
        // - If an absolute path is provided, use it as-is.
        // - Otherwise, assume the file is in the project directory (next to Program.cs).
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var specPath = Path.IsPathRooted(specArg)
            ? specArg
            : Path.Combine(projectDir, specArg);

        if (!File.Exists(specPath))
        {
            Console.Error.WriteLine("OpenAPI spec file not found.");
            Console.Error.WriteLine($"Resolved path: {specPath}");
            Console.Error.WriteLine("Tip: place the spec file in the project directory or pass an absolute path.");
            Console.Error.WriteLine("Example: dotnet run --project PoCAPISpecParsing/PoCAPISpecParsing.csproj -- DevHadrianAPI.json");
            return;
        }

        var file = new FileInfo(specPath);
        Console.WriteLine($"Using spec: {file.FullName}");

        // Load the OpenAPI document directly (no patching)
        var document = file.Extension.ToLowerInvariant() switch
        {
            ".json" => await OpenApiDocument.FromJsonAsync(await File.ReadAllTextAsync(file.FullName), file.FullName),
            ".yaml" or ".yml" => await OpenApiYamlDocument.FromFileAsync(file.FullName),
            _ => throw new InvalidOperationException("File must be .json or .yaml")
        };

        // Build a dictionary of canonical JSON for all schemas in components/schemas
        var schemaNameByCanonicalJson = new Dictionary<string, string>();
        foreach (var kvp in document.Components.Schemas)
        {
            // Use JToken.FromObject for canonicalization (robust for all schemas)
            var canonical = JToken.FromObject(kvp.Value).ToString(Newtonsoft.Json.Formatting.None);
            schemaNameByCanonicalJson[canonical] = kvp.Key;
        }

        var schemaUsages = new List<SchemaUsage>();

        foreach (var path in document.Paths)
        {
            int pathIndent = 1;
            Console.WriteLine($"{Indent(pathIndent)}Path: {path.Key}");
            foreach (var operation in path.Value.ActualPathItem.ActualPathItem.Values)
            {
                int opIndent = pathIndent + 1;
                Console.WriteLine($"{Indent(opIndent)}Operation: {operation.OperationId} - {operation.Summary}");
                // Request Body
                if (operation.RequestBody != null)
                {
                    int reqIndent = opIndent + 1;
                    Console.WriteLine($"{Indent(reqIndent)}Request Body:");
                    foreach (var content in operation.RequestBody.Content)
                    {
                        int contentIndent = reqIndent + 1;
                        Console.WriteLine($"{Indent(contentIndent)}Content Type: {content.Key}");
                        var originalSchema = content.Value.Schema;
                        var schema = originalSchema?.ActualSchema;
                        PrintSchemaWithTypeName(originalSchema, schema, contentIndent + 1, schemaNameByCanonicalJson);
                        // Track usage
                        var refPath = originalSchema?.Reference?.DocumentPath;
                        string? schemaName = GetSchemaTypeName(originalSchema);
                        if (string.IsNullOrEmpty(schemaName) && schema != null)
                        {
                            var canonical = JToken.FromObject(schema).ToString(Newtonsoft.Json.Formatting.None);
                            if (schemaNameByCanonicalJson.TryGetValue(canonical, out var foundName))
                                schemaName = foundName;
                        }
                        schemaUsages.Add(new SchemaUsage
                        {
                            SchemaName = schemaName,
                            RefPath = refPath,
                            UsageType = "request",
                            OperationId = operation.OperationId,
                            Path = path.Key
                        });
                    }
                }
                // Responses
                Console.WriteLine($"{Indent(opIndent + 1)}Responses:");
                foreach (var response in operation.Responses)
                {
                    int respIndent = opIndent + 2;
                    Console.WriteLine($"{Indent(respIndent)}{response.Key}: {response.Value.Description}");
                    foreach (var content in response.Value.Content)
                    {
                        int contentIndent = respIndent + 1;
                        Console.WriteLine($"{Indent(contentIndent)}Content Type: {content.Key}");
                        var originalSchema = content.Value.Schema;
                        var schema = originalSchema?.ActualSchema;
                        PrintSchemaWithTypeName(originalSchema, schema, contentIndent + 1, schemaNameByCanonicalJson);
                        // Track usage
                        var refPath = originalSchema?.Reference?.DocumentPath;
                        string? schemaName = GetSchemaTypeName(originalSchema);
                        if (string.IsNullOrEmpty(schemaName) && schema != null)
                        {
                            var canonical = JToken.FromObject(schema).ToString(Newtonsoft.Json.Formatting.None);
                            if (schemaNameByCanonicalJson.TryGetValue(canonical, out var foundName))
                                schemaName = foundName;
                        }
                        schemaUsages.Add(new SchemaUsage
                        {
                            SchemaName = schemaName,
                            RefPath = refPath,
                            UsageType = "response",
                            OperationId = operation.OperationId,
                            Path = path.Key,
                            StatusCode = response.Key
                        });
                    }
                }
                // Parameters
                Console.WriteLine($"{Indent(opIndent + 1)}Parameters:");
                foreach (var parameter in operation.Parameters)
                {
                    Console.WriteLine($"{Indent(opIndent + 2)}{parameter.Kind} - {parameter.Schema?.Type} - {parameter.Name} - {parameter.Description}");
                    // Track usage
                    var refPath = parameter.Schema?.Reference?.DocumentPath;
                    string? schemaName = GetSchemaTypeName(parameter.Schema);
                    if (string.IsNullOrEmpty(schemaName) && parameter.Schema != null)
                    {
                        var canonical = JToken.FromObject(parameter.Schema).ToString(Newtonsoft.Json.Formatting.None);
                        if (schemaNameByCanonicalJson.TryGetValue(canonical, out var foundName))
                            schemaName = foundName;
                    }
                    schemaUsages.Add(new SchemaUsage
                    {
                        SchemaName = schemaName,
                        RefPath = refPath,
                        UsageType = "parameter",
                        OperationId = operation.OperationId,
                        Path = path.Key,
                        ParameterName = parameter.Name
                    });
                }
            }
        }

        // Print the schema usage map for verification
        Console.WriteLine("\n--- Schema Usage Map ---");
        foreach (var usage in schemaUsages)
        {
            Console.WriteLine($"Schema: {usage.SchemaName ?? "<anonymous>"}, Ref: {usage.RefPath ?? "<inline>"}, Usage: {usage.UsageType ?? "<unknown>"}, Path: {usage.Path ?? "<unknown>"}, Operation: {usage.OperationId ?? "<unknown>"}, Parameter: {usage.ParameterName ?? ""}, Status: {usage.StatusCode ?? ""}");
        }

        // Print schema with type name, handling arrays and objects, and matching inlined schemas to components
        void PrintSchemaWithTypeName(JsonSchema? originalSchema, JsonSchema? resolvedSchema, int indentLevel, Dictionary<string, string> schemaIndex)
        {
            string indent = Indent(indentLevel);
            string? typeName = null;
            // For arrays, try to get the type name of the items
            if (resolvedSchema?.IsArray == true && resolvedSchema.Item != null)
            {
                typeName = GetSchemaTypeName(resolvedSchema.Item);
                if (string.IsNullOrEmpty(typeName))
                {
                    var canonical = JToken.FromObject(resolvedSchema.Item).ToString(Newtonsoft.Json.Formatting.None);
                    if (schemaIndex.TryGetValue(canonical, out var foundName))
                        typeName = foundName;
                }
            }
            else if (resolvedSchema != null)
            {
                typeName = GetSchemaTypeName(originalSchema);
                if (string.IsNullOrEmpty(typeName))
                {
                    var canonical = JToken.FromObject(resolvedSchema).ToString(Newtonsoft.Json.Formatting.None);
                    if (schemaIndex.TryGetValue(canonical, out var foundName))
                        typeName = foundName;
                }
            }
            if (!string.IsNullOrEmpty(typeName))
                Console.WriteLine($"{indent}Type Name: {typeName}");
            if (resolvedSchema != null)
            {
                var pretty = JToken.FromObject(resolvedSchema).ToString(Newtonsoft.Json.Formatting.Indented);
                foreach (var line in pretty.Split('\n'))
                    Console.WriteLine($"{indent}{line}");
            }
        }
        // Helper to generate indentation string
        string Indent(int level) => new(' ', level * 2);

        // Helper to get the schema type name from Reference or ReferencePath
        string? GetSchemaTypeName(JsonSchema? schema)
        {
            if (schema?.Reference != null)
            {
                if (!string.IsNullOrEmpty(schema.Reference.Title))
                    return schema.Reference.Title;
                if (!string.IsNullOrEmpty(schema.Reference.DocumentPath))
                {
                    var path = schema.Reference.DocumentPath;
                    var last = path.Split('/');
                    if (last.Length > 0)
                        return last[^1];
                }
            }
            // Try direct title
            if (!string.IsNullOrEmpty(schema?.Title))
                return schema.Title;
            return null;
        }
    }
}
