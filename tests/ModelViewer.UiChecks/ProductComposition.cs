using System.Reflection;
using System.Windows;
using X.Desktop.Services;

static partial class Program
{
    /// <summary>
    /// The composition root of the product (X.Desktop.Services.DesktopComposition, internal), as App.OnStartup creates it (F5.2):
    /// the checks open the main window and the sheets with its services and its editors. Dialogs without an owner window.
    /// </summary>
    static object ProductComposition()
    {
        var composition = typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.Services.DesktopComposition")!;
        return Activator.CreateInstance(composition, Flags, null, [(Func<Window?>)(() => null)], null)!;
    }

    /// <summary>The factory of the sheet editors of the composition root (internal X.Desktop.SheetEditorFactory).</summary>
    static object ProductSheetEditors(object composition) => composition.GetType().GetProperty("SheetEditors", Flags)!.GetValue(composition)!;

    /// <summary>The main window as App.OnStartup creates it: window services and sheet editors of the composition root.</summary>
    static X.Desktop.MainWindow ProductWindow()
    {
        var composition = ProductComposition();
        var services = (Func<Window, DesktopServices>)Delegate.CreateDelegate(typeof(Func<Window, DesktopServices>), composition,
            composition.GetType().GetMethod("WindowServices", Flags)!);
        return (X.Desktop.MainWindow)Activator.CreateInstance(typeof(X.Desktop.MainWindow), Flags, null, [services, ProductSheetEditors(composition)], null)!;
    }
}
