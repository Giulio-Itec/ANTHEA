using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Desktop.Views.Common;

namespace X.Desktop;

/// <summary>Modal host and drawing only. Draft, validation and application belong to the ViewModel.</summary>
internal sealed class ConcreteTemplateWizardWindow : Window
{
    internal ConcreteTemplateWizardViewModel Model { get; }
    internal ConcreteTemplateWizardWindow(JsonObject data)
    {
        Model = new(data);
        Title = Model.Title;
        Width = 1160; Height = 820; MinWidth = 780; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Appearance.Surface;
        var preview = new ConcreteSectionViewport { MinHeight = 280, Stirrups = new JsonObject(), Labels = true, Axes = true };
        var form = new FieldForm { DataContext = Model.Form, Margin = new Thickness(12) };
        var error = Ui.Text("", 12); error.SetBinding(TextBlock.TextProperty, new Binding(nameof(Model.Error)) { Source = Model });
        var note = Ui.Text("", 12); note.SetBinding(TextBlock.TextProperty, new Binding(nameof(Model.Note)) { Source = Model });
        var apply = Ui.Button("Applica sezione", () => { if (Model.Apply()) DialogResult = true; });
        apply.SetBinding(IsEnabledProperty, new Binding(nameof(Model.CanApply)) { Source = Model });
        var cancel = Ui.Button("Annulla", () => DialogResult = false); cancel.IsCancel = true;
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(430) });
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        layout.Children.Add(scroll);
        var right = Ui.Dock(preview, bottom: Ui.Stack(note, error, Ui.Bar(apply, cancel)));
        right.Margin = new Thickness(12); Grid.SetColumn(right, 1); layout.Children.Add(right);
        Content = layout;
        void UpdatePreview() { preview.Section = Model.Geometry; preview.Message = Model.Error; preview.InvalidateVisual(); }
        Model.PreviewChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }
}
