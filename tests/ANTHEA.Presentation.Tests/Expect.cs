namespace Anthea.Presentation.Tests;

/// <summary>
/// Counted checks of one group. A failed check no longer stops the suite (approved on 18/11/2026: a failure hid every check after it): it is
/// collected with its message and the run goes on, so that one run shows every difference, the frozen traces included; the program fails at the
/// end with the whole list (<see cref="Failures"/>). An exception that is not a check (a group that cannot go on) still ends its group only.
/// </summary>
internal sealed class Expect
{
    /// <summary>Upper bound for awaiting a barrier: a deadlock fails the suite instead of hanging the runner. Not a pause.</summary>
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private static readonly object Gate = new();
    private static readonly List<string> Collected = [];

    /// <summary>The failed checks of the run, in the order they happened (the checks of a group may run on more threads).</summary>
    internal static IReadOnlyList<string> Failures { get { lock (Gate) return Collected.ToArray(); } }

    /// <summary>Records a failure that is not the condition of a check: an exception that ended a group.</summary>
    internal static void Fail(string message) { lock (Gate) Collected.Add(message); }

    internal int Count { get; private set; }

    internal void That(bool condition, string message)
    {
        if (!condition) { Fail(message); return; }
        Count++;
    }

    internal void Throws<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { Count++; return; }
        Fail(message);
    }

    internal static Task Within(Task task) => task.WaitAsync(Deadline);
    internal static Task<T> Within<T>(Task<T> task) => task.WaitAsync(Deadline);
}
