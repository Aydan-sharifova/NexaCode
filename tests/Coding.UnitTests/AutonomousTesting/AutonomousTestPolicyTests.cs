using Coding.Infrastructure.AutonomousTesting;
using Xunit;

namespace Coding.UnitTests.AutonomousTesting;

public sealed class AutonomousTestPolicyTests
{
    [Theory]
    [InlineData(-10, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(99, 3)]
    public void ClampIterations_enforces_a_hard_finite_boundary(int requested, int expected) =>
        Assert.Equal(expected, AutonomousTestPolicy.ClampIterations(requested));

    [Theory]
    [InlineData(0, false, "", true)]
    [InlineData(0, false, "   ", true)]
    [InlineData(1, false, "assertion failed", false)]
    [InlineData(0, true, "", false)]
    [InlineData(null, false, "", false)]
    [InlineData(0, false, "compiler warning or error", false)]
    public void Passing_result_requires_independent_clean_runtime_evidence(int? exitCode, bool timedOut, string stderr, bool expected) =>
        Assert.Equal(expected, AutonomousTestPolicy.IsPassingExecution(exitCode, timedOut, stderr));

    [Fact]
    public void Generated_harness_must_expose_the_server_selected_runner()
    {
        Assert.True(AutonomousTestPolicy.HasDedicatedRunner("class AutonomousTestRunner { public static void Main() {} }"));
        Assert.False(AutonomousTestPolicy.HasDedicatedRunner("class Program { static void Main() {} }"));
    }
}
