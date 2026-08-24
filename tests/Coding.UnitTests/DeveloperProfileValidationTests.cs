using Coding.Application.Features.Users;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class DeveloperProfileValidationTests
{
    [Fact]
    public async Task Rejects_non_http_profile_links()
    {
        var validator = new UpdateDeveloperProfileValidator();
        var command = Valid() with { WebsiteUrl = "javascript:alert(1)" };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(item => item.ErrorMessage.Contains("HTTP or HTTPS"));
    }

    [Fact]
    public async Task Accepts_complete_public_profile()
    {
        var validator = new UpdateDeveloperProfileValidator();

        var result = await validator.ValidateAsync(Valid());

        result.IsValid.Should().BeTrue();
    }

    private static UpdateDeveloperProfileCommand Valid() => new(
        "Aydan Sharifova",
        "Full-stack developer",
        "Building developer tools",
        "Baku",
        "https://example.com",
        "https://github.com/example",
        "https://linkedin.com/in/example",
        "https://portfolio.example.com",
        "Full-stack developer",
        "Senior",
        ["C#", "React"],
        ["Distributed systems"],
        true,
        true,
        true);
}
