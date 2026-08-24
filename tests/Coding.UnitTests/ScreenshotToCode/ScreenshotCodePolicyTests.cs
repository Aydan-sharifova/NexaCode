using Coding.Infrastructure.ScreenshotToCode;
using Xunit;

namespace Coding.UnitTests.ScreenshotToCode;

public sealed class ScreenshotCodePolicyTests
{
    [Fact]
    public void ExtractSection_reads_bounded_marker_content()
    {
        var result = ScreenshotCodePolicy.ExtractSection("[[[ANALYSIS]]]\nvisible grid\n[[[APP_TSX]]]\ncode", "ANALYSIS");
        Assert.Equal("visible grid", result);
    }

    [Fact]
    public void ExtractSection_rejects_missing_marker() =>
        Assert.Throws<InvalidOperationException>(() => ScreenshotCodePolicy.ExtractSection("plain prose", "APP_TSX"));

    [Fact]
    public void ValidateGenerated_rejects_remote_preview_resources()
    {
        var app = "export default function App(){ return <main>" + new string('x', 80) + "</main>; }";
        var css = ".page { display: grid; color: white; }";
        var preview = "<html><head></head><body><img src=\"https://tracking.invalid/a.png\">" + new string('x', 80) + "</body></html>";
        Assert.Throws<InvalidOperationException>(() => ScreenshotCodePolicy.ValidateGenerated(app, css, preview));
    }

    [Fact]
    public void SecurePreview_adds_network_denying_content_security_policy()
    {
        var result = ScreenshotCodePolicy.SecurePreview("<html><head><style>body{color:red}</style></head><body></body></html>");
        Assert.Contains("connect-src 'none'", result);
        Assert.Contains("form-action 'none'", result);
    }
}
