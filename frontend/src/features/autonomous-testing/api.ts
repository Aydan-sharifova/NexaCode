import { apiClient } from "../../services/apiClient";

export type TestRunStatus = "Analyzing"|"Running"|"AwaitingApply"|"Passed"|"Failed"|"Cancelled"|"AppliedAwaitingRerun";
export type TestIterationOutcome = "Generated"|"Passed"|"Failed"|"TimedOut"|"RuntimeUnavailable"|"InvalidModelOutput";
export interface TestIteration {id:string;number:number;outcome:TestIterationOutcome;sourceHash:string;generatedTestSource:string;stdout?:string;stderr?:string;exitCode?:number;timedOut:boolean;durationMs:number;failureAnalysis?:string;startedAt:string;completedAt:string}
export interface AutonomousTestRun {id:string;projectId:string;workspaceNodeId:string;goal:string;language:string;status:TestRunStatus;maximumIterations:number;completedIterations:number;analysis?:string;finalSummary?:string;suggestedFix?:string;hasProposedFix:boolean;proposedSource?:string;proposedSourceHash?:string;modelProvider?:string;modelName?:string;startedAt:string;completedAt?:string;appliedAt?:string;appliedFileVersionId?:string;iterations:TestIteration[]}
export interface AutonomousTestTimeline {items:AutonomousTestRun[];total:number}
export const autonomousTestingKeys={timeline:(projectId:string)=>["autonomous-tests",projectId] as const,run:(projectId:string,id:string)=>["autonomous-tests",projectId,id] as const};
export const autonomousTestingApi={
  list:(projectId:string)=>apiClient.get<AutonomousTestTimeline>(`/projects/${projectId}/autonomous-tests`),
  get:(projectId:string,id:string)=>apiClient.get<AutonomousTestRun>(`/projects/${projectId}/autonomous-tests/${id}`),
  start:(projectId:string,input:{workspaceNodeId:string;goal:string;maximumIterations:number})=>apiClient.post<AutonomousTestRun>(`/projects/${projectId}/autonomous-tests`,input),
  apply:(projectId:string,id:string)=>apiClient.post<AutonomousTestRun>(`/projects/${projectId}/autonomous-tests/${id}/apply`,{confirm:true}),
  runAgain:(projectId:string,id:string,maximumIterations:number)=>apiClient.post<AutonomousTestRun>(`/projects/${projectId}/autonomous-tests/${id}/run-again`,{maximumIterations})
};
