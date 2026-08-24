using Coding.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Coding.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Role> Roles => Set<Role>();

        public DbSet<UserRole> UserRoles => Set<UserRole>();

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<AccountToken> AccountTokens => Set<AccountToken>();

        public DbSet<Workspace> Workspaces => Set<Workspace>();

        public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProgrammingLanguage> ProgrammingLanguages => Set<ProgrammingLanguage>();

        public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

        public DbSet<ProjectInvitation> ProjectInvitations => Set<ProjectInvitation>();

        public DbSet<WorkspaceNode> WorkspaceNodes => Set<WorkspaceNode>();
        public DbSet<FileContent> FileContents => Set<FileContent>();
        public DbSet<FileVersion> FileVersions => Set<FileVersion>();
        public DbSet<CollaborativeDocumentSnapshot> CollaborativeDocumentSnapshots => Set<CollaborativeDocumentSnapshot>();
        public DbSet<CollaborativeDocumentUpdate> CollaborativeDocumentUpdates => Set<CollaborativeDocumentUpdate>();

        public DbSet<Folder> Folders => Set<Folder>();

        public DbSet<FileItem> FileItems => Set<FileItem>();

        public DbSet<CodeHistory> CodeHistories => Set<CodeHistory>();

        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
        public DbSet<MessageReadReceipt> MessageReadReceipts => Set<MessageReadReceipt>();
        public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
        public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
        public DbSet<TaskComment> TaskComments => Set<TaskComment>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<CodingSession> CodingSessions => Set<CodingSession>();
        public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<UserBan> UserBans => Set<UserBan>();
        public DbSet<DeveloperProfile> DeveloperProfiles => Set<DeveloperProfile>();
        public DbSet<UserFollow> UserFollows => Set<UserFollow>();
        public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
        public DbSet<SocialPost> SocialPosts => Set<SocialPost>();
        public DbSet<SocialPostComment> SocialPostComments => Set<SocialPostComment>();
        public DbSet<SocialPostReaction> SocialPostReactions => Set<SocialPostReaction>();
        public DbSet<SavedSocialPost> SavedSocialPosts => Set<SavedSocialPost>();
        public DbSet<SocialPostShare> SocialPostShares => Set<SocialPostShare>();
        public DbSet<PullRequest> PullRequests => Set<PullRequest>();
        public DbSet<PullRequestReview> PullRequestReviews => Set<PullRequestReview>();
        public DbSet<PullRequestComment> PullRequestComments => Set<PullRequestComment>();
        public DbSet<MarketplaceItem> MarketplaceItems => Set<MarketplaceItem>();
        public DbSet<MarketplaceItemVersion> MarketplaceItemVersions => Set<MarketplaceItemVersion>();
        public DbSet<MarketplaceInstallation> MarketplaceInstallations => Set<MarketplaceInstallation>();
        public DbSet<MarketplaceLike> MarketplaceLikes => Set<MarketplaceLike>();
        public DbSet<SavedMarketplaceItem> SavedMarketplaceItems => Set<SavedMarketplaceItem>();
        public DbSet<SavedProject> SavedProjects => Set<SavedProject>();
        public DbSet<ProjectView> ProjectViews => Set<ProjectView>();
        public DbSet<LiveCodingRoom> LiveCodingRooms => Set<LiveCodingRoom>();
        public DbSet<RoomParticipant> RoomParticipants => Set<RoomParticipant>();
        public DbSet<RoomMessage> RoomMessages => Set<RoomMessage>();
        public DbSet<RoomTask> RoomTasks => Set<RoomTask>();
        public DbSet<RoomReaction> RoomReactions => Set<RoomReaction>();
        public DbSet<RoomInterviewerNote> RoomInterviewerNotes => Set<RoomInterviewerNote>();
        public DbSet<ContentReport> ContentReports => Set<ContentReport>();
        public DbSet<ModerationActionRecord> ModerationActionRecords => Set<ModerationActionRecord>();
        public DbSet<Achievement> Achievements => Set<Achievement>();
        public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
        public DbSet<ProjectPlan> ProjectPlans => Set<ProjectPlan>();
        public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
        public DbSet<ProjectIssue> ProjectIssues => Set<ProjectIssue>();
        public DbSet<KnowledgeGraphSnapshot> KnowledgeGraphSnapshots => Set<KnowledgeGraphSnapshot>();
        public DbSet<KnowledgeGraphNode> KnowledgeGraphNodes => Set<KnowledgeGraphNode>();
        public DbSet<KnowledgeGraphEdge> KnowledgeGraphEdges => Set<KnowledgeGraphEdge>();
        public DbSet<DebuggingIncident> DebuggingIncidents => Set<DebuggingIncident>();
        public DbSet<DebuggingEvidence> DebuggingEvidence => Set<DebuggingEvidence>();
        public DbSet<DebuggingExecutionObservation> DebuggingExecutionObservations => Set<DebuggingExecutionObservation>();
        public DbSet<AutonomousTestRun> AutonomousTestRuns => Set<AutonomousTestRun>();
        public DbSet<AutonomousTestIteration> AutonomousTestIterations => Set<AutonomousTestIteration>();
        public DbSet<ScreenshotCodeGeneration> ScreenshotCodeGenerations => Set<ScreenshotCodeGeneration>();
        public DbSet<AiUiGeneration> AiUiGenerations => Set<AiUiGeneration>();
        public DbSet<ProjectDeployment> ProjectDeployments => Set<ProjectDeployment>();
        public DbSet<ProjectDeploymentFile> ProjectDeploymentFiles => Set<ProjectDeploymentFile>();

        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();

        public DbSet<Invitation> Invitations => Set<Invitation>();

        public DbSet<GitCommit> GitCommits => Set<GitCommit>();

        public DbSet<AIRequest> AIRequests => Set<AIRequest>();

        public DbSet<AIResponse> AIResponses => Set<AIResponse>();
        public DbSet<AiConversation> AiConversations => Set<AiConversation>();
        public DbSet<AiMessage> AiMessages => Set<AiMessage>();
        public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();

        public DbSet<AiAgentRun> AiAgentRuns => Set<AiAgentRun>();
        public DbSet<AiAgentStep> AiAgentSteps => Set<AiAgentStep>();
        public DbSet<AiToolCall> AiToolCalls => Set<AiToolCall>();
        public DbSet<AiApprovalRequest> AiApprovalRequests => Set<AiApprovalRequest>();
        public DbSet<AiPatch> AiPatches => Set<AiPatch>();
        public DbSet<AiReviewFinding> AiReviewFindings => Set<AiReviewFinding>();


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.HasPostgresExtension("citext");

            builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
