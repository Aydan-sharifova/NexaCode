namespace Coding.Enums;

public enum DebuggingIncidentKind { Runtime = 0, Build = 1, Test = 2 }
public enum DebuggingIncidentStatus { Open = 0, Analyzed = 1, Resolved = 2 }
public enum DebugEvidenceKind { Error = 0, StackTrace = 1, RecentChange = 2, GitCommit = 3, AffectedFile = 4, Log = 5, TestResult = 6 }
public enum DebugEvidenceConfidence { Low = 0, Medium = 1, High = 2 }
