using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Anthea.Presentation.Shell.Commands;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>
/// The numbers of the bindings of the views with the separator of «Aspetto» (16/11/2026, item d of the digits): before, every binding with a
/// numeric StringFormat or without a format used en-US, the point. Checked off screen on real WPF bindings: a TextBlock with StringFormat, a number
/// without format, a ContentControl with ContentStringFormat and a read-only DataGrid column, with «Virgola», then «Punto» and a culture with the
/// customizations of Windows in the same session; dates stay as before; the eight inputs of the model viewer pinned to en-US keep their reading
/// («0.6» is 0,6 and «0,6» is 6, as before).
/// </summary>
internal static class ViewNumberChecks
{
    private sealed class Row { public double Value { get; set; } = 1234.5; public DateTime When { get; set; } = new(2026, 11, 16); }

    internal static async Task Run(string output, Action<bool, string> check)
    {
        CultureInfo saved = CultureInfo.CurrentCulture, savedDefault = CultureInfo.DefaultThreadCurrentCulture ?? CultureInfo.CurrentCulture;
        check(WpfNumberSeparatorService.InstallViewNumbers(), "Cultura dei numeri delle viste non installata: campo di XmlLanguage cambiato nella versione di WPF?");
        var row = new Row();
        var formatted = new TextBlock(); formatted.SetBinding(TextBlock.TextProperty, new Binding(nameof(Row.Value)) { StringFormat = "F2" });
        var plain = new TextBlock(); plain.SetBinding(TextBlock.TextProperty, new Binding(nameof(Row.Value)));
        var date = new TextBlock(); date.SetBinding(TextBlock.TextProperty, new Binding(nameof(Row.When)) { StringFormat = "d" });
        var content = new ContentControl { ContentStringFormat = "{0:0.###} m" }; content.SetBinding(ContentControl.ContentProperty, new Binding(nameof(Row.Value)));
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, ItemsSource = new[] { row }, Height = 80 };
        grid.Columns.Add(new DataGridTextColumn { Header = "Valore", Binding = new Binding(nameof(Row.Value)) { StringFormat = "G7" } });
        var viewer = new Row { Value = .5 };
        var input = new TextBox(); input.SetBinding(TextBox.TextProperty, new Binding(nameof(Row.Value))
        { Source = viewer, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, ValidatesOnExceptions = true, ConverterCulture = CultureInfo.GetCultureInfo("en-US"), StringFormat = "0.####" });
        var panel = new StackPanel { DataContext = row, Children = { formatted, plain, date, content, grid, input } };
        var window = new Window { Content = panel, Width = 400, Height = 300, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual };
        string Cell() => grid.Columns[0].GetCellContent(row) is TextBlock t ? t.Text : "";
        string Shown() => formatted.Text + " | " + plain.Text + " | " + Content(content) + " | " + Cell() + " | " + date.Text;
        async Task Refresh(double value) { row.Value = value; foreach (var e in new[] { formatted, plain, date }) BindingOperations.GetBindingExpression(e, TextBlock.TextProperty)?.UpdateTarget();
            BindingOperations.GetBindingExpression(content, ContentControl.ContentProperty)?.UpdateTarget(); grid.Items.Refresh(); await Idle(window); }
        var lines = new List<string>();
        try
        {
            window.Show(); await Idle(window);
            string englishDate = new DateTime(2026, 11, 16).ToString("d", CultureInfo.GetCultureInfo("en-US"));
            WpfNumberSeparatorService.Apply(DecimalSeparatorChoice.Comma); await Refresh(1234.5);
            lines.Add("Virgola: " + Shown());
            check(formatted.Text == "1234,50" && plain.Text == "1234,5" && Content(content) == "1234,5 m" && Cell() == "1234,5" && date.Text == englishDate,
                "Binding con «Virgola»: " + Shown());
            WpfNumberSeparatorService.Apply(DecimalSeparatorChoice.Point); await Refresh(0.6);
            lines.Add("Punto (stessa sessione): " + Shown());
            check(formatted.Text == "0.60" && plain.Text == "0.6" && Content(content) == "0.6 m" && Cell() == "0.6", "Binding con «Punto» dopo il cambio nella sessione: " + Shown());
            var custom = (CultureInfo)CultureInfo.GetCultureInfo("it-IT").Clone(); custom.NumberFormat.NumberDecimalSeparator = "·"; custom.NumberFormat.NumberGroupSeparator = "'";
            CultureInfo.CurrentCulture = custom; await Refresh(1234.5);
            lines.Add("Separatore personalizzato di Windows («Sistema»): " + Shown());
            check(formatted.Text == "1234·50" && plain.Text == "1234·5", "Binding con le personalizzazioni di Windows: " + Shown());
            WpfNumberSeparatorService.Apply(DecimalSeparatorChoice.Comma); await Idle(window);
            // The inputs of the model viewer pinned to en-US: the same reading and writing as before 16/11.
            BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateTarget(); await Idle(window);
            bool shownPoint = input.Text == "0.5";
            input.Text = "0.6"; BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource(); double point = viewer.Value;
            input.Text = "0,6"; BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource(); double comma = viewer.Value;
            lines.Add($"Casella del visore con «Virgola»: mostra 0.5 = {shownPoint}; «0.6» → {point.ToString(CultureInfo.InvariantCulture)}; «0,6» → {comma.ToString(CultureInfo.InvariantCulture)}");
            check(shownPoint && point == .6 && comma == 6, "Caselle del visore: lettura cambiata rispetto a prima (attesi 0.6 e 6): " + lines[^1]);
            File.WriteAllLines(Path.Combine(output, "numeri-viste.txt"), lines);
        }
        finally { CultureInfo.CurrentCulture = saved; CultureInfo.DefaultThreadCurrentCulture = savedDefault; window.Close(); }
    }

    private static string Content(ContentControl control)
    {
        control.ApplyTemplate();
        var presenter = FindChild<ContentPresenter>(control);
        return presenter is null ? "" : FindChild<TextBlock>(presenter)?.Text ?? "";
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private static async Task Idle(Window window)
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
