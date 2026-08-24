namespace Coding.Enums;

public enum LiveRoomMode { Interview, Workshop, PairProgramming, CommunityEvent }
public enum LiveRoomStatus { Scheduled, Active, Completed, Cancelled }
public enum LiveRoomVisibility { InviteOnly, ProjectMembers }
public enum LiveRoomParticipantRole { Owner, Host, Interviewer, Candidate, Participant }
public enum LiveRoomParticipantStatus { Invited, Joined, Left, Removed }
public enum LiveRoomChallengeType { Algorithm, CodingTask, Architecture, Debugging }
public enum LiveRoomTaskStatus { Open, Completed }
