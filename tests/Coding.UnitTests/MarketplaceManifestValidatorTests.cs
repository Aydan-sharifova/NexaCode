using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Enums;
using Coding.Exceptions;
using Coding.Infrastructure.AiAgent;
using Coding.Infrastructure.Marketplace;
using FluentAssertions;
using Moq;
using Xunit;

namespace Coding.UnitTests;

public sealed class MarketplaceManifestValidatorTests
{
    private readonly Mock<IAiToolRegistry> _tools = new();

    [Fact]
    public void Rejects_unknown_permissions()
    {
        var act = () => Validate(MarketplaceCategory.Snippet, """{"language":"csharp","content":"ok"}""", ["filesystem.root"]);
        act.Should().Throw<ConflictException>().WithMessage("*Unknown marketplace permission*");
    }

    [Fact]
    public void Rejects_executable_plugin_frontend_code()
    {
        var act = () => Validate(MarketplaceCategory.Plugin, """{"name":"bad","capabilities":[],"script":"alert(1)"}""");
        act.Should().Throw<ConflictException>().WithMessage("*executable frontend code*");
    }

    [Theory]
    [InlineData("{\"name\":\"template\",\"files\":{\"../escape.txt\":\"hello\"}}")]
    [InlineData("{\"name\":\"template\",\"files\":{\".env\":\"API_KEY=supersecretvalue123\"}}")]
    public void Rejects_unsafe_template_content(string json)
    {
        var act = () => Validate(MarketplaceCategory.ProjectTemplate, json);
        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Rejects_theme_url_payloads()
    {
        var act = () => Validate(MarketplaceCategory.Theme, """{"name":"bad","colors":{"background":"url(https://evil.test/x)"}}""");
        act.Should().Throw<ConflictException>().WithMessage("*safe CSS color*");
    }

    [Fact]
    public void Rejects_unregistered_agent_tools()
    {
        _tools.Setup(value => value.Describe("root_shell")).Throws(new UnknownAiToolException("root_shell"));
        var act = () => Validate(MarketplaceCategory.AiAgent, """{"name":"agent","systemPrompt":"help safely","allowedTools":["root_shell"]}""");
        act.Should().Throw<ConflictException>().WithMessage("*unknown tool*");
    }

    [Fact]
    public void Produces_stable_checksum_for_a_valid_agent()
    {
        _tools.Setup(value => value.Describe("project_read")).Returns(new AiToolDescriptor("project_read", "read", AiToolRiskLevel.ReadOnly, new HashSet<AiAgentMode>(), new HashSet<ProjectRole>(), typeof(object)));
        const string json = """{"name":"reviewer","systemPrompt":"Review project files.","allowedTools":["project_read"]}""";
        var first = Validate(MarketplaceCategory.AiAgent, json, ["project.read"]);
        var second = Validate(MarketplaceCategory.AiAgent, json, ["project.read"]);
        first.Checksum.Should().Be(second.Checksum).And.HaveLength(64);
    }

    private Coding.Application.Features.Marketplace.MarketplaceValidatedManifest Validate(MarketplaceCategory category, string json, IReadOnlyList<string>? permissions = null)
    {
        using var document = JsonDocument.Parse(json);
        return new MarketplaceManifestValidator(_tools.Object).Validate(category, document.RootElement, permissions ?? []);
    }
}
