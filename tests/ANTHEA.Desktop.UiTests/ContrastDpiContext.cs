using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace X.Desktop;

/// <summary>
/// The pixel baseline is frozen at 96 DPI. An off-screen window can inherit the nearest monitor's DPI on Windows 11.
/// Only the contrast test thread uses the 96-DPI context, before WPF creates any window; the Release and other tests keep
/// the product manifest and monitor scaling. ContrastBaseline still rejects a measured DPI different from its reference.
/// </summary>
internal static class ContrastDpiContext
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!Environment.GetCommandLineArgs().Contains("--check-contrast", StringComparer.Ordinal)) return;
        // DPI_AWARENESS_CONTEXT_UNAWARE: an isolated test context, not a Windows or application preference.
        if (SetThreadDpiAwarenessContext(new IntPtr(-1)) == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Impossibile preparare il banco di contrasto a 96 DPI.");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
}
