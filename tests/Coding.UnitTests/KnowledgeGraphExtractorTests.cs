using Coding.Enums;
using Coding.Infrastructure.KnowledgeGraph;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class KnowledgeGraphExtractorTests
{
    [Fact]
    public void Extracts_backend_api_service_table_and_test_relationships()
    {
        var serviceId = Guid.NewGuid(); var controllerId = Guid.NewGuid(); var testId = Guid.NewGuid();
        var graph = KnowledgeGraphExtractor.Extract([
            new(serviceId, "src/AuthService.cs", "public class AuthService { public void Login() {} }\npublic DbSet<User> Users { get; set; }"),
            new(controllerId, "src/AuthController.cs", "using System;\npublic class AuthController { private readonly AuthService service;\n[HttpPost(\"login\")] public void Login() {} }"),
            new(testId, "tests/AuthServiceTests.cs", "public class AuthServiceTests { public void Verifies_login() { AuthService service; } }")
        ]);

        graph.Nodes.Should().Contain(x => x.Kind == KnowledgeNodeKind.Controller && x.Name == "AuthController");
        graph.Nodes.Should().Contain(x => x.Kind == KnowledgeNodeKind.Service && x.Name == "AuthService");
        graph.Nodes.Should().Contain(x => x.Kind == KnowledgeNodeKind.ApiEndpoint && x.Name == "POST login");
        graph.Nodes.Should().Contain(x => x.Kind == KnowledgeNodeKind.DatabaseTable && x.Name == "Users");
        var service = graph.Nodes.Single(x => x.Kind == KnowledgeNodeKind.Service && x.Name == "AuthService");
        graph.Edges.Should().Contain(x => x.ToNodeId == service.Id && x.Kind == KnowledgeEdgeKind.Uses);
        graph.Edges.Should().Contain(x => x.ToNodeId == service.Id && x.Kind == KnowledgeEdgeKind.Tests);
    }

    [Fact]
    public void Resolves_relative_frontend_imports_and_components()
    {
        var storeId = Guid.NewGuid(); var loginId = Guid.NewGuid();
        var graph = KnowledgeGraphExtractor.Extract([
            new(storeId, "src/authStore.ts", "export class AuthStore {}"),
            new(loginId, "src/Login.tsx", "import { AuthStore } from './authStore';\nexport function Login() { return <main />; }")
        ]);
        var loginFile = graph.Nodes.Single(x => x.SourceFileId == loginId && x.Kind == KnowledgeNodeKind.File);
        var storeFile = graph.Nodes.Single(x => x.SourceFileId == storeId && x.Kind == KnowledgeNodeKind.File);
        graph.Nodes.Should().Contain(x => x.Kind == KnowledgeNodeKind.Component && x.Name == "Login");
        graph.Edges.Should().Contain(x => x.FromNodeId == loginFile.Id && x.ToNodeId == storeFile.Id && x.Kind == KnowledgeEdgeKind.Imports);
    }
}
