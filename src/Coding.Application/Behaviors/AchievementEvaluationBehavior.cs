using Coding.Application.Abstractions;
using Coding.Application.Features.Achievements;
using Coding.Application.Features.Projects;
using Coding.Application.Features.PullRequests;
using Coding.Application.Features.Repositories;
using Coding.Application.Features.SocialFeed;
using MediatR;

namespace Coding.Application.Behaviors;

public sealed class AchievementEvaluationBehavior<TRequest, TResponse>(IAchievementEvaluator evaluator, ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next();
        if (request is CreateProjectCommand or CommitRepositoryChangesCommand or CreatePullRequestCommand or MergePullRequestCommand or ReviewPullRequestCommand or CreateSocialPostCommand or AddSocialCommentCommand)
            await evaluator.EvaluateAsync(currentUser.UserId, ct);
        return response;
    }
}
