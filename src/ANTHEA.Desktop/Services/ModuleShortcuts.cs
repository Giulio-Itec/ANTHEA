using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;

namespace X.Desktop.Services;

/// <summary>
/// The identity of a module alone on the taskbar (19/11/2026): ANTHEA started with «--modulo» takes the identity of the module
/// (<see cref="AppUserModelId"/>), the same one written in its shortcut, so that Windows groups its windows under the pinned shortcut and not
/// under ANTHEA; the windows show the icon of the module (<see cref="Ui.ModuleIcon"/>).
/// </summary>
internal static class ModuleIdentity
{
    private const string Prefix = "ITEC.ANTHEA.Modulo.";
    private const int IconSize = 256, WindowIconSize = 64, DesignSize = 80;

    /// <summary>The identity of the module for Windows (AppUserModelID).</summary>
    internal static string AppUserModelId(string module) => Prefix + module;

    /// <summary>The process takes the identity of <paramref name="module"/>: called before the first window.</summary>
    internal static void Apply(string module) => SetCurrentProcessExplicitAppUserModelID(AppUserModelId(module));

    /// <summary>The icon of the module drawn at <paramref name="size"/> pixels, on a transparent background.</summary>
    internal static BitmapSource Picture(string module, int size)
    {
        var icon = Ui.ModuleIcon(module);
        var box = new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = new Border { Width = DesignSize, Height = DesignSize, Child = icon } };
        box.Measure(new Size(size, size)); box.Arrange(new Rect(0, 0, size, size)); box.UpdateLayout();
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(box); bitmap.Freeze();
        return bitmap;
    }

    /// <summary>The icon of the windows of the module.</summary>
    internal static ImageSource WindowIcon(string module) => Picture(module, WindowIconSize);

    /// <summary>Writes the icon of <paramref name="module"/> as an .ico with one 256-pixel PNG image (read by Windows since Vista).</summary>
    internal static void WriteIcon(string module, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Picture(module, IconSize)));
        using var png = new MemoryStream(); encoder.Save(png);
        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);
        const short Reserved = 0, IconType = 1, Images = 1, Planes = 1, Bits = 32;
        const byte Large = 0, Colours = 0, Unused = 0;
        const int Offset = 22;
        writer.Write(Reserved); writer.Write(IconType); writer.Write(Images);
        writer.Write(Large); writer.Write(Large); writer.Write(Colours); writer.Write(Unused);
        writer.Write(Planes); writer.Write(Bits); writer.Write((int)png.Length); writer.Write(Offset);
        writer.Write(png.ToArray());
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(string appId);
}

/// <summary>
/// The shortcuts of the modules (19/11/2026, <see cref="IModuleShortcuts"/>): «ANTHEA · modulo.lnk» in the folder ANTHEA of the Start menu, on
/// ANTHEA.exe with «--modulo», with the icon of the module (written beside, in the data of the user) and its identity for the taskbar
/// (<see cref="ModuleIdentity.AppUserModelId"/>); Esplora file then shows it, for the user to pin it.
/// </summary>
internal sealed class ModuleShortcuts(string folder, string icons, string program, Action<string>? reveal = null) : IModuleShortcuts
{
    /// <summary>The shortcuts of the product: the Start menu of the user, the icons in its local data, the running ANTHEA.exe, Esplora file.</summary>
    internal static ModuleShortcuts ForUser() => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ANTHEA"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "Moduli"),
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ANTHEA.exe"),
        path => Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }));

    public string Create(string module, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(module);
        ArgumentException.ThrowIfNullOrEmpty(name);
        Directory.CreateDirectory(folder); Directory.CreateDirectory(icons);
        string icon = Path.Combine(icons, module + ".ico");
        ModuleIdentity.WriteIcon(module, icon);
        string file = string.Concat(("ANTHEA · " + name).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
        string path = Path.Combine(folder, file + ".lnk");
        ShellLinks.Write(path, program, StartupRequest.ModuleFlag + " " + module, Path.GetDirectoryName(program) ?? "", icon, "ANTHEA · " + name,
            ModuleIdentity.AppUserModelId(module));
        reveal?.Invoke(path);
        return path;
    }
}

/// <summary>The shortcuts of Windows (.lnk) with their AppUserModelID, through the COM objects of the shell; <see cref="Read"/> for the checks.</summary>
internal static class ShellLinks
{
    private const ushort StringType = 31;
    private static readonly Guid LinkClass = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid AppUserModelFormat = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private const uint AppUserModelIdProperty = 5;
    private const int TextLength = 1024;

    /// <summary>Writes the shortcut <paramref name="path"/>.</summary>
    internal static void Write(string path, string target, string arguments, string folder, string icon, string description, string appId)
    {
        var link = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(LinkClass, true)!)!;
        try
        {
            link.SetPath(target); link.SetArguments(arguments); link.SetWorkingDirectory(folder); link.SetIconLocation(icon, 0); link.SetDescription(description);
            var store = (IPropertyStore)link;
            var key = new PropertyKey(AppUserModelFormat, AppUserModelIdProperty);
            var value = new PropVariant { Type = StringType, Pointer = Marshal.StringToCoTaskMemUni(appId) };
            try { store.SetValue(ref key, ref value); store.Commit(); }
            finally { PropVariantClear(ref value); }
            ((IPersistFile)link).Save(path, true);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    /// <summary>Target, arguments, icon and AppUserModelID of the shortcut <paramref name="path"/>.</summary>
    internal static (string Target, string Arguments, string Icon, string AppId) Read(string path)
    {
        var link = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(LinkClass, true)!)!;
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var target = new StringBuilder(TextLength); link.GetPath(target, TextLength, IntPtr.Zero, 0);
            var arguments = new StringBuilder(TextLength); link.GetArguments(arguments, TextLength);
            var icon = new StringBuilder(TextLength); link.GetIconLocation(icon, TextLength, out _);
            var key = new PropertyKey(AppUserModelFormat, AppUserModelIdProperty);
            ((IPropertyStore)link).GetValue(ref key, out var value);
            try { return (target.ToString(), arguments.ToString(), icon.ToString(), value.Type == StringType ? Marshal.PtrToStringUni(value.Pointer) ?? "" : ""); }
            finally { PropVariantClear(ref value); }
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct PropertyKey(Guid format, uint id)
    {
        private readonly Guid format = format;
        private readonly uint id = id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Type;
        private readonly ushort reserved1, reserved2, reserved3;
        public IntPtr Pointer;
        private readonly IntPtr extra;
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int length, IntPtr data, uint flags);
        void GetIDList(out IntPtr list);
        void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder folder, int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string folder);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short key);
        void SetHotkey(short key);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int length, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }
}
