using Coding.Application.Features.ScreenshotToCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coding.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:guid}/screenshot-code")]
public sealed class ScreenshotToCodeController(IScreenshotToCodeService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<ScreenshotGenerationDto>> List(Guid projectId, [FromQuery] int take = 20, CancellationToken ct = default) => service.ListAsync(projectId, take, ct);
    [HttpGet("{id:guid}")] public Task<ScreenshotGenerationDto> Get(Guid projectId, Guid id, CancellationToken ct) => service.GetAsync(projectId, id, ct);
    [HttpPost, RequestSizeLimit(5_500_000), EnableRateLimiting("ai")]
    public async Task<ScreenshotGenerationDto> Generate(Guid projectId, [FromForm] ScreenshotGenerationForm form, CancellationToken ct)
    {
        if (form.Image.Length is 0 or > 5 * 1024 * 1024) throw new ArgumentException("The image must be between 1 byte and 5 MB.");
        await using var stream = form.Image.OpenReadStream();
        using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct);
        return await service.GenerateAsync(new(projectId, form.Prompt, form.Image.FileName, form.Image.ContentType, memory.ToArray()), ct);
    }
    [HttpPost("{id:guid}/apply")] public Task<ScreenshotGenerationDto> Apply(Guid projectId, Guid id, ApplyScreenshotGenerationRequest request, CancellationToken ct) => service.ApplyAsync(projectId, id, request.Confirm, ct);
}
public sealed record ScreenshotGenerationForm(string Prompt, IFormFile Image);
public sealed record ApplyScreenshotGenerationRequest(bool Confirm);
