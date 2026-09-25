using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Materiali;

/// <summary>Uses physical monitor coordinates only at the Win32 boundary; WPF lays out in DIPs.</summary>
internal static class DisplayAdaptation
{
    internal static string CheckMaximized(Window window)
    {
        if(window.WindowState!=WindowState.Maximized || !double.IsPositiveInfinity(window.MaxHeight) || !double.IsPositiveInfinity(window.MaxWidth))
            throw new Exception("La finestra non è massimizzata o conserva limiti massimi fissi.");
        var hwnd=new WindowInteropHelper(window).Handle;
        var monitor=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfo(MonitorFromWindow(hwnd,2),ref monitor))throw new Exception("Monitor non disponibile.");
        if(DwmGetWindowAttribute(hwnd,9,out var bounds,Marshal.SizeOf<Rect>())!=0)throw new Exception("Bordi visibili non disponibili.");
        var dpi=VisualTreeHelper.GetDpi(window);
        double tolerance=2*Math.Max(dpi.DpiScaleX,dpi.DpiScaleY);
        if(Math.Abs(bounds.Left-monitor.Work.Left)>tolerance || Math.Abs(bounds.Top-monitor.Work.Top)>tolerance ||
           Math.Abs(bounds.Right-monitor.Work.Right)>tolerance || Math.Abs(bounds.Bottom-monitor.Work.Bottom)>tolerance)
            throw new Exception($"Finestra {bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom}; area utile {monitor.Work.Left},{monitor.Work.Top},{monitor.Work.Right},{monitor.Work.Bottom}.");
        return $"OK: avvio massimizzato, nessun limite massimo fisso, bordi visibili coincidenti con area utile del monitor. DPI {dpi.PixelsPerInchX:0}/{dpi.PixelsPerInchY:0}; area {monitor.Work.Right-monitor.Work.Left} × {monitor.Work.Bottom-monitor.Work.Top} pixel.\n"+
            "Manifest PerMonitorV2. Le scale Windows e i monitor fisici non sono stati modificati durante la prova.";
    }
    internal static void Attach(Window window)
    {
        HwndSource? source = null;
        double minimumWidth = window.MinWidth, minimumHeight = window.MinHeight;
        bool pending = false, closed = false;
        void Fit()
        {
            if (closed || source is null) return;
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromWindow(source.Handle, 2), ref info)) return;
            var dpi = VisualTreeHelper.GetDpi(window);
            int availableWidth = info.Work.Right - info.Work.Left;
            int availableHeight = info.Work.Bottom - info.Work.Top;
            window.MinWidth = Math.Min(minimumWidth, availableWidth / dpi.DpiScaleX);
            window.MinHeight = Math.Min(minimumHeight, availableHeight / dpi.DpiScaleY);
            if (window.WindowState != WindowState.Normal || !GetWindowRect(source.Handle, out var rect)) return;
            int width = Math.Min(rect.Right - rect.Left, availableWidth);
            int height = Math.Min(rect.Bottom - rect.Top, availableHeight);
            int left = Math.Clamp(rect.Left, info.Work.Left, info.Work.Right - width);
            int top = Math.Clamp(rect.Top, info.Work.Top, info.Work.Bottom - height);
            if (width != rect.Right - rect.Left || height != rect.Bottom - rect.Top || left != rect.Left || top != rect.Top)
                SetWindowPos(source.Handle, IntPtr.Zero, left, top, width, height, 0x0014); // No activation or Z-order change.
        }
        void QueueFit()
        {
            if (pending || closed) return;
            pending = true;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { pending = false; Fit(); }));
        }
        IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Let WPF apply WM_DPICHANGED before checking the resulting physical bounds.
            if (message is 0x02E0 or 0x007E or 0x001A or 0x0232) QueueFit();
            return IntPtr.Zero;
        }
        window.SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source?.AddHook(Hook);
            Fit();
        };
        window.Loaded += (_, _) => QueueFit();
        window.StateChanged += (_, _) => QueueFit();
        window.Closed += (_, _) => { closed = true; source?.RemoveHook(Hook); source = null; };
    }

    // Supply a finite layout area even inside a ScrollViewer, so star grids and
    // the editors' own scrollbars keep working. Very small windows can scroll the shell.
    internal static ScrollViewer Viewport(FrameworkElement content, double minWidth, double minHeight)
    {
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
        void Resize()
        {
            content.Width = Math.Max(minWidth, viewer.ViewportWidth);
            content.Height = Math.Max(minHeight, viewer.ViewportHeight);
        }
        viewer.Loaded += (_, _) => Resize();
        viewer.ScrollChanged += (_, e) =>
        {
            if (e.OriginalSource == viewer && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) Resize();
        };
        content.Width = minWidth;
        content.Height = minHeight;
        return viewer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out Rect value,int size);
}

