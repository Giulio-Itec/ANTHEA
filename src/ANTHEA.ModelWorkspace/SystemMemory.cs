using System.Runtime.InteropServices;

namespace ANTHEA.ModelWorkspace;

/// <summary>Rilievo R7 (16/11/2026): the physical memory free on the computer now (GlobalMemoryStatusEx), for the work started without the
/// user asking for it; elsewhere, or when the call fails, the estimate of the garbage collector at its last collection.</summary>
public static class SystemMemory
{
    /// <summary>Bytes of physical memory available to the programs now.</summary>
    public static long AvailableBytes()
    {
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            if (GlobalMemoryStatusEx(ref status)) return (long)Math.Min(status.AvailablePhysical, long.MaxValue);
        }
        var info = GC.GetGCMemoryInfo();
        return Math.Max(0, info.TotalAvailableMemoryBytes - info.MemoryLoadBytes);
    }

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

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
