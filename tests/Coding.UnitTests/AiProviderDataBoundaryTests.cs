using System.Net;
using System.Text;
using Coding.Application.Features.AiAssistant;
using Coding.Enums;
using Coding.Infrastructure.AiAgent;
using Coding.Infrastructure.AiAssistant;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Coding.UnitTests;

public sealed class AiProviderDataBoundaryTests
{
    [Fact]
    public async Task Provider_redacts_every_text_channel_before_transmission()
    {
        var handler = new CaptureHandler();
        var provider = new OpenAiCompatibleProvider(new HttpClient(handler), Options.Create(new OpenAiCompatibleOptions
        {
            BaseUrl = "http://localhost:11434/v1/", Model = "test-model", ApiKey = "ollama"
        }), new AiSecretRedactionService(), NullLogger<OpenAiCompatibleProvider>.Instance);
        var secret = "sk-abcdefghijklmnopqrstuvwxyz0123456789";
        var request = new AiRequest($"system {secret}", $"user {secret}", $"repo {secret}", "csharp",
            AiAssistantAction.Explain, [new(AiMessageRole.User, $"history {secret}")]);

        await foreach (var _ in provider.StreamAsync(request, CancellationToken.None)) { }

        handler.Body.Should().NotContain(secret);
        handler.Body.Should().Contain("[REDACTED]");
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
            };
        }
    }
}
