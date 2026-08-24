using Coding.Domain.Services;
using Coding.Enums;
using FluentValidation;
using MediatR;

namespace Coding.Application.Features.ProjectPlanner;

public sealed record ProjectPlanSummary(Guid Id, string Title, string Summary, string DefaultLanguage, ProjectPlanStatus Status, int Version, DateTime CreatedAt, Guid? CreatedProjectId);
public sealed record ProjectPlanDetails(Guid Id, string Idea, ProjectPlanBlueprint Plan, ProjectPlanStatus Status, int Version, string Provider, string Model, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ApprovedAt, DateTime? AppliedAt, Guid? CreatedProjectId);
public sealed record GenerateProjectPlanCommand(string Idea) : IRequest<ProjectPlanDetails>;
public sealed record ApproveProjectPlanCommand(Guid PlanId, int ExpectedVersion) : IRequest<ProjectPlanDetails>;
public sealed record RejectProjectPlanCommand(Guid PlanId, int ExpectedVersion) : IRequest<ProjectPlanDetails>;
public sealed record ApplyProjectPlanCommand(Guid PlanId, int ExpectedVersion, bool ConfirmBulkCreation) : IRequest<Guid>;
public sealed record GetProjectPlanQuery(Guid PlanId) : IRequest<ProjectPlanDetails>;
public sealed record ListProjectPlansQuery : IRequest<IReadOnlyList<ProjectPlanSummary>>;

public sealed class GenerateProjectPlanValidator : AbstractValidator<GenerateProjectPlanCommand>
{
    public GenerateProjectPlanValidator() => RuleFor(x => x.Idea).NotEmpty().MinimumLength(20).MaximumLength(2000);
}
public sealed class PlanVersionValidator : AbstractValidator<ApproveProjectPlanCommand>
{
    public PlanVersionValidator() { RuleFor(x => x.PlanId).NotEmpty(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
public sealed class RejectPlanVersionValidator : AbstractValidator<RejectProjectPlanCommand>
{
    public RejectPlanVersionValidator() { RuleFor(x => x.PlanId).NotEmpty(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}
public sealed class ApplyProjectPlanValidator : AbstractValidator<ApplyProjectPlanCommand>
{
    public ApplyProjectPlanValidator() { RuleFor(x => x.PlanId).NotEmpty(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); RuleFor(x => x.ConfirmBulkCreation).Equal(true).WithMessage("Explicit bulk creation confirmation is required."); }
}

public interface IProjectPlannerService
{
    Task<ProjectPlanDetails> GenerateAsync(string idea, CancellationToken cancellationToken);
    Task<ProjectPlanDetails> GetAsync(Guid planId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectPlanSummary>> ListAsync(CancellationToken cancellationToken);
    Task<ProjectPlanDetails> ApproveAsync(Guid planId, int expectedVersion, CancellationToken cancellationToken);
    Task<ProjectPlanDetails> RejectAsync(Guid planId, int expectedVersion, CancellationToken cancellationToken);
    Task<Guid> ApplyAsync(Guid planId, int expectedVersion, bool confirm, CancellationToken cancellationToken);
}
