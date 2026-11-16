namespace ANTHEA.ModelWorkspace;

/// <summary>Rilievi R1 and R6 (16/11/2026): the memory of the process and of the computer, for the operations that move gigabytes.</summary>
public static class SystemMemory
{
    /// <summary>
    /// Rilievi R1 and R6 (16/11/2026): gives back to Windows the memory the garbage collector keeps after an operation that freed or moved
    /// gigabytes (the end of a series of combinations, «Libera», a copy read again). Without it the process stayed at 24 GB after «Calcola tutte»
    /// on the MIDAS model of 16/11 with 7.7 GB of live data, and at 18 GB after «Libera tutte» with 3.6 GB: the collections had freed the
    /// memory, the process kept it, because nothing ran afterwards. A full, blocking and compacting collection in the mode that returns the free
    /// memory (GCCollectionMode.Aggressive), on a thread of the pool; it stops every thread while it runs, so it is asked only at the end of such
    /// operations, never in a loop.
    /// </summary>
    public static Task ReleaseAsync() => Task.Run(Release);

    /// <summary>As <see cref="ReleaseAsync"/>, on the calling thread (a thread of the pool that has just finished such an operation).</summary>
    public static void Release() => GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
}
