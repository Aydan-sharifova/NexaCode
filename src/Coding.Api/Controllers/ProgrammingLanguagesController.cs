using Coding.Application.Features.Administration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coding.Controllers;

[ApiController, Route("api/programming-languages"), Authorize]
public sealed class ProgrammingLanguagesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProgrammingLanguageItem>> List(CancellationToken ct) =>
        sender.Send(new ListProgrammingLanguagesQuery(), ct);
}
