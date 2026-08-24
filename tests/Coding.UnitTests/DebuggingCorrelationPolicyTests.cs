using Coding.Infrastructure.Debugging;
using Xunit;

namespace Coding.UnitTests;

public sealed class DebuggingCorrelationPolicyTests
{
    [Fact]
    public void Exact_git_patch_path_is_required()
    {
        const string patch="diff --git a/src/A.cs b/src/A.cs\n--- a/src/A.cs\n+++ b/src/A.cs";
        Assert.True(DebuggingCorrelationPolicy.CommitTouchesPath(patch,"src/A.cs"));
        Assert.False(DebuggingCorrelationPolicy.CommitTouchesPath(patch,"src/A.cs.bak"));
    }

    [Fact]
    public void Regression_claim_requires_success_then_touching_commit_then_failure()
    {
        var success=new DateTime(2026,8,23,1,0,0,DateTimeKind.Utc); var commit=success.AddMinutes(5); var failure=commit.AddMinutes(5);
        Assert.True(DebuggingCorrelationPolicy.SupportsRegressionClaim(success,commit,failure,true));
        Assert.False(DebuggingCorrelationPolicy.SupportsRegressionClaim(null,commit,failure,true));
        Assert.False(DebuggingCorrelationPolicy.SupportsRegressionClaim(success,commit,failure,false));
        Assert.False(DebuggingCorrelationPolicy.SupportsRegressionClaim(commit.AddMinutes(1),commit,failure,true));
    }
}
