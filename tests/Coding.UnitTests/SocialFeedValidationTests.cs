using Coding.Application.Features.SocialFeed;
using Coding.Enums;
using FluentAssertions;
using Xunit;

namespace Coding.UnitTests;

public sealed class SocialFeedValidationTests
{
    [Fact]
    public async Task Code_posts_require_a_language()
    {
        var command = new CreateSocialPostCommand(PostType.Code, "Console.WriteLine(1);", null, null, null);
        var result = await new CreateSocialPostValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(item => item.PropertyName == nameof(CreateSocialPostCommand.CodeLanguage));
    }

    [Fact]
    public async Task Project_posts_require_a_project()
    {
        var command = new CreateSocialPostCommand(PostType.ProjectShare, "A public project", null, null, null);
        var result = await new CreateSocialPostValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(item => item.PropertyName == nameof(CreateSocialPostCommand.ProjectId));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    public async Task Image_posts_reject_unsafe_urls(string imageUrl)
    {
        var command = new CreateSocialPostCommand(PostType.Image, "Image", null, imageUrl, null);
        var result = await new CreateSocialPostValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Valid_text_post_is_accepted()
    {
        var command = new CreateSocialPostCommand(PostType.Text, "Building a safer cloud IDE.", null, null, null);
        var result = await new CreateSocialPostValidator().ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(PostType.Achievement)]
    [InlineData(PostType.Deployment)]
    public async Task Evidence_post_types_cannot_be_created_manually(PostType type)
    {
        var command=new CreateSocialPostCommand(type,"Unverified claim",null,null,null);
        var result=await new CreateSocialPostValidator().ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x=>x.PropertyName==nameof(CreateSocialPostCommand.Type));
    }
}
