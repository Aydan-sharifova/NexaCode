using Coding.Application.Security;
using FluentAssertions;
using Xunit;
namespace Coding.UnitTests;
public sealed class SecurityBoundaryTests
{
    [Fact]public void Cookie_origin_requires_same_or_configured_origin(){var allowed=new[]{"https://app.example.com"};RequestOriginPolicy.IsAllowed(null,"https","api.example.com",allowed).Should().BeTrue();RequestOriginPolicy.IsAllowed("https://app.example.com","https","api.example.com",allowed).Should().BeTrue();RequestOriginPolicy.IsAllowed("https://evil.example","https","api.example.com",allowed).Should().BeFalse();RequestOriginPolicy.IsAllowed("javascript:alert(1)","https","api.example.com",allowed).Should().BeFalse();}
    [Fact]public void Image_signature_must_match_declared_media_type(){var png=new byte[]{0x89,0x50,0x4e,0x47,0x0d,0x0a,0x1a,0x0a};ImageUploadPolicy.HasValidSignature(png,"image/png").Should().BeTrue();ImageUploadPolicy.HasValidSignature("not an image"u8,"image/png").Should().BeFalse();ImageUploadPolicy.HasValidSignature(png,"image/jpeg").Should().BeFalse();}
}
