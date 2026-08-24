using Coding.Enums;
using FluentValidation;
using MediatR;

namespace Coding.Application.Features.Moderation;

public sealed record ModerationUser(Guid Id, string PublicId, string UserName, string FullName);
public sealed record ModerationActionItem(Guid Id, ModerationUser Moderator, ModerationActionType Action, ModerationReportState PreviousState, ModerationReportState NewState, string Note, DateTime CreatedAt);
public sealed record ContentReportItem(Guid Id, ModerationUser Reporter, ReportTargetType TargetType, Guid TargetId, string TargetLabel, string Reason, string? Details, ModerationReportState State, ModerationUser? AssignedModerator, DateTime CreatedAt, DateTime? ReviewedAt, IReadOnlyList<ModerationActionItem> Actions);
public sealed record ModerationQueue(IReadOnlyList<ContentReportItem> Items, int Total, int Page, int PageSize);

public sealed record CreateContentReportCommand(ReportTargetType TargetType, Guid TargetId, string Reason, string? Details) : IRequest<ContentReportItem>;
public sealed record GetMyContentReportsQuery(int Page = 1, int PageSize = 30) : IRequest<ModerationQueue>;
public sealed record GetModerationQueueQuery(ModerationReportState? State, ReportTargetType? TargetType, int Page = 1, int PageSize = 30) : IRequest<ModerationQueue>;
public sealed record ModerateContentReportCommand(Guid ReportId, ModerationActionType Action, string Note) : IRequest<ContentReportItem>;

public sealed class CreateContentReportValidator : AbstractValidator<CreateContentReportCommand>
{
    private static readonly string[] Reasons = ["Spam", "Harassment", "Hate or abuse", "Dangerous content", "Privacy", "Copyright", "Impersonation", "Other"];
    public CreateContentReportValidator() { RuleFor(x => x.Reason).Must(Reasons.Contains).WithMessage("Unsupported report reason."); RuleFor(x => x.Details).MaximumLength(4000); RuleFor(x => x).Must(x => x.Reason != "Other" || !string.IsNullOrWhiteSpace(x.Details)).WithMessage("Details are required for Other reports."); }
}

public sealed class ModerateContentReportValidator : AbstractValidator<ModerateContentReportCommand>
{
    public ModerateContentReportValidator() { RuleFor(x => x.Note).NotEmpty().MaximumLength(4000); }
}
