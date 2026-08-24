using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Coding.Application.Features.AiAssistant;
using Coding.Application.Features.AiAgent;
using Coding.Enums;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Coding.Infrastructure.AiAssistant;

public sealed class OpenAiCompatibleProvider(
    HttpClient httpClient,
    IOptions<OpenAiCompatibleOptions> options,
    IAiSecretRedactionService redaction,
    ILogger<OpenAiCompatibleProvider> logger) : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly OpenAiCompatibleOptions _options = options.Value;

    public string ProviderName => "Ollama";
    public string Model => _options.Model;

    public async IAsyncEnumerable<AiStreamChunk> StreamAsync(
        AiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(EnsureTrailingSlash(_options.BaseUrl)), "chat/completions"));

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Content = JsonContent.Create(BuildPayload(request), options: JsonOptions);

        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Local AI provider returned {(int)response.StatusCode}: {redaction.Redact(ReadError(body))}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var completed = false;
        int? inputTokens = null;
        int? outputTokens = null;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null || !line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = line[5..].Trim();
            if (data.Length == 0)
                continue;
            if (data == "[DONE]")
            {
                completed = true;
                break;
            }

            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
                throw new InvalidOperationException(redaction.Redact(ReadError(error.GetRawText())));

            if (root.TryGetProperty("usage", out var usage))
            {
                inputTokens = ReadInt(usage, "prompt_tokens");
                outputTokens = ReadInt(usage, "completion_tokens");
            }

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
                continue;

            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("content", out var content))
            {
                var text = content.GetString();
                if (!string.IsNullOrEmpty(text))
                    yield return new AiStreamChunk(text);
            }

            if (choice.TryGetProperty("finish_reason", out var finishReason) &&
                finishReason.ValueKind == JsonValueKind.String)
                completed = true;
        }

        yield return new AiStreamChunk(
            string.Empty,
            IsCompleted: true,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            FinishReason: completed ? "stop" : "stream_ended");
        logger.LogInformation(
            "AI request completed via {Provider} model {Model} for action {Action} in {ElapsedMs} ms; input tokens {InputTokens}, output tokens {OutputTokens}.",
            ProviderName, request.Images is { Count: > 0 } ? _options.VisionModel : Model, request.Action,
            stopwatch.ElapsedMilliseconds, inputTokens, outputTokens);
    }

    private object BuildPayload(AiRequest request)
    {
        var messages = new List<object>
        {
            new { role = "system", content = redaction.Redact(request.SystemInstructions) }
        };
        messages.AddRange(request.History.Select(history => new
        {
            role = history.Role == AiMessageRole.Assistant ? "assistant" : "user",
            content = redaction.Redact(history.Content)
        }));

        var userInput = new StringBuilder()
            .AppendLine($"Requested action: {request.Action}")
            .AppendLine($"Programming language: {request.ProgrammingLanguage}")
            .AppendLine()
            .AppendLine(redaction.Redact(request.UserInstructions));

        if (!string.IsNullOrWhiteSpace(request.RepositoryContext))
        {
            userInput
                .AppendLine()
                .AppendLine("Repository reference material follows:")
                .AppendLine(redaction.Redact(request.RepositoryContext));
        }

        var images = request.Images ?? [];
        object content;
        if (images.Count == 0)
        {
            content = userInput.ToString();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_options.VisionModel))
                throw new InvalidOperationException(
                    "Image analysis is not configured for the local AI provider. Set OpenAICompatible__VisionModel to a vision-capable model.");

            var parts = new List<object>
            {
                new { type = "text", text = userInput.ToString() }
            };
            parts.AddRange(images.Select(image => new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:{image.MediaType};base64,{image.Base64Content}"
                }
            }));
            content = parts;
        }

        messages.Add(new { role = "user", content });

        var payload = new Dictionary<string, object>
        {
            ["model"] = images.Count > 0 ? _options.VisionModel : _options.Model,
            ["messages"] = messages,
            ["temperature"] = Math.Clamp(_options.Temperature, 0, 2),
            ["max_tokens"] = images.Count > 0
                ? Math.Clamp(request.MaxOutputTokens ?? _options.MaxOutputTokens, 256, 8_192)
                : Math.Clamp(request.MaxOutputTokens ?? _options.MaxOutputTokens, 256, 16_384),
            ["stream"] = true,
            ["stream_options"] = new { include_usage = true }
        };
        if (images.Count > 0)
            payload["reasoning_effort"] = "none";

        return payload;
    }

    private static int? ReadInt(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.TryGetInt32(out var result)
            ? result
            : null;

    private static string ReadError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
                root = error;
            if (root.TryGetProperty("message", out var message))
                return message.GetString() ?? "Request failed.";
        }
        catch (JsonException)
        {
            // Return only a bounded response from non-JSON compatible servers.
        }

        return body.Length <= 500 ? body : body[..500];
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
}
