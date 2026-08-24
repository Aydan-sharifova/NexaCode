using System.Text.RegularExpressions;
using Coding.Enums;

namespace Coding.Infrastructure.KnowledgeGraph;

public sealed record GraphSourceFile(Guid Id, string Path, string Content);
public sealed record ExtractedNode(Guid Id, Guid? SourceFileId, KnowledgeNodeKind Kind, string Key, string Name, string? Path, int? Line);
public sealed record ExtractedEdge(Guid Id, Guid FromNodeId, Guid ToNodeId, KnowledgeEdgeKind Kind, decimal Confidence, string Evidence);
public sealed record ExtractedGraph(IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges);

public static partial class KnowledgeGraphExtractor
{
    private const int MaxNodes = 30_000;
    private const int MaxEdges = 100_000;

    public static ExtractedGraph Extract(IReadOnlyList<GraphSourceFile> files)
    {
        var nodes = new List<ExtractedNode>(); var edges = new List<ExtractedEdge>();
        var byKey = new Dictionary<string, ExtractedNode>(StringComparer.OrdinalIgnoreCase);
        var fileNodes = new Dictionary<Guid, ExtractedNode>();
        var pathMap = files.ToDictionary(x => Normalize(x.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
        {
            var path = Normalize(file.Path); var node = AddNode(KnowledgeNodeKind.File, $"file:{path}", FileName(path), file.Id, path, null);
            fileNodes[file.Id] = node;
        }

        foreach (var file in files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
        {
            var fileNode = fileNodes[file.Id]; var path = Normalize(file.Path); var lines = file.Content.Replace("\r\n", "\n").Split('\n');
            var isTest = TestPathRegex().IsMatch(path);
            if (isTest)
            {
                var test = AddNode(KnowledgeNodeKind.Test, $"test:{path}", FileName(path), file.Id, path, 1);
                AddEdge(fileNode, test, KnowledgeEdgeKind.Contains, 1m, "Test-like filename.");
            }
            for (var index = 0; index < lines.Length && nodes.Count < MaxNodes; index++)
            {
                var line = lines[index]; var lineNumber = index + 1;
                foreach (Match match in ImportRegex().Matches(line))
                {
                    var importName = match.Groups[1].Value.Trim(); if (importName.Length == 0) continue;
                    var resolved = ResolveImport(path, importName, pathMap);
                    if (resolved is not null) AddEdge(fileNode, fileNodes[resolved.Id], KnowledgeEdgeKind.Imports, .95m, $"Import at line {lineNumber}.");
                    else { var dependency = AddNode(KnowledgeNodeKind.Import, $"import:{importName}", importName, null, null, null); AddEdge(fileNode, dependency, KnowledgeEdgeKind.Imports, .8m, $"Import declaration at line {lineNumber}."); }
                }
                foreach (Match match in TypeRegex().Matches(line))
                {
                    var category = match.Groups[1].Value; var name = match.Groups[2].Value;
                    var kind = name.EndsWith("Controller", StringComparison.Ordinal) ? KnowledgeNodeKind.Controller : name.EndsWith("Service", StringComparison.Ordinal) || name.EndsWith("Repository", StringComparison.Ordinal) ? KnowledgeNodeKind.Service : category.Equals("interface", StringComparison.OrdinalIgnoreCase) ? KnowledgeNodeKind.Interface : IsComponent(name, path) ? KnowledgeNodeKind.Component : KnowledgeNodeKind.Class;
                    var symbol = AddNode(kind, $"symbol:{path}:{kind}:{name}:{lineNumber}", name, file.Id, path, lineNumber); AddEdge(fileNode, symbol, KnowledgeEdgeKind.Contains, 1m, $"Declaration at line {lineNumber}.");
                }
                foreach (Match match in FunctionRegex().Matches(line))
                {
                    var name = match.Groups[1].Value; if (ControlWords.Contains(name)) continue;
                    var kind = IsComponent(name, path) ? KnowledgeNodeKind.Component : KnowledgeNodeKind.Method;
                    var symbol = AddNode(kind, $"function:{path}:{name}:{lineNumber}", name, file.Id, path, lineNumber); AddEdge(fileNode, symbol, KnowledgeEdgeKind.Contains, .95m, $"Function or method declaration at line {lineNumber}.");
                }
                foreach (Match match in ApiRegex().Matches(line))
                {
                    var verb = match.Groups[1].Value.ToUpperInvariant(); var route = match.Groups[2].Value; if (route.Length == 0) route = "(attribute route)";
                    var api = AddNode(KnowledgeNodeKind.ApiEndpoint, $"api:{path}:{verb}:{route}:{lineNumber}", $"{verb} {route}", file.Id, path, lineNumber); AddEdge(fileNode, api, KnowledgeEdgeKind.Exposes, .9m, $"Endpoint evidence at line {lineNumber}.");
                }
                foreach (Match match in TableRegex().Matches(line))
                {
                    var tableName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value; if (tableName.Length == 0) continue;
                    var table = AddNode(KnowledgeNodeKind.DatabaseTable, $"table:{tableName}", tableName, file.Id, path, lineNumber); AddEdge(fileNode, table, KnowledgeEdgeKind.PersistsTo, .85m, $"Database mapping at line {lineNumber}.");
                }
            }
        }

        var symbolsByName = nodes.Where(x => x.Kind is KnowledgeNodeKind.Class or KnowledgeNodeKind.Interface or KnowledgeNodeKind.Controller or KnowledgeNodeKind.Service or KnowledgeNodeKind.Component or KnowledgeNodeKind.ApiEndpoint or KnowledgeNodeKind.DatabaseTable)
            .GroupBy(x => x.Name, StringComparer.Ordinal).Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single(), StringComparer.Ordinal);
        foreach (var file in files)
        {
            var source = fileNodes[file.Id]; var tokens = IdentifierRegex().Matches(file.Content).Select(x => x.Value).Distinct(StringComparer.Ordinal).Take(5_000);
            var count = 0; foreach (var token in tokens)
            {
                if (!symbolsByName.TryGetValue(token, out var target) || target.SourceFileId == file.Id) continue;
                var kind = TestPathRegex().IsMatch(file.Path) ? KnowledgeEdgeKind.Tests : KnowledgeEdgeKind.Uses;
                AddEdge(source, target, kind, kind == KnowledgeEdgeKind.Tests ? .85m : .65m, $"Unique symbol reference '{token}'.");
                if (++count >= 200 || edges.Count >= MaxEdges) break;
            }
        }
        return new(nodes, edges);

        ExtractedNode AddNode(KnowledgeNodeKind kind, string key, string name, Guid? sourceFileId, string? path, int? line)
        {
            if (byKey.TryGetValue(key, out var existing)) return existing;
            if (nodes.Count >= MaxNodes) return nodes[0];
            var node = new ExtractedNode(Guid.NewGuid(), sourceFileId, kind, key.Length <= 700 ? key : key[..700], name.Length <= 300 ? name : name[..300], path, line);
            byKey[key] = node; nodes.Add(node); return node;
        }
        void AddEdge(ExtractedNode from, ExtractedNode to, KnowledgeEdgeKind kind, decimal confidence, string evidence)
        {
            if (from.Id == to.Id || edges.Count >= MaxEdges || edges.Any(x => x.FromNodeId == from.Id && x.ToNodeId == to.Id && x.Kind == kind)) return;
            edges.Add(new(Guid.NewGuid(), from.Id, to.Id, kind, confidence, evidence));
        }
    }

    private static GraphSourceFile? ResolveImport(string sourcePath, string import, IReadOnlyDictionary<string, GraphSourceFile> paths)
    {
        if (!import.StartsWith(".", StringComparison.Ordinal)) return null;
        var directory = sourcePath.Contains('/') ? sourcePath[..sourcePath.LastIndexOf('/')] : string.Empty;
        var basePath = NormalizeSegments(directory + "/" + import);
        foreach (var candidate in new[] { basePath, basePath + ".ts", basePath + ".tsx", basePath + ".js", basePath + ".jsx", basePath + ".cs", basePath + "/index.ts", basePath + "/index.tsx", basePath + "/index.js" }) if (paths.TryGetValue(candidate, out var file)) return file;
        return null;
    }
    private static string NormalizeSegments(string path)
    {
        var parts = new List<string>(); foreach (var part in Normalize(path).Split('/', StringSplitOptions.RemoveEmptyEntries)) { if (part == ".") continue; if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); } else parts.Add(part); } return string.Join('/', parts);
    }
    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static string FileName(string path) => path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
    private static bool IsComponent(string name, string path) => name.Length > 0 && char.IsUpper(name[0]) && (path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase));
    private static readonly HashSet<string> ControlWords = ["if", "for", "foreach", "while", "switch", "catch", "using", "return"];

    private static Regex ImportRegex() => CombinedImportRegex();
    [GeneratedRegex("""(?:\busing\s+|\bfrom\s+['"]|\brequire\s*\(\s*['"]|\bfrom\s+|\bimport\s+)([\w./@-]+)""")] private static partial Regex CombinedImportRegex();
    private static Regex TypeRegex() => TypeSimpleRegex();
    [GeneratedRegex(@"\b(class|interface|record|type)\s+([A-Za-z_][A-Za-z0-9_]*)")] private static partial Regex TypeSimpleRegex();
    [GeneratedRegex(@"\b(?:public|private|protected|internal|static|async|export|default|function|def)\s+(?:(?:static|async)\s+)?(?:[\w<>\[\],?.]+\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*\(")] private static partial Regex FunctionRegex();
    private static Regex ApiRegex() => ApiSimpleRegex();
    [GeneratedRegex("""(?:Http|Map|\.)(Get|Post|Put|Patch|Delete)\s*\(\s*['"]?([^'")\]]*)""", RegexOptions.IgnoreCase)] private static partial Regex ApiSimpleRegex();
    [GeneratedRegex("""(?:DbSet<[^>]+>\s+([A-Za-z_][A-Za-z0-9_]*)|(?:CREATE\s+TABLE|FROM|JOIN)\s+["`\[]?([A-Za-z_][A-Za-z0-9_.]*))""", RegexOptions.IgnoreCase)] private static partial Regex TableRegex();
    [GeneratedRegex(@"(^|/)(test|tests|spec|specs)(/|$)|(?:test|tests|spec)\.[^.]+$|Tests?\.(?:cs|java)$", RegexOptions.IgnoreCase)] private static partial Regex TestPathRegex();
    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*")] private static partial Regex IdentifierRegex();
}
