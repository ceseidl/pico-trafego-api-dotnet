namespace Holiday.Core;

// One switch per protection, so each trap can be shown before and after.
public sealed record Profile
{
    public bool Cache { get; init; }
    public bool WarmUp { get; init; }
    public int? MaxConcurrent { get; init; }     // browsing load shedding
    public int ShedQueue { get; init; }
    public TimeSpan? RecsTimeout { get; init; }  // null = wait forever
    public int? OrderQueueSize { get; init; }    // null = unbounded
    public int OrderWorkers { get; init; } = 2;
    public int OrderMs { get; init; } = 20;
    public int QueryMs { get; init; } = 25;
    public int RecsMs { get; init; } = 20;

    // What most APIs look like on a normal day.
    public static Profile Plain => new();

    // What we want in place before December 24th.
    public static Profile Ready => new()
    {
        Cache = true,
        WarmUp = true,
        MaxConcurrent = 64,
        ShedQueue = 32,
        RecsTimeout = TimeSpan.FromMilliseconds(150),
        OrderQueueSize = 50,
    };
}
