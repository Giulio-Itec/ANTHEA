using System.IO;
using System.Windows;
using System.Windows.Media;
using ANTHEA.ModelReports;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using HelixToolkit.Wpf.SharpDX;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>A separate render host keeps report generation from changing the interactive viewport.</summary>
internal sealed class ModelReportRenderSession : IDisposable
{
    readonly Window window;
    readonly Viewport3DX viewport;
    readonly ModelSceneRenderer renderer;
    readonly ModelViewerViewModel state;
    readonly ModelReportDocument recipe;
    public ModelReportRenderSession(ModelSnapshot snapshot, ModelReportDocument recipe)
    {
        this.recipe = recipe;
        viewport = new() { IsHitTestVisible = false, ShowFrameRate = false };
        window = new() { Content = viewport, Width = 1000, Height = 750, WindowStyle = WindowStyle.None,
            ShowActivated = false, ShowInTaskbar = false, Opacity = 0, ResizeMode = ResizeMode.NoResize };
        renderer = new(viewport); renderer.SetModel(snapshot);
        state = new(snapshot, [], true, new RenderServices(), _ => { }, (_, _, _, _) => { });
        window.Show(); window.UpdateLayout();
    }
    public Task<ReportFigure> CaptureAsync(ModelReportBlock block, string path, CancellationToken cancellationToken)
        => viewport.Dispatcher.InvokeAsync(() => CaptureCoreAsync(block, path, cancellationToken)).Task.Unwrap();
    async Task<ReportFigure> CaptureCoreAsync(ModelReportBlock block, string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(block.MemberDiagram!=null) return MemberDiagramReportRenderer.Capture(state.Snapshot!,block,path,recipe.ImageWidth,recipe.WhiteBackground);
        var preset = block.View!;
        state.ApplyView(preset); state.CompareEnabled = false;
        if (preset.Mode == 2 && (state.SelectedComponent != preset.Component || !state.HasResults))
            throw new InvalidDataException($"«{block.Title}»: il campo {preset.Component} non è disponibile per {preset.Case}.");
        if (preset.Clip && state.ActiveClipPlanes.Count==0)
            throw new InvalidDataException($"«{block.Title}»: il taglio non può essere ricostruito.");
        if (preset.Appearance is { Deformation: true } && !state.CanDeform)
            throw new InvalidDataException($"«{block.Title}»: deformata non disponibile. {state.DeformationMessage}");
        if (preset.Appearance is { Loads: true } a && !state.LoadCases.Contains(a.LoadCase ?? ""))
            throw new InvalidDataException($"«{block.Title}»: il caso dei carichi non è disponibile.");
        var camera = preset.Camera!;
        window.Height = 1000 / (camera.AspectRatio > 0 ? camera.AspectRatio : 4.0 / 3);
        window.UpdateLayout();
        state.ExportWidth = recipe.ImageWidth; state.ExportWhiteBackground = recipe.WhiteBackground;
        if (recipe.WhiteBackground) state.ApplyBackgroundCommand.Execute("#FFFFFF");
        viewport.BackgroundColor = (Color)ColorConverter.ConvertFromString(state.SceneBackground);
        viewport.ShowCoordinateSystem = state.ShowGlobalAxes; viewport.ShowViewCube = false;
        viewport.CoordinateSystemLabelForeground = (Color)ColorConverter.ConvertFromString(state.SceneInk);
        viewport.MSAA = state.SelectedAntialiasing switch { 2 => HelixToolkit.SharpDX.MSAALevel.Four, 3 => HelixToolkit.SharpDX.MSAALevel.Eight, _ => HelixToolkit.SharpDX.MSAALevel.Disable };
        viewport.FXAALevel = state.SelectedAntialiasing == 1 ? HelixToolkit.SharpDX.FXAALevel.High : HelixToolkit.SharpDX.FXAALevel.None;
        viewport.EnableSSAO = state.SelectedLighting == 2 && !state.IsResultsView;
        renderer.RestoreCamera(camera);
        var stats = renderer.Render(ModelViewerControl.Options(state) with { RasterScale=recipe.ImageWidth/1200.0 });
        await FrameAsync(cancellationToken);
        ViewerImageExporter.SaveScene(viewport, null, state, path, printLayout:true);
        string description = state.ResultHeading + (state.IsResultsView ? " · " + state.ResultUnit + " · " + (state.IsDisplacementResult ? "Globali" : state.AxesLabels[state.SelectedAxes]) : "") +
            "\n" + state.VisibilitySummary + (state.ClipEnabled ? " · " + state.ClipDescription : "");
        if (state.IsResultsView) description += "\nScala: " + state.FormatLegend(state.ColorMinimum) + " / " + state.FormatLegend(state.ColorMaximum) + ". " + state.ColorScaleModes[state.SelectedColorScale] + ".";
        if (stats.MissingElements > 0 && state.IsResultsView) description += $"\n{stats.MissingElements} elementi senza risultati del campo selezionato.";
        if (stats.IncompleteDeformations > 0) description += $"\n{stats.IncompleteDeformations} elementi con spostamenti incompleti: contorno grigio nella posizione originale.";
        if (state.IsAveragedContour) description += "\nMedia solo grafica; dati originali invariati.";
        if (state.Snapshot!.Results.FirstOrDefault(r => r.Name == state.SelectedCase)?.IsEnvelope == true && state.IsResultsView)
            description += "\nInviluppo importato: estremi per componente non simultanei.";
        if (state.ShowLoads) description += "\n" + state.LoadsMessage;
        if (state.ShowDeformation) description += "\n" + state.DeformationHeading + ". " + state.DeformationMessage;
        if (state.IsExtruded) description += "\nVolumi preliminari: spessori e offset plate; aste rettangolari centrate, offset di estremità non importati.";
        if (state.IsExtruded && state.IsResultsView && state.Family == "PLATE") description += "\nCampo plate riportato sulle facce estruse; non è una distribuzione di tensione nello spessore.";
        if(state.IsResultsView && state.IsBeamFamily)
        {
            description += "\nAste: " + (state.ShowLineContour?"contouring attivo":"contouring nascosto") + (state.ShowBeamDiagram?$"; diagramma lungo {(state.BeamDiagramAlongY?"Y":"Z")} locale, scala grafica {state.BeamDiagramScale:G3}.":"; diagramma nascosto.");
            description += "\nInterpolazione grafica fra campioni importati; nessuna estensione oltre le stazioni disponibili.";
        }
        var categories=!state.IsResultsView && state.SelectedColorMode!=0
            ? state.ColorLegend.Select(c=>new ReportCategory(c.Label,c.Color,c.Count)).ToArray() : [];
        if(categories.Length>0) description += "\n" + state.ColorModes[state.SelectedColorMode];
        return new(path, description,categories);
    }
    async Task FrameAsync(CancellationToken token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler rendered = (_, _) => completion.TrySetResult();
        viewport.OnRendered += rendered;
        try { viewport.InvalidateRender(); await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), token); }
        finally { viewport.OnRendered -= rendered; }
    }
    public void Dispose()
    {
        if (!window.Dispatcher.CheckAccess()) { window.Dispatcher.Invoke(Dispose); return; }
        try { state.Dispose(); renderer.Dispose(); } finally { window.Close(); }
    }
    sealed class RenderServices : IModelViewerServices
    {
        public Task<ModelSnapshot?> ImportAsync(CancellationToken cancellationToken) => Task.FromResult<ModelSnapshot?>(null);
        public void FitView() { } public void SaveImage() { } public void ExportTable(string name, string csv) { }
    }
}
