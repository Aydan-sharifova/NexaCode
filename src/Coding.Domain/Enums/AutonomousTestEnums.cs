namespace Coding.Enums;

public enum AutonomousTestRunStatus
{
    Analyzing = 0,
    Running = 1,
    AwaitingApply = 2,
    Passed = 3,
    Failed = 4,
    Cancelled = 5,
    AppliedAwaitingRerun = 6
}

public enum AutonomousTestIterationOutcome
{
    Generated = 0,
    Passed = 1,
    Failed = 2,
    TimedOut = 3,
    RuntimeUnavailable = 4,
    InvalidModelOutput = 5
}
