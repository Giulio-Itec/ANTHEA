using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>Navigation into the existing editors; it owns no engineering data.</summary>
public sealed class EmbeddedWallWorkNode : ObservableObject
{
    private string title = "", detail = "";
    private bool expanded = true, selected;
    public string Title { get => title; internal set => SetProperty(ref title, value); }
    public string Detail { get => detail; internal set => SetProperty(ref detail, value); }
    public bool IsExpanded { get => expanded; set => SetProperty(ref expanded, value); }
    public bool IsSelected { get => selected; set => SetProperty(ref selected, value); }
    public ObservableCollection<EmbeddedWallWorkNode> Children { get; } = [];
    public string? TableKey { get; internal set; }
    public EmbeddedWallRow? Row { get; internal set; }
    public string? CaseId { get; internal set; }
    public EmbeddedWallRow? Stage { get; internal set; }
    internal string? Target { get; set; }
}
