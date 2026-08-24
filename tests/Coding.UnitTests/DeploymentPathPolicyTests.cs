using Coding.Application.Features.Deployments;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class DeploymentPathPolicyTests
{
    [Theory]
    [InlineData(null, "index.html")]
    [InlineData("assets/app.js", "assets/app.js")]
    [InlineData("/styles/site.css", "styles/site.css")]
    public void Normalizes_safe_asset_paths(string? input, string expected) => DeploymentPathPolicy.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData("../secret")]
    [InlineData("assets/../../secret")]
    [InlineData("assets//app.js")]
    [InlineData("assets/./app.js")]
    [InlineData("%2e%2e/secret")]
    public void Rejects_traversal_and_ambiguous_paths(string input) => DeploymentPathPolicy.Normalize(input).Should().BeNull();
}
