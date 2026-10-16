namespace Anthea.Presentation.Tests;

/// <summary>Counted checks of one group: a failed check throws with its message, as in the other console suites.</summary>
internal sealed class Expect
{
    /// <summary>Upper bound for awaiting a barrier: a deadlock fails the suite instead of hanging the runner. Not a pause.</summary>
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    internal int Count { get; private set; }

    internal void That(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Count++;
    }

    internal void Throws<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { Count++; return; }
        throw new InvalidOperationException(message);
    }

    internal static Task Within(Task task) => task.WaitAsync(Deadline);
    internal static Task<T> Within<T>(Task<T> task) => task.WaitAsync(Deadline);
}
