/// <summary>
/// SLE engine of the checks (refactoring F2.7, commit A5): '--motore-sle legacy|libreria' among the arguments, otherwise null, the default
/// engine of ConcreteServiceabilityAdapter. The stress limits (CheckerSection) and the cracking of the checks go through the adapter with it.
/// </summary>
internal static class SleEngine
{
    internal static ServiceabilityEngine? Selected { get; private set; }

    /// <summary>Reads the option and returns the other arguments, so that the dispatch of Program.cs does not change.</summary>
    internal static string[] Take(string[] args)
    {
        int at = Array.IndexOf(args, "--motore-sle");
        if (at < 0) return args;
        Selected = (at + 1 < args.Length ? args[at + 1] : null) switch
        {
            "legacy" => ServiceabilityEngine.Legacy, "libreria" => ServiceabilityEngine.Library,
            var other => throw new ArgumentException("--motore-sle: legacy o libreria, non " + other)
        };
        Console.WriteLine("Motore SLE: " + Selected);
        return [.. args[..at], .. args[(at + 2)..]];
    }
}
