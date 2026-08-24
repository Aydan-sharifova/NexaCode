import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { socialFeedApi, type DiscoverFilters, type DiscoverPackage, type DiscoverSort } from "../features/social-feed/api";
import "./DiscoverPage.css";
import { queryKeys } from "../services/queryKeys";

type Category = "Developers"|"Projects"|"Snippets"|"Templates"|"Agents"|"Themes";
const categories:Category[]=["Developers","Projects","Snippets","Templates","Agents","Themes"];

function PackageCards({items}:{items:DiscoverPackage[]}) { return <div className="discover-grid">{items.map(item=><article className="discover-card" key={item.id}><small>{item.category}</small><h3>{item.title}</h3><p>{item.description}</p><div className="discover-tags">{item.tags.map(tag=><span key={tag}>{tag}</span>)}</div><footer>♥ {item.likes} · ↓ {item.downloads}<Link to="/marketplace">View package</Link></footer></article>)}</div>; }

export function DiscoverPage(){
  const [category,setCategory]=useState<Category>("Developers"); const [draft,setDraft]=useState("");
  const [filters,setFilters]=useState<DiscoverFilters>({sort:"Trending",limit:20});
  const result=useQuery({queryKey:queryKeys.discover(filters),queryFn:()=>socialFeedApi.discover(filters)}); const data=result.data;
  const empty=(title:string)=><EmptyState title={`No ${title.toLowerCase()} found`} description="Try broader backend filters."/>;
  return <main className="discover-page"><header><div><small>COMMUNITY CATALOG</small><h1>Discover</h1><p>Find developers, public projects, reusable code and reviewed marketplace packages.</p></div></header>
    <form className="discover-filters" onSubmit={event=>{event.preventDefault();setFilters(value=>({...value,search:draft.trim()||undefined}));}}><input value={draft} onChange={e=>setDraft(e.target.value)} placeholder="Search by name, code or description…"/><input value={filters.technology??""} onChange={e=>setFilters(v=>({...v,technology:e.target.value||undefined}))} placeholder="Technology"/><input value={filters.language??""} onChange={e=>setFilters(v=>({...v,language:e.target.value||undefined}))} placeholder="Language"/><select value={filters.sort} onChange={e=>setFilters(v=>({...v,sort:e.target.value as DiscoverSort}))}><option>Trending</option><option>Popularity</option><option>Recent</option></select><button>Search</button></form>
    <nav className="discover-tabs">{categories.map(item=><button className={category===item?"active":""} onClick={()=>setCategory(item)} key={item}>{item}</button>)}</nav>
    {result.isPending?<LoadingState label="Discovering public work…"/>:result.isError?<ErrorState message={result.error.message} retry={()=>void result.refetch()}/>:<>
      {category==="Developers"&&(data!.developers.length?<div className="discover-grid">{data!.developers.map(x=><Link className="discover-card" to={`/users/${x.publicId}`} key={x.id}><h3>{x.displayName||x.userName}</h3><p>@{x.userName}</p><footer>{x.followers} followers · {x.posts} posts</footer></Link>)}</div>:empty(category))}
      {category==="Projects"&&(data!.projects.length?<div className="discover-grid">{data!.projects.map(x=><Link className="discover-card" to={`/public/projects/${x.id}`} key={x.id}><h3>{x.name}</h3><p>{x.description||"Public project"}</p><footer>{x.saves} saves · owner {x.ownerPublicId}</footer></Link>)}</div>:empty(category))}
      {category==="Snippets"&&(data!.snippets.length?<div className="discover-grid">{data!.snippets.map(x=><article className="discover-card" key={x.id}><small>{x.language}</small><pre>{x.content}</pre><footer>by <Link to={`/users/${x.author.publicId}`}>@{x.author.userName}</Link> · ♥ {x.likes} · {x.saves} saves</footer></article>)}</div>:empty(category))}
      {category==="Templates"&&(data!.templates.length?<PackageCards items={data!.templates}/>:empty(category))}{category==="Agents"&&(data!.agents.length?<PackageCards items={data!.agents}/>:empty(category))}{category==="Themes"&&(data!.themes.length?<PackageCards items={data!.themes}/>:empty(category))}
      <p className="discover-ranking">{data!.rankingExplanation}</p></>}
  </main>;
}
