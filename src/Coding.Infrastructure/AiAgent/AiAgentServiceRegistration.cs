using Coding.Application.Features.AiAgent;
using Coding.Infrastructure.AiAgent;
using Coding.Infrastructure.AiAgent.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Coding.Infrastructure.AiAgent;

/// <summary>
/// DI registration for the AI agent feature. Extracted from
/// <see cref="DependencyInjection"/> so the agent layer stays self-contained.
/// </summary>
public static class AiAgentServiceRegistration
{
    public static IServiceCollection AddAiAgentServices(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IAiSecretRedactionService, AiSecretRedactionService>();
        services.AddScoped<IAiToolApprovalPolicy, AiToolApprovalPolicy>();
        services.AddScoped<IAiToolAuthorizationService, AiToolAuthorizationService>();

        // Tools. Each tool is scoped so its DbContext dependencies are scoped.
        services.AddScoped<IAiTool, GetProjectTreeTool>();
        services.AddScoped<IAiTool, ReadFileTool>();
        services.AddScoped<IAiTool, ReadFileRangeTool>();
        services.AddScoped<IAiTool, SearchCodeTool>();
        services.AddScoped<IAiTool, GetFileVersionsTool>();
        services.AddScoped<IAiTool, GetProjectMembersTool>();
        services.AddScoped<IAiTool, GetDatabaseSchemaTool>();
        services.AddScoped<IAiTool, GetRecentActivityTool>();
        services.AddScoped<IAiTool, GetExecutionResultTool>();

        services.AddScoped<IAiToolDescriptorSource, TypeAiToolDescriptorSource>();
        services.AddScoped<IAiToolRegistry, AiToolRegistry>();
        services.AddScoped<IAiToolExecutionService, AiToolExecutionService>();
        services.AddScoped<IAiApprovalService, AiApprovalService>();

        return services;
    }
}
