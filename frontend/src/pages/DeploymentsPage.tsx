import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { useToast } from "../contexts/ToastContext";
import { deploymentKeys, deploymentsApi } from "../features/deployments/api";

export function DeploymentsPage(){
  const {projectId=""}=useParams(),client=useQueryClient(),{show}=useToast();
  const list=useQuery({queryKey:deploymentKeys.list(projectId),queryFn:()=>deploymentsApi.list(projectId)});
  const deploy=useMutation({mutationFn:()=>deploymentsApi.deploy(projectId),onSuccess:value=>{void client.invalidateQueries({queryKey:deploymentKeys.list(projectId)});show(`Deployment v${value.version} published.`)},onError:error=>show(error.message,"error")});
  return <main className="dashboard-content feature-page"><header className="feature-heading"><div><Link className="back-link" to={`/projects/${projectId}/workspace`}>← Workspace</Link><h1>Deployments</h1><p>Immutable static snapshots from the current saved workspace. Public projects only.</p></div><button className="create-button" disabled={deploy.isPending} onClick={()=>{if(confirm("Publish the current saved index.html and static assets?"))deploy.mutate()}}>{deploy.isPending?"Deploying…":"Deploy current version"}</button></header>
    {list.isLoading?<LoadingState label="Loading deployments…"/>:list.isError?<ErrorState message={list.error.message} retry={()=>void list.refetch()}/>:!list.data?.length?<EmptyState title="No deployments yet" description="A root index.html is required. Deployments are versioned and source-hashed."/>:<section className="room-grid">{list.data.map(item=><article className="room-card" key={item.id}><span className={`room-status ${item.isActive?"active":"completed"}`}>{item.isActive?"Live":"Superseded"}</span><small>Version {item.version}</small><h2>{item.slug}</h2><p>Source <code>{item.sourceHash.slice(0,12)}</code>{item.commitSha?<> · Commit <code>{item.commitSha.slice(0,8)}</code></>:null}</p><footer><span>{new Date(item.deployedAt).toLocaleString()}</span><a href={item.url} target="_blank" rel="noreferrer">Open deployment ↗</a></footer></article>)}</section>}
  </main>;
}
