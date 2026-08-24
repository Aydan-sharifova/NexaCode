import { useMemo, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { useToast } from "../contexts/ToastContext";
import { useProjects } from "../features/projects/hooks";
import type { FeedPost, FeedTab, PostType } from "../features/social-feed/api";
import { useAddComment, useComments, useCreateFeedPost, useDeleteComment, useDeleteFeedPost, useFeed, useSavedFeed, useSharePost, useSocialDiscover, useToggleLike, useToggleSave } from "../features/social-feed/hooks";
import { moderationApi, type ReportTargetType } from "../features/moderation/api";
import { deduplicateById } from "../features/social-feed/deduplicate";

const tabs: Array<{ value: FeedTab | "Saved"; label: string }> = [{ value: "ForYou", label: "For you" }, { value: "Following", label: "Following" }, { value: "Trending", label: "Trending" }, { value: "Saved", label: "Saved" }];
const postTypes: Array<{ value: PostType; label: string }> = [{ value: "Text", label: "Text" }, { value: "Code", label: "Code snippet" }, { value: "Image", label: "Image" }, { value: "ProjectShare", label: "Project" }, { value: "Learning", label: "Learning" }];
const reportReasons = ["Spam", "Harassment", "Hate or abuse", "Dangerous content", "Privacy", "Copyright", "Impersonation", "Other"];
async function reportContent(type: ReportTargetType, id: string) { const reason = window.prompt(`Report reason:\n${reportReasons.join(", ")}`, "Spam")?.trim(); if (!reason) return false; if (!reportReasons.includes(reason)) throw new Error("Choose one of the listed report reasons."); const details = window.prompt("Optional details (required for Other)")?.trim(); await moderationApi.report(type, id, reason, details || undefined); return true; }

function PostComments({ post }: { post: FeedPost }) {
  const [text, setText] = useState(""); const [replyTo,setReplyTo]=useState<{id:string;name:string}>();
  const comments = useComments(post.id, true); const add = useAddComment(post.id); const remove = useDeleteComment(post.id); const { show } = useToast();
  const commentItems = useMemo(() => deduplicateById(comments.data?.pages.flatMap(page => page.items) ?? []), [comments.data]);
  const submit = async (event: FormEvent) => { event.preventDefault(); if (!text.trim()) return; await add.mutateAsync({content:text.trim(),parentCommentId:replyTo?.id}); setText("");setReplyTo(undefined); };
  return <section className="feed-comments" aria-label={`Comments on ${post.author.displayName}'s post`}>
    <form onSubmit={event => void submit(event)}>{replyTo&&<small>Replying to {replyTo.name} <button type="button" onClick={()=>setReplyTo(undefined)}>Cancel</button></small>}<input value={text} maxLength={2000} onChange={event => setText(event.target.value)} placeholder={replyTo?`Reply to ${replyTo.name}…`:"Write a thoughtful comment…"} aria-label="Comment" /><button disabled={!text.trim() || add.isPending}>{add.isPending ? "Posting…" : replyTo?"Reply":"Post"}</button></form>
    {comments.isPending ? <LoadingState label="Loading comments…" /> : comments.isError ? <ErrorState message={comments.error.message} retry={() => void comments.refetch()} /> : commentItems.map(comment => <article className={comment.parentCommentId?"feed-reply":""} key={comment.id}>
      <Link to={`/users/${comment.author.publicId}`}>{comment.author.displayName}</Link><p>{comment.content}</p><small>{new Date(comment.createdAt).toLocaleString()}</small><div className="feed-comment-actions"><button onClick={()=>setReplyTo({id:comment.id,name:comment.author.displayName})}>Reply</button>{!comment.isOwner && <button onClick={() => void reportContent("Comment", comment.id).then(sent => sent && show("Report submitted.")).catch(error => show(error.message, "error"))}>Report</button>}{comment.isOwner && <button className="text-danger" onClick={() => remove.mutate(comment.id)}>Delete</button>}</div>
    </article>)}
    {comments.hasNextPage && <button className="feed-more" disabled={comments.isFetchingNextPage} onClick={() => void comments.fetchNextPage()}>More comments</button>}
  </section>;
}

function FeedCard({ post }: { post: FeedPost }) {
  const [commentsOpen, setCommentsOpen] = useState(false); const like = useToggleLike(); const save = useToggleSave(); const share = useSharePost(); const remove = useDeleteFeedPost(); const { show } = useToast();
  const sharePost = async () => { try { await share.mutateAsync(post.id); const url = `${window.location.origin}/feed?post=${post.id}`; await navigator.clipboard.writeText(url); show("Post link copied."); } catch (error) { show(error instanceof Error ? error.message : "Could not share post.", "error"); } };
  return <article className="feed-card">
    <header><Link className="feed-author" to={`/users/${post.author.publicId}`}>{post.author.avatarUrl ? <img src={post.author.avatarUrl} alt="" /> : <span>{post.author.displayName.slice(0, 2).toUpperCase()}</span>}<span><strong>{post.author.displayName}</strong><small>@{post.author.userName} · {new Date(post.createdAt).toLocaleString()}</small></span></Link><b>{post.type.replace(/([A-Z])/g, " $1").trim()}</b></header>
    {post.type !== "Code" && <p className="feed-content">{post.content}</p>}
    {post.type === "Code" && <pre><code data-language={post.codeLanguage}>{post.content}</code></pre>}
    {post.imageUrl && <img className="feed-image" src={post.imageUrl} alt="Post attachment" loading="lazy" />}
    {post.project && <Link className="feed-project" to={`/public/projects/${post.project.id}`}>◇ <span><strong>{post.project.name}</strong><small>Open public project</small></span></Link>}
    <footer><button className={post.isLiked ? "active" : ""} disabled={like.isPending} onClick={() => like.mutate(post.id)}>♥ {post.likeCount}</button><button onClick={() => setCommentsOpen(value => !value)}>◫ {post.commentCount}</button><button className={post.isSaved ? "active" : ""} disabled={save.isPending} onClick={() => save.mutate(post.id)}>⌑ {post.saveCount}</button><button disabled={share.isPending} onClick={() => void sharePost()}>↗ {post.shareCount}</button>{!post.isOwner && <button onClick={() => void reportContent(post.type === "Code" ? "Snippet" : "Post", post.id).then(sent => sent && show("Report submitted for moderator review.")).catch(error => show(error.message, "error"))}>Report</button>}{post.isOwner && <button className="text-danger" disabled={remove.isPending} onClick={() => { if (window.confirm("Delete this post?")) remove.mutate(post.id); }}>Delete</button>}</footer>
    {commentsOpen && <PostComments post={post} />}
  </article>;
}

export function FeedPage() {
  const [tab, setTab] = useState<FeedTab | "Saved">("ForYou"); const [type, setType] = useState<PostType>("Text"); const [content, setContent] = useState(""); const [codeLanguage, setCodeLanguage] = useState(""); const [imageUrl, setImageUrl] = useState(""); const [projectId, setProjectId] = useState("");
  const feed = useFeed(tab === "Saved" ? "ForYou" : tab, tab !== "Saved"); const saved = useSavedFeed(tab === "Saved"); const query = tab === "Saved" ? saved : feed; const create = useCreateFeedPost(); const projects = useProjects(); const discover=useSocialDiscover(); const { show } = useToast();
  const submit = async (event: FormEvent) => { event.preventDefault(); try { await create.mutateAsync({ type, content: content.trim(), codeLanguage: type === "Code" ? codeLanguage.trim() : undefined, imageUrl: type === "Image" ? imageUrl.trim() : undefined, projectId: type === "ProjectShare" ? projectId : undefined }); setContent(""); setCodeLanguage(""); setImageUrl(""); setProjectId(""); show("Post published."); } catch (error) { show(error instanceof Error ? error.message : "Post could not be published.", "error"); } };
  const items = useMemo(() => deduplicateById(query.data?.pages.flatMap(page => page.items) ?? []), [query.data]);
  return <main className="feed-page">
    <header className="feed-heading"><span><small>DEVELOPER NETWORK</small><h1>Feed</h1><p>Share work, code, progress, and useful ideas with the community.</p></span></header>
    <div className="feed-layout"><section className="feed-stream">
      <form className="feed-composer" onSubmit={event => void submit(event)}><header><strong>Create a post</strong><select value={type} onChange={event => setType(event.target.value as PostType)}>{postTypes.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></header><textarea value={content} maxLength={10000} onChange={event => setContent(event.target.value)} placeholder={type === "Code" ? "Paste a useful snippet and explain it…" : "What are you building?"} aria-label="Post content" />
        {type === "Code" && <input value={codeLanguage} maxLength={50} onChange={event => setCodeLanguage(event.target.value)} placeholder="Language, e.g. csharp" required />}{type === "Image" && <input type="url" value={imageUrl} onChange={event => setImageUrl(event.target.value)} placeholder="HTTPS image URL" required />}{type === "ProjectShare" && <select value={projectId} required onChange={event => setProjectId(event.target.value)}><option value="">Choose a public project</option>{projects.data?.map(project => <option key={project.id} value={project.id}>{project.name}</option>)}</select>}
        <footer><small>{content.length.toLocaleString()} / 10,000</small><button disabled={!content.trim() || create.isPending}>{create.isPending ? "Publishing…" : "Publish"}</button></footer></form>
      <nav className="feed-tabs" aria-label="Feed filters">{tabs.map(item => <button className={tab === item.value ? "active" : ""} key={item.value} onClick={() => setTab(item.value)}>{item.label}</button>)}</nav>
      {query.isPending ? <LoadingState label="Loading feed…" /> : query.isError ? <ErrorState message={query.error.message} retry={() => void query.refetch()} /> : items.length === 0 ? <EmptyState title={tab === "Saved" ? "No saved posts" : "Nothing here yet"} description={tab === "Following" ? "Follow developers to build your personal feed." : "Start the conversation by publishing the first post."} /> : items.map(post => <FeedCard key={post.id} post={post} />)}
      {query.hasNextPage && <button className="feed-more" disabled={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()}>{query.isFetchingNextPage ? "Loading…" : "Load more"}</button>}
    </section><aside className="feed-about"><strong>Discover</strong>{discover.data?.developers.slice(0,4).map(x=><Link className="feed-discover-person" to={`/users/${x.publicId}`} key={x.id}><b>{x.displayName}</b><small>@{x.userName} · {x.followers} followers</small></Link>)}{discover.data?.projects.slice(0,3).map(x=><Link className="feed-discover-person" to={`/public/projects/${x.id}`} key={x.id}><b>{x.name}</b><small>{x.saves} feed saves</small></Link>)}{discover.data?.topics.length?<div className="feed-topics">{discover.data.topics.map(x=><span key={x.name}>#{x.name} · {x.posts}</span>)}</div>:null}<strong>Transparent ranking</strong><p><b>For you</b> shows recent posts. <b>Following</b> contains developers you follow. <b>Trending</b> ranks the last 30 days using reactions ×3, comments ×2, saves ×4, and shares ×3.</p><span>{discover.data?.rankingExplanation??"No hidden ML ranking is used."}</span></aside></div>
  </main>;
}
