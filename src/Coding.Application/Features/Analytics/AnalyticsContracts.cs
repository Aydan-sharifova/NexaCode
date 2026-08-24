using FluentValidation;
using MediatR;

namespace Coding.Application.Features.Analytics;

public sealed record AnalyticsFilter(DateTime? From = null, DateTime? To = null, Guid? ProjectId = null);
public sealed record AnalyticsSummaryDto(int ActiveUsers, int ProjectsCreated, decimal TaskCompletionRate, int FileChanges, decimal EstimatedCodingHours);
public sealed record ActiveUserDto(Guid UserId, string DisplayName, string UserName, string? AvatarUrl, int ActivityCount);
public sealed record TimeSeriesPointDto(DateTime Period, int Value);
public sealed record LanguageUsageDto(string Language, int ProjectCount);
public sealed record DeveloperAnalyticsDto(int Commits,int PullRequests,int Reviews,int Deployments,int Projects,int Contributions,int Followers,int Posts,int Snippets);
public sealed record ProjectAnalyticsDto(Guid ProjectId,string Name,bool IsPublic,int Views,int Forks,bool ForkingAvailable,int Likes,int Saves,int Contributors,int Deployments,int Activity);
public sealed record AnalyticsDashboardDto(
    DateTime From,
    DateTime To,
    AnalyticsSummaryDto Summary,
    IReadOnlyList<ActiveUserDto> ActiveUsers,
    IReadOnlyList<TimeSeriesPointDto> ProjectsOverTime,
    IReadOnlyList<LanguageUsageDto> Languages,
    IReadOnlyList<TimeSeriesPointDto> WeeklyActivity,
    IReadOnlyList<TimeSeriesPointDto> MonthlyActivity,
    DeveloperAnalyticsDto Developer,
    IReadOnlyList<ProjectAnalyticsDto> Projects);

public sealed record GetAnalyticsDashboardQuery(DateTime? From = null, DateTime? To = null, Guid? ProjectId = null)
    : IRequest<AnalyticsDashboardDto>;

public sealed record StartCodingSessionCommand(Guid ProjectId, Guid FileId) : IRequest<Guid>;
public sealed record HeartbeatCodingSessionCommand(Guid SessionId) : IRequest;
public sealed record EndCodingSessionCommand(Guid SessionId) : IRequest;

public sealed class GetAnalyticsDashboardQueryValidator : AbstractValidator<GetAnalyticsDashboardQuery>
{
    public GetAnalyticsDashboardQueryValidator()
    {
        RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("The start date must be before the end date.");
    }
}

public sealed class StartCodingSessionCommandValidator : AbstractValidator<StartCodingSessionCommand>
{
    public StartCodingSessionCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.FileId).NotEmpty();
    }
}
