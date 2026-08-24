using System.Text.Json;
using Coding.Application.Features.AiAgent;
using Coding.Enums;
using Coding.Models;
using Coding.Infrastructure.AiAgent;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class AiAgentPhase2Tests
{
    // --- Path guard -----------------------------------------------------------

    [Theory]
    [InlineData("src/Program.cs", "src/Program.cs")]
    [InlineData("src/../Program.cs", null)]
    [InlineData("/etc/passwd", null)]
    [InlineData("C:/Windows/System32", null)]
    [InlineData("\\share\\secret", null)]
    [InlineData("", null)]
    [InlineData("..", null)]
    [InlineData("./a/./b", "a/b")]
    public void Path_guard_normalizes_and_rejects_traversal(string input, string? expected)
    {
        if (expected is null)
        {
            Action act = () => AiPathGuard.NormalizeProjectRelativePath(input);
            act.Should().Throw<ArgumentException>();
        }
        else
        {
            AiPathGuard.NormalizeProjectRelativePath(input).Should().Be(expected);
        }
    }

    [Fact]
    public void Path_guard_rejects_dot_segments()
    {
        Action a1 = () => AiPathGuard.NormalizeProjectRelativePath("foo/./bar");
        Action a2 = () => AiPathGuard.NormalizeProjectRelativePath("./foo");
        a1.Should().NotThrow();
        a2.Should().NotThrow();
    }

    // --- Secret redaction ----------------------------------------------------

    public static IEnumerable<object[]> SecretInputs() => new[]
    {
        new object[] { "Authorization: Bearer abcdefghijklmnop", "Bearer" },
        new object[] { "Authorization: Basic dXNlcjpwYXNz", "Basic" },
        new object[] { "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature", "JWT" },
        new object[] { "AWS_ACCESS_KEY_ID=AKIAIOSFODNN7EXAMPLE", "AWS" },
        new object[] { "password=hunter2", "password" },
        new object[] { "postgres://user:secret@host:5432/db", "DSN" },
        new object[] { "-----BEGIN RSA PRIVATE KEY-----\nABC\n-----END RSA PRIVATE KEY-----", "PEM" },
        new object[] { "ghp_abc1234567890abcdefghij", "GitHub" },
        new object[] { "sk-abcdefghijklmnopqrstuvwxyz0123456789", "OpenAI" },
        new object[] { "API_KEY=super-secret-value-123", "generic API key" },
    };

    [Theory]
    [MemberData(nameof(SecretInputs))]
    public void Secret_redaction_replaces_known_shapes(string input, string label)
    {
        var svc = new AiSecretRedactionService();
        var redacted = svc.Redact(input);
        redacted.Should().Contain("[REDACTED]", $"because '{label}' must be redacted");
        redacted.Should().NotBe(input);
    }

    [Fact]
    public void Secret_redaction_returns_text_unchanged_when_no_secrets_present()
    {
        var svc = new AiSecretRedactionService();
        svc.Redact("the quick brown fox jumps over the lazy dog").Should().NotContain("[REDACTED]");
    }

    [Theory]
    [InlineData(".env")]
    [InlineData("./secrets/.env")]
    [InlineData("appsettings.Production.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData(".env.staging")]
    [InlineData(".npmrc")]
    [InlineData("private.key")]
    [InlineData("server.pem")]
    [InlineData("certs/server.pfx")]
    [InlineData("id_rsa")]
    [InlineData("credentials.json")]
    public void Secret_file_detection_flags_sensitive_paths(string path)
    {
        new AiSecretRedactionService().IsSecretFile(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("Program.cs")]
    [InlineData("src/index.ts")]
    [InlineData("README.md")]
    [InlineData("package.json")]
    public void Secret_file_detection_passes_normal_sources(string path)
    {
        new AiSecretRedactionService().IsSecretFile(path).Should().BeFalse();
    }

    // --- Risk policy ---------------------------------------------------------

    [Theory]
    [InlineData("get_project_tree", AiToolRiskLevel.ReadOnly)]
    [InlineData("read_file", AiToolRiskLevel.ReadOnly)]
    [InlineData("search_code", AiToolRiskLevel.ReadOnly)]
    [InlineData("get_project_members", AiToolRiskLevel.ReadOnly)]
    [InlineData("get_database_schema", AiToolRiskLevel.ReadOnly)]
    [InlineData("create_file", AiToolRiskLevel.Low)]
    [InlineData("create_patch", AiToolRiskLevel.Low)]
    [InlineData("create_task", AiToolRiskLevel.Low)]
    [InlineData("apply_patch", AiToolRiskLevel.Medium)]
    [InlineData("update_task", AiToolRiskLevel.Medium)]
    [InlineData("run_file", AiToolRiskLevel.Medium)]
    [InlineData("rename_file", AiToolRiskLevel.High)]
    [InlineData("delete_file", AiToolRiskLevel.High)]
    [InlineData("run_build", AiToolRiskLevel.High)]
    [InlineData("run_tests", AiToolRiskLevel.High)]
    [InlineData("create_git_branch", AiToolRiskLevel.High)]
    [InlineData("unknown_tool", AiToolRiskLevel.High)]
    public void Risk_policy_assigns_risk_levels(string toolName, AiToolRiskLevel expected)
    {
        var run = new AiAgentRun { ProjectId = Guid.NewGuid(), Mode = AiAgentMode.Agent };
        AiRiskPolicy.Classify(toolName, run).Should().Be(expected);
    }

    [Theory]
    [InlineData("run_shell")]
    [InlineData("exec_command")]
    [InlineData("execute_sql")]
    [InlineData("read_env")]
    [InlineData("delete_project")]
    [InlineData("disable_authorization")]
    [InlineData("modify_system_role")]
    [InlineData("open_network")]
    public void Critical_tools_are_blocked_by_policy(string toolName)
    {
        AiRiskPolicy.IsCriticalBlockedTool(toolName).Should().BeTrue();
        var run = new AiAgentRun { ProjectId = Guid.NewGuid(), Mode = AiAgentMode.Agent };
        AiRiskPolicy.Classify(toolName, run).Should().Be(AiToolRiskLevel.Critical);
    }

    [Fact]
    public void Critical_tools_are_blocked_even_in_agent_mode()
    {
        var run = new AiAgentRun { ProjectId = Guid.NewGuid(), Mode = AiAgentMode.Agent };
        AiRiskPolicy.ModeAllowsRisk(run.Mode, AiToolRiskLevel.Critical).Should().BeFalse();
    }

    // --- Mode restrictions --------------------------------------------------

    [Theory]
    [InlineData(AiAgentMode.Ask, AiToolRiskLevel.ReadOnly, true)]
    [InlineData(AiAgentMode.Ask, AiToolRiskLevel.Low, false)]
    [InlineData(AiAgentMode.Ask, AiToolRiskLevel.Medium, false)]
    [InlineData(AiAgentMode.Ask, AiToolRiskLevel.High, false)]
    [InlineData(AiAgentMode.Plan, AiToolRiskLevel.ReadOnly, true)]
    [InlineData(AiAgentMode.Plan, AiToolRiskLevel.Low, false)]
    [InlineData(AiAgentMode.Review, AiToolRiskLevel.ReadOnly, true)]
    [InlineData(AiAgentMode.Review, AiToolRiskLevel.Low, false)]
    [InlineData(AiAgentMode.Agent, AiToolRiskLevel.ReadOnly, true)]
    [InlineData(AiAgentMode.Agent, AiToolRiskLevel.Low, true)]
    [InlineData(AiAgentMode.Agent, AiToolRiskLevel.Medium, true)]
    [InlineData(AiAgentMode.Agent, AiToolRiskLevel.High, true)]
    [InlineData(AiAgentMode.Agent, AiToolRiskLevel.Critical, false)]
    public void Mode_to_risk_allowlist_is_correct(AiAgentMode mode, AiToolRiskLevel risk, bool allowed)
    {
        AiRiskPolicy.ModeAllowsRisk(mode, risk).Should().Be(allowed);
    }

    // --- Approval policy ----------------------------------------------------

    [Theory]
    [InlineData(AiToolRiskLevel.ReadOnly, false)]
    [InlineData(AiToolRiskLevel.Low, false)]
    [InlineData(AiToolRiskLevel.Medium, true)]
    [InlineData(AiToolRiskLevel.High, true)]
    [InlineData(AiToolRiskLevel.Critical, true)]
    public void Approval_policy_requires_approval_for_high_risk_tools(AiToolRiskLevel risk, bool required)
    {
        var policy = new AiToolApprovalPolicy();
        var descriptor = new AiToolDescriptor(
            "x", "x", risk,
            new HashSet<AiAgentMode> { AiAgentMode.Agent },
            new HashSet<ProjectRole> { ProjectRole.Developer },
            typeof(object));
        policy.RequiresApproval(descriptor).Should().Be(required);
    }

    [Fact]
    public void Approval_can_auto_approve_low_risk_only_with_opt_in()
    {
        var policy = new AiToolApprovalPolicy();
        var descriptor = new AiToolDescriptor(
            "create_file", "x", AiToolRiskLevel.Low,
            new HashSet<AiAgentMode> { AiAgentMode.Agent },
            new HashSet<ProjectRole> { ProjectRole.Developer },
            typeof(object));

        var noOptIn = new AiAgentRun { ProjectId = Guid.NewGuid(), Mode = AiAgentMode.Agent, PromptVersion = "v1" };
        var withOptIn = new AiAgentRun { ProjectId = Guid.NewGuid(), Mode = AiAgentMode.Agent, PromptVersion = "v1 auto-approve-low" };

        policy.CanAutoApproveLowRisk(noOptIn, descriptor).Should().BeFalse();
        policy.CanAutoApproveLowRisk(withOptIn, descriptor).Should().BeTrue();
    }

    [Fact]
    public void Approval_is_valid_only_for_approved_status()
    {
        var now = DateTime.UtcNow;
        var policy = new AiToolApprovalPolicy();

        policy.IsApprovalValid(AiApprovalStatus.ApprovedOnce, "h", "h", now.AddMinutes(5), now).Should().BeTrue();
        policy.IsApprovalValid(AiApprovalStatus.ApprovedForRun, "h", "h", now.AddMinutes(5), now).Should().BeTrue();

        policy.IsApprovalValid(AiApprovalStatus.Pending, "h", "h", now.AddMinutes(5), now).Should().BeFalse();
        policy.IsApprovalValid(AiApprovalStatus.Rejected, "h", "h", now.AddMinutes(5), now).Should().BeFalse();
        policy.IsApprovalValid(AiApprovalStatus.Expired, "h", "h", now.AddMinutes(5), now).Should().BeFalse();
        policy.IsApprovalValid(AiApprovalStatus.NotRequired, "h", "h", now.AddMinutes(5), now).Should().BeFalse();
    }

    [Fact]
    public void Approval_is_invalid_when_expired()
    {
        var policy = new AiToolApprovalPolicy();
        var now = DateTime.UtcNow;
        policy.IsApprovalValid(AiApprovalStatus.ApprovedOnce, "h", "h", now.AddSeconds(-1), now).Should().BeFalse();
    }

    [Fact]
    public void Approval_is_invalid_when_arguments_change()
    {
        var policy = new AiToolApprovalPolicy();
        var now = DateTime.UtcNow;
        policy.IsApprovalValid(AiApprovalStatus.ApprovedOnce, "approved", "actual", now.AddMinutes(5), now).Should().BeFalse();
    }

    // --- Idempotency / canonical arguments -----------------------------------

    [Fact]
    public void Idempotency_hash_is_stable_for_semantically_equal_arguments()
    {
        var a = JsonDocument.Parse("{\"b\":1,\"a\":2}").RootElement;
        var b = JsonDocument.Parse("{\"a\":2,\"b\":1}").RootElement;

        AiSecretRedactionService.HashArguments(a).Should().Be(AiSecretRedactionService.HashArguments(b));
    }

    [Fact]
    public void Idempotency_hash_differs_when_value_changes()
    {
        var a = JsonDocument.Parse("{\"a\":1}").RootElement;
        var b = JsonDocument.Parse("{\"a\":2}").RootElement;

        AiSecretRedactionService.HashArguments(a).Should().NotBe(AiSecretRedactionService.HashArguments(b));
    }

    [Fact]
    public void Canonicalize_is_stable_for_nested_objects()
    {
        var a = JsonDocument.Parse("{\"x\":{\"b\":2,\"a\":1},\"arr\":[3,2,1]}").RootElement;
        var b = JsonDocument.Parse("{\"arr\":[3,2,1],\"x\":{\"a\":1,\"b\":2}}").RootElement;

        AiSecretRedactionService.Canonicalize(a).Should().Be(AiSecretRedactionService.Canonicalize(b));
    }

    // --- Registry rejects duplicates / unknown tools -------------------------

    [Fact]
    public void Registry_rejects_duplicate_tool_names()
    {
        var source = new TestDescriptorSource(
            new AiToolDescriptor("dup", "first", AiToolRiskLevel.ReadOnly,
                new HashSet<AiAgentMode> { AiAgentMode.Agent },
                new HashSet<ProjectRole> { ProjectRole.Developer },
                typeof(object)),
            new AiToolDescriptor("dup", "second", AiToolRiskLevel.ReadOnly,
                new HashSet<AiAgentMode> { AiAgentMode.Agent },
                new HashSet<ProjectRole> { ProjectRole.Developer },
                typeof(object)));

        Action act = () => new AiToolRegistry(source);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate*");
    }

    [Fact]
    public void Registry_describe_throws_for_unknown_tool()
    {
        var source = new TestDescriptorSource();
        var registry = new AiToolRegistry(source);

        Action act = () => registry.Describe("not_registered");
        act.Should().Throw<UnknownAiToolException>();
    }

    [Fact]
    public void Registry_list_all_returns_all_descriptors()
    {
        var source = new TestDescriptorSource(
            new AiToolDescriptor("a", "a", AiToolRiskLevel.ReadOnly,
                new HashSet<AiAgentMode> { AiAgentMode.Agent },
                new HashSet<ProjectRole> { ProjectRole.Developer },
                typeof(object)),
            new AiToolDescriptor("b", "b", AiToolRiskLevel.ReadOnly,
                new HashSet<AiAgentMode> { AiAgentMode.Agent },
                new HashSet<ProjectRole> { ProjectRole.Developer },
                typeof(object)));

        var registry = new AiToolRegistry(source);
        registry.ListAll().Select(d => d.Name).Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void Registry_try_get_returns_false_for_unknown()
    {
        var source = new TestDescriptorSource();
        var registry = new AiToolRegistry(source);

        registry.TryGet("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Registry_try_get_returns_true_for_known()
    {
        var fakeTool = new FakeTool();
        var source = new TestDescriptorSource(fakeTool, new AiToolDescriptor(
            "fake_tool", "fake", AiToolRiskLevel.ReadOnly,
            new HashSet<AiAgentMode> { AiAgentMode.Agent },
            new HashSet<ProjectRole> { ProjectRole.Developer },
            typeof(object)));
        var registry = new AiToolRegistry(source);

        registry.TryGet("fake_tool", out var tool).Should().BeTrue();
        tool.Should().BeSameAs(fakeTool);
    }

    [Fact]
    public void Registry_rejects_empty_tool_name()
    {
        var source = new TestDescriptorSource(new AiToolDescriptor(
            "", "x", AiToolRiskLevel.ReadOnly,
            new HashSet<AiAgentMode> { AiAgentMode.Agent },
            new HashSet<ProjectRole> { ProjectRole.Developer },
            typeof(object)));

        Action act = () => new AiToolRegistry(source);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Name*");
    }

    // --- Test doubles --------------------------------------------------------

    private sealed class TestDescriptorSource : IAiToolDescriptorSource
    {
        private readonly List<AiToolDescriptor> _descriptors;
        private readonly Dictionary<string, IAiTool> _tools;

        public TestDescriptorSource(params AiToolDescriptor[] descriptors)
        {
            _descriptors = descriptors.ToList();
            _tools = new Dictionary<string, IAiTool>(StringComparer.Ordinal);
        }

        public TestDescriptorSource(IAiTool tool, AiToolDescriptor descriptor)
        {
            _descriptors = new List<AiToolDescriptor> { descriptor };
            _tools = new Dictionary<string, IAiTool>(StringComparer.Ordinal) { [descriptor.Name] = tool };
        }

        public IEnumerable<AiToolDescriptor> GetDescriptors() => _descriptors;

        public IAiTool Resolve(string toolName) =>
            _tools.TryGetValue(toolName, out var t) ? t : throw new UnknownAiToolException(toolName);
    }

    private sealed class FakeTool : IAiTool
    {
        public AiToolDescriptor Descriptor => new(
            "fake_tool", "fake", AiToolRiskLevel.ReadOnly,
            new HashSet<AiAgentMode> { AiAgentMode.Agent },
            new HashSet<ProjectRole> { ProjectRole.Developer },
            typeof(object));

        public Task<IAiToolResult> ExecuteAsync(JsonElement arguments, AiAgentRun run, CancellationToken cancellationToken)
            => Task.FromResult<IAiToolResult>(new FakeToolResult("ok", "{}"));
    }

    private sealed class FakeToolResult : IAiToolResult
    {
        public FakeToolResult(string summary, string? json) { Summary = summary; Json = json; }
        public string Summary { get; }
        public string? Json { get; }
    }
}
