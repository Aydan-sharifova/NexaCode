export type LiveRoomMode = "Interview" | "Workshop" | "PairProgramming" | "CommunityEvent";
export type LiveRoomStatus = "Scheduled" | "Active" | "Completed" | "Cancelled";
export type LiveRoomVisibility = "InviteOnly" | "ProjectMembers";
export type LiveRoomRole = "Owner" | "Host" | "Interviewer" | "Candidate" | "Participant";
export interface LiveRoomUser { id: string; publicId: string; userName: string; fullName: string; avatarUrl?: string; }
export interface LiveRoomParticipant { id: string; user: LiveRoomUser; role: LiveRoomRole; status: "Invited" | "Joined" | "Left" | "Removed"; invitedAt: string; joinedAt?: string; leftAt?: string; }
export interface LiveRoomSummary { id: string; projectId?: string; title: string; description?: string; mode: LiveRoomMode; status: LiveRoomStatus; visibility: LiveRoomVisibility; challengeType?: "Algorithm" | "CodingTask" | "Architecture" | "Debugging"; problemTitle?: string; durationMinutes?: number; scheduledAt?: string; startedAt?: string; completedAt?: string; stateVersion: number; owner: LiveRoomUser; participantCount: number; currentUserRole: LiveRoomRole; }
export interface LiveRoomDetails { room: LiveRoomSummary; problemStatement?: string; participants: LiveRoomParticipant[]; canManage: boolean; canStart: boolean; canComplete: boolean; }
export interface LiveRoomMessage { id: string; roomId: string; author: LiveRoomUser; content: string; sentAt: string; }
export interface LiveRoomStateEvent { roomId: string; status: LiveRoomStatus; startedAt?: string; completedAt?: string; durationMinutes?: number; stateVersion: number; serverTime: string; }
export interface LiveRoomTask { id: string; roomId: string; createdBy: LiveRoomUser; title: string; description?: string; status: "Open" | "Completed"; createdAt: string; completedAt?: string; }
export interface LiveRoomReaction { id: string; roomId: string; user: LiveRoomUser; emoji: string; createdAt: string; }
export interface InterviewerNote { id: string; roomId: string; author: LiveRoomUser; content: string; createdAt: string; updatedAt: string; }
