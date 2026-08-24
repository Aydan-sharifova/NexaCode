import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  usersApi,
  userKeys,
  type PublicUserProfile,
} from "../features/users/api";
import { chatApi } from "../features/chat/api";
import { ErrorState, LoadingState } from "../components/AsyncState";
import { useToast } from "../contexts/ToastContext";
import { achievementsApi } from "../features/achievements/api";
import "./PublicUserPortfolio.css";
import { moderationApi } from "../features/moderation/api";

export function PublicUserProfilePage() {
  const { publicId = "" } = useParams();
  const identifier = decodeURIComponent(publicId).trim().replace(/^@/, "");
  const isValidIdentifier =
    Boolean(identifier) && identifier !== "undefined" && identifier !== "null";
  const navigate = useNavigate();
  const { show } = useToast();
  const client = useQueryClient();
  const profile = useQuery({
    queryKey: userKeys.profile(identifier),
    queryFn: () => usersApi.profile(identifier),
    enabled: isValidIdentifier,
  });
  const projects = useQuery({
    queryKey: userKeys.publicProjects(identifier),
    queryFn: () => usersApi.publicProjects(identifier),
    enabled: isValidIdentifier,
  });
  const achievements = useQuery({
    queryKey: ["achievements", "user", identifier],
    queryFn: () => achievementsApi.user(identifier),
    enabled: isValidIdentifier,
  });
  const portfolio=useQuery({queryKey:userKeys.portfolio(identifier),queryFn:()=>usersApi.portfolio(identifier),enabled:isValidIdentifier});
  const journey=useQuery({queryKey:["developer-journey",identifier],queryFn:()=>achievementsApi.journey(identifier),enabled:isValidIdentifier});
  const message = useMutation({
    mutationFn: () => chatApi.direct(profile.data?.publicId ?? identifier),
    onSuccess: async (conversation) => {
      await client.invalidateQueries({ queryKey: ["chat-conversations"] });
      navigate(`/chat?conversation=${conversation.id}`);
    },
    onError: (error) => show(error.message, "error"),
  });
  const follow = useMutation({
    mutationFn: (currentlyFollowing: boolean) =>
      currentlyFollowing
        ? usersApi.unfollow(profile.data?.publicId ?? identifier)
        : usersApi.follow(profile.data?.publicId ?? identifier),
    onSuccess: (state) => {
      client.setQueryData(
        userKeys.profile(identifier),
        (current: PublicUserProfile | undefined) =>
          current ? { ...current, ...state } : current,
      );
      const canonicalId = profile.data?.publicId;
      if (canonicalId && identifier !== canonicalId)
        client.setQueryData(
          userKeys.profile(canonicalId),
          (current: PublicUserProfile | undefined) =>
            current ? { ...current, ...state } : current,
        );
    },
    onError: (error) => show(error.message, "error"),
  });
  const block = useMutation({
    mutationFn: (currentlyBlocked: boolean) =>
      currentlyBlocked
        ? usersApi.unblock(profile.data?.publicId ?? identifier)
        : usersApi.block(profile.data?.publicId ?? identifier),
    onSuccess: (state) => {
      client.setQueryData(
        userKeys.profile(identifier),
        (current: PublicUserProfile | undefined) =>
          current
            ? {
                ...current,
                isBlockedByMe: state.isBlocked,
                isFollowing: state.isBlocked ? false : current.isFollowing,
              }
            : current,
      );
      void client.invalidateQueries({ queryKey: ["social-feed"] });
      void client.invalidateQueries({ queryKey: ["team-directory"] });
      show(
        state.isBlocked
          ? "User blocked. Their posts and messages are now filtered."
          : "User unblocked.",
      );
    },
    onError: (error) => show(error.message, "error"),
  });
  if (!isValidIdentifier)
    return (
      <main className="public-profile-page">
        <ErrorState
          message="This profile link is invalid. Select the user again from Chat or Team."
          retry={() => navigate("/team")}
        />
      </main>
    );
  if (profile.isPending)
    return (
      <main className="public-profile-page">
        <LoadingState label="Loading profile…" />
      </main>
    );
  if (profile.isError)
    return (
      <main className="public-profile-page">
        <ErrorState
          message={profile.error.message}
          retry={() => profile.refetch()}
        />
      </main>
    );
  const user = profile.data;
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(user.publicId);
      show("Public ID copied.");
    } catch {
      show("Unable to copy the public ID.", "error");
    }
  };
  return (
    <main className="public-profile-page">
      <section className="public-profile-hero">
        <div className="public-profile-avatar" aria-hidden="true">
          {user.avatarUrl ? (
            <img src={user.avatarUrl} alt="" />
          ) : (
            user.displayName.slice(0, 1).toUpperCase()
          )}
        </div>

        <div className="public-profile-details">
          <p className="public-profile-eyebrow">Public profile</p>
          <div className="public-profile-title">
            <h1>{user.displayName}</h1>
            <span>@{user.userName}</span>
          </div>
          <button
            className="public-id-badge"
            type="button"
            onClick={() => void copy()}
            title="Copy public ID"
          >
            <span>@{user.publicId}</span>
            <small>Copy ID</small>
          </button>
          <p className="public-profile-bio">
            {user.bio || "No biography added yet."}
          </p>
          {user.headline && (
            <p className="public-profile-headline">{user.headline}</p>
          )}
          {(user.primaryRole||user.experienceLevel||user.location)&&<div className="public-profile-career">{user.primaryRole&&<b>{user.primaryRole}</b>}{user.experienceLevel&&<span>{user.experienceLevel}</span>}{user.location&&<span>{user.location}</span>}</div>}
          <div className="public-profile-links">{user.websiteUrl&&<a href={user.websiteUrl} target="_blank" rel="noreferrer">Website</a>}{user.gitHubUrl&&<a href={user.gitHubUrl} target="_blank" rel="noreferrer">GitHub</a>}{user.linkedInUrl&&<a href={user.linkedInUrl} target="_blank" rel="noreferrer">LinkedIn</a>}{user.portfolioUrl&&<a href={user.portfolioUrl} target="_blank" rel="noreferrer">Portfolio</a>}</div>
          <div className="public-profile-meta">
            <span>
              <strong>{user.publicProjectCount}</strong> public{" "}
              {user.publicProjectCount === 1 ? "project" : "projects"}
            </span>
            {user.areFollowersPublic && (
              <>
                <span>
                  <strong>{user.followerCount ?? 0}</strong>{" "}
                  {user.followerCount === 1 ? "follower" : "followers"}
                </span>
                <span>
                  <strong>{user.followingCount ?? 0}</strong> following
                </span>
              </>
            )}
            <span>Joined {new Date(user.joinedAt).toLocaleDateString()}</span>
          </div>
        </div>

        <div className="public-profile-actions">
          {user.isOwnProfile ? (
            <Link className="ui-button primary" to="/settings?section=profile">
              Edit profile
            </Link>
          ) : (
            <>
              <button
                className={`ui-button ${user.isFollowing ? "secondary" : "primary"}`}
                disabled={follow.isPending || user.isBlockedByMe}
                onClick={() => follow.mutate(user.isFollowing)}
              >
                {follow.isPending
                  ? "Updating…"
                  : user.isFollowing
                    ? "Following"
                    : "Follow"}
              </button>
              <button
                className="ui-button secondary"
                disabled={message.isPending || user.isBlockedByMe}
                onClick={() => message.mutate()}
              >
                {message.isPending ? "Opening…" : "Message user"}
              </button>
              <button
                className="ui-button danger"
                disabled={block.isPending}
                onClick={() => {
                  if (
                    user.isBlockedByMe ||
                    window.confirm(
                      `Block ${user.displayName}? Following and direct messaging will be disabled.`,
                    )
                  )
                    block.mutate(user.isBlockedByMe);
                }}
              >
                {block.isPending
                  ? "Updating…"
                  : user.isBlockedByMe
                    ? "Unblock"
                    : "Block"}
              </button>
              <button className="ui-button secondary" onClick={() => { const reason = window.prompt("Report reason: Spam, Harassment, Hate or abuse, Dangerous content, Privacy, Copyright, Impersonation, Other", "Impersonation")?.trim(); if (reason) void moderationApi.report("Profile", user.id, reason).then(() => show("Profile report submitted.")).catch(error => show(error.message, "error")); }}>Report</button>
            </>
          )}
        </div>
      </section>

      {user.skills.length > 0 && (
        <section className="public-profile-skills">
          <header><p>Declared expertise</p><h2>Skills</h2></header>
          <div>{user.skills.map((skill) => <span key={skill}>{skill}</span>)}</div>
        </section>
      )}

      {achievements.data && (
        <section className="public-achievements">
          <header>
            <div>
              <p>Verified growth</p>
              <h2>Achievements</h2>
            </div>
            <span>
              {achievements.data.reputationScore} reputation ·{" "}
              {achievements.data.contributionLevel}
            </span>
          </header>
          <div>
            {achievements.data.achievements
              .filter((item) => item.unlocked)
              .slice(0, 6)
              .map((item) => (
                <article key={item.id}>
                  <b>✓</b>
                  <div>
                    <h3>{item.title}</h3>
                    <p>{item.description}</p>
                  </div>
                  <small>{item.points} pts</small>
                </article>
              ))}
            {achievements.data.unlockedCount === 0 && (
              <p>No verified achievements yet.</p>
            )}
          </div>
        </section>
      )}

      {portfolio.data?.contributions&&<section className="portfolio-contributions"><header><p>VERIFIED CONTRIBUTION DATA</p><h2>Developer impact</h2></header><div>{Object.entries(portfolio.data.contributions).map(([label,value])=><article key={label}><strong>{value}</strong><span>{label.replace(/([A-Z])/g," $1")}</span></article>)}</div></section>}

      {portfolio.data&&!portfolio.data.activityVisible&&<section className="portfolio-private">This developer keeps detailed activity private.</section>}
      {portfolio.data?.activityVisible&&<section className="portfolio-content-grid"><div><header><p>PUBLIC WRITING</p><h2>Posts</h2></header>{portfolio.data.posts.length?portfolio.data.posts.map(post=><article className="portfolio-post" key={post.id}><b>{post.type}</b>{post.type==="Code"?<pre><code>{post.content}</code></pre>:<p>{post.content}</p>}<small>{new Date(post.createdAt).toLocaleDateString()} · {post.likes} likes · {post.saves} saves</small></article>):<p>No public posts yet.</p>}</div><div><header><p>REUSABLE CODE</p><h2>Snippets</h2></header>{portfolio.data.snippets.length?portfolio.data.snippets.map(post=><article className="portfolio-post" key={post.id}><b>{post.codeLanguage||"Code"}</b><pre><code>{post.content}</code></pre><small>{post.saves} saves · {post.comments} comments</small></article>):<p>No public snippets yet.</p>}</div></section>}

      {journey.data&&<section className="public-journey"><header><p>DEVELOPER JOURNEY</p><h2>Verified growth timeline</h2></header>{journey.data.length?<div className="journey-line">{journey.data.map(item=><article key={`${item.code}-${item.occurredAt}`}><time>{new Date(item.occurredAt).toLocaleDateString()}</time><span/><div><h3>{item.title}</h3><p>{item.description}</p></div></article>)}</div>:<p>No verified milestones yet.</p>}</section>}

      {portfolio.data?.activityVisible&&<section className="portfolio-activity"><header><p>PUBLIC ACTIVITY</p><h2>Recent contribution trail</h2></header>{portfolio.data.activity.length?<div>{portfolio.data.activity.map((item,index)=><article key={`${item.type}-${item.evidenceId??index}-${item.occurredAt}`}><time>{new Date(item.occurredAt).toLocaleDateString()}</time><b>{item.title}</b><p>{item.description}</p><span>{item.type}</span></article>)}</div>:<p>No public contribution activity yet.</p>}</section>}

      {portfolio.data&&user.areFollowersPublic&&<section className="portfolio-network"><div><h2>Followers</h2>{portfolio.data.followers.length?portfolio.data.followers.map(person=><Link to={`/users/${person.publicId}`} key={person.publicId}>@{person.userName}<small>{person.displayName}</small></Link>):<p>No followers yet.</p>}</div><div><h2>Following</h2>{portfolio.data.following.length?portfolio.data.following.map(person=><Link to={`/users/${person.publicId}`} key={person.publicId}>@{person.userName}<small>{person.displayName}</small></Link>):<p>Not following anyone yet.</p>}</div></section>}

      <section className="public-projects">
        <header className="public-projects-header">
          <div>
            <p>Repositories</p>
            <h2>Public projects</h2>
          </div>
          <span>{user.publicProjectCount}</span>
        </header>

        {projects.isPending ? (
          <LoadingState label="Loading public projects…" />
        ) : projects.isError ? (
          <ErrorState
            message={projects.error.message}
            retry={() => projects.refetch()}
          />
        ) : projects.data?.items.length ? (
          <div className="public-project-grid">
            {projects.data.items.map((project) => (
              <Link
                to={`/public/projects/${project.id}`}
                className="public-project-card"
                key={project.id}
              >
                <div className="public-project-card-head">
                  <span className="public-project-icon" aria-hidden="true">
                    {"</>"}
                  </span>
                  <span className="public-language-badge">
                    {project.defaultLanguage || "Other"}
                  </span>
                </div>
                <h3>{project.name}</h3>
                <p>{project.description || "No description provided."}</p>
                <small>
                  Updated {new Date(project.updatedAt).toLocaleDateString()}
                </small>
              </Link>
            ))}
          </div>
        ) : (
          <div className="public-project-empty">
            <span aria-hidden="true">{"</>"}</span>
            <h3>No public projects yet</h3>
            <p>This user's public projects will appear here.</p>
          </div>
        )}
      </section>
    </main>
  );
}
