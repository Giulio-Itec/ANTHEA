using System.Collections.ObjectModel;
using System.ComponentModel;
using ANTHEA.ModelReports;
using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ReportChoice<T>(string label, T value) : ObservableObject
{
    public string Label { get; } = label;
    public T Value { get; } = value;
    [ObservableProperty] bool selected;
}

public sealed partial class ReportChapter : ObservableObject
{
    public ReportChapter(ModelReportBlock block) { Source = block; title = block.Title; text = block.Text; included = block.Included; }
    public ModelReportBlock Source { get; private set; }
    [ObservableProperty] string title;
    [ObservableProperty] string text;
    [ObservableProperty] bool included;
    public bool IsFigure => Source.Kind == ReportBlockKind.Figure;
    public string Kind => Source.MemberDiagram!=null?"DIAGRAMMI ASTA":Source.Kind switch { ReportBlockKind.Figure => "FIGURA DINAMICA", ReportBlockKind.Text => "TESTO", _ => "VERIFICA · PREDISPOSIZIONE" };
    public string Detail => Source.MemberDiagram is {} member ? $"Asta {member.Element} · {Source.View?.Case}\n{(member.AllComponents?"Tutte le componenti":Source.View?.Component)} · assi locali · x/L {member.Station:0.####}":Source.View is {} v ? $"{v.Name} · {v.Case ?? "Geometria"} · {v.Component}\n{(v.Visible is null ? "Tutti gli elementi" : v.Visible.Length + " elementi salvati")} · camera e stile memorizzati" :
        Source.Kind == ReportBlockKind.Verification ? $"{Source.SheetName ?? "Foglio da associare"}\nRisultati non ancora collegati." : "Testo libero conservato durante la rigenerazione.";
    public ModelReportBlock ToBlock() => Source with { Title = Title, Text = Text, Included = Included };
    public void ReplaceView(ModelViewPreset view, ModelReportMemberDiagram? member=null) { Source = Source with { View = view, MemberDiagram=member }; OnPropertyChanged(nameof(Detail));OnPropertyChanged(nameof(Kind)); }
}

public sealed partial class ModelViewerViewModel
{
    public ObservableCollection<ReportChapter> ReportChapters { get; } = [];
    public ObservableCollection<ReportChoice<string>> ReportCases { get; } = [];
    public ObservableCollection<ReportChoice<ModelViewPreset>> ReportViews { get; } = [];
    [ObservableProperty] string reportTitle = "Relazione del modello";
    [ObservableProperty] string reportAuthor = "";
    [ObservableProperty] string reportMessage = "Aggiungi viste e testi, poi genera il documento.";
    [ObservableProperty] ReportChapter? selectedReportChapter;
    [ObservableProperty] SheetTarget? reportSheet;
    [ObservableProperty] bool isReportRunning;
    [ObservableProperty] double reportPercent;
    [ObservableProperty] bool reportWhiteBackground = true;
    [ObservableProperty] int reportImageWidth = 2400;
    [ObservableProperty] bool reportDirty;
    [ObservableProperty] string? lastReportPdf;
    string reportRevision = "";
    string reportModelName = "";
    public bool CanEditReport => !IsReportRunning;
    public string ReportRevisionStatus => reportRevision.Length == 0 ? "Nessun modello associato" : reportRevision != viewRevision
        ? "Revisione del modello cambiata: controlla i riferimenti e rigenera il report."
        : ReportDirty ? "Composizione modificata · documento da rigenerare" : "Composizione associata alla revisione corrente";
    public string ReportCount => $"{ReportChapters.Count(c => c.Included)} / {ReportChapters.Count} capitoli inclusi";
    partial void OnReportTitleChanged(string value) => MarkReportDirty();
    partial void OnReportAuthorChanged(string value) => MarkReportDirty();
    partial void OnReportWhiteBackgroundChanged(bool value) => MarkReportDirty();
    partial void OnReportImageWidthChanged(int value) => MarkReportDirty();
    partial void OnReportDirtyChanged(bool value) { OnPropertyChanged(nameof(ReportRevisionStatus)); OpenReportPdfCommand.NotifyCanExecuteChanged(); }
    partial void OnLastReportPdfChanged(string? value) => OpenReportPdfCommand.NotifyCanExecuteChanged();
    partial void OnIsReportRunningChanged(bool value) { OnPropertyChanged(nameof(CanEditReport)); ImportCommand.NotifyCanExecuteChanged(); }
    void MarkReportDirty() { ReportDirty = true; OnPropertyChanged(nameof(ReportCount)); }
    void ChapterChanged(object? sender, PropertyChangedEventArgs e) => MarkReportDirty();
    void InitializeReport()
    {
        if (reportRevision.Length == 0) { reportRevision = viewRevision; reportModelName = Title; }
        RefreshReportChoices(); OnPropertyChanged(nameof(ReportRevisionStatus)); OpenReportPdfCommand.NotifyCanExecuteChanged();
    }
    void RefreshReportChoices()
    {
        var selectedCases = ReportCases.Where(c => c.Selected).Select(c => c.Value).ToHashSet();
        ReportCases.Clear(); foreach (string name in Cases) ReportCases.Add(new(name, name) { Selected = selectedCases.Contains(name) });
        var selectedViews = ReportViews.Where(c => c.Selected).Select(c => c.Value.Name).ToHashSet();
        ReportViews.Clear(); foreach (var view in SavedViews) ReportViews.Add(new(view.Name, view) { Selected = selectedViews.Contains(view.Name) });
    }
    bool CanComposeReport() => Snapshot != null && !disposed && !IsReportRunning;
    void AddChapter(ModelReportBlock block)
    {
        if (ReportChapters.Count >= 100) throw new InvalidDataException("Il report può contenere al massimo 100 capitoli.");
        var chapter = new ReportChapter(block); chapter.PropertyChanged += ChapterChanged;
        ReportChapters.Add(chapter); SelectedReportChapter = chapter; MarkReportDirty();
    }
    ModelViewPreset ReportCapture() => CaptureView("Vista principale") with { Camera = services.CaptureCamera() ?? throw new InvalidOperationException("La vista grafica non è ancora pronta.") };
    [RelayCommand] void AddReportFigure()
    {
        if (!CanComposeReport()) return;
        try { AddChapter(new() { Kind = ReportBlockKind.Figure, Title = IsResultsView ? ResultHeading : "Geometria del modello", View = ReportCapture() });
            ReportMessage = "Figura aggiunta. Salva la composizione per ritrovarla alla riapertura. Le letture fissate e il confronto affiancato non sono inclusi."; }
        catch (Exception ex) { ReportMessage = ex.Message; }
    }
    [RelayCommand] void AddReportText()
    {
        if (!CanComposeReport()) return;
        try { AddChapter(new() { Kind = ReportBlockKind.Text, Title = "Note al modello" }); }
        catch (Exception ex) { ReportMessage = ex.Message; }
    }
    ModelReportMemberDiagram CaptureMemberDiagram()
    {
        if(!HasMemberDiagram || MemberDiagramElementId is not int id)throw new InvalidOperationException("Apri un diagramma dell’asta con risultati disponibili.");
        return new(id,MemberDiagramAllComponents,MemberDiagramInvert,MemberDiagramFill,MemberDiagramStation);
    }
    ModelViewPreset CaptureMemberReportView()=>ReportCapture() with {ResultType=0,Axes=0,Mode=2};
    [RelayCommand] void AddReportMemberDiagram()
    {
        if(!CanComposeReport())return;
        try {AddChapter(new() {Kind=ReportBlockKind.Figure,Title=$"Asta {MemberDiagramElementId} · {SelectedCase} · diagrammi",View=CaptureMemberReportView(),MemberDiagram=CaptureMemberDiagram()});ReportMessage="Diagrammi aggiunti: saranno rigenerati dai campioni importati.";}
        catch(Exception ex){ReportMessage=ex.Message;}
    }
    [RelayCommand] void AddReportVerification()
    {
        if (!CanComposeReport()) return;
        try { AddChapter(new() { Kind = ReportBlockKind.Verification, Title = "Verifiche delle sezioni", SheetId = ReportSheet?.Id, SheetName = ReportSheet?.Name }); }
        catch (Exception ex) { ReportMessage = ex.Message; }
    }
    [RelayCommand] void AddReportSeries()
    {
        if (!CanComposeReport()) return;
        try
        {
            var cases = ReportCases.Where(c => c.Selected).Select(c => c.Value).ToArray();
            var views = ReportViews.Where(v => v.Selected).Select(v => v.Value).ToArray();
            if (views.Length == 0) views = [ReportCapture()];
            if (cases.Length == 0) throw new InvalidDataException("Seleziona almeno un caso o una combinazione.");
            if (ReportChapters.Count + cases.Length * views.Length > 100) throw new InvalidDataException("La serie supera il limite di 100 capitoli. Riduci la selezione.");
            foreach (var view in views) foreach (string name in cases)
                AddChapter(new() { Kind = ReportBlockKind.Figure, Title = $"{view.Name} · {name} · {view.Component}", View = view with { Mode = 2, Representation = view.EffectiveRepresentation, Case = name } });
            ReportMessage = $"Aggiunte {cases.Length * views.Length} figure. Campo e stile derivano da ciascuna vista scelta.";
        }
        catch (Exception ex) { ReportMessage = ex.Message; }
    }
    [RelayCommand] void ReplaceReportFigure()
    {
        if (!CanComposeReport() || SelectedReportChapter?.IsFigure != true) return;
        try { SelectedReportChapter.ReplaceView(SelectedReportChapter.Source.MemberDiagram==null?ReportCapture():CaptureMemberReportView(),SelectedReportChapter.Source.MemberDiagram==null?null:CaptureMemberDiagram()); MarkReportDirty(); ReportMessage = "Campo e impostazioni della figura aggiornati."; }
        catch (Exception ex) { ReportMessage = ex.Message; }
    }
    [RelayCommand] void ShowReportFigure()
    {
        if (!CanComposeReport() || SelectedReportChapter?.Source.View is not {} view) return;
        try
        {
            ModelReportFiles.ValidateReferences(CaptureReport() with { Blocks = [SelectedReportChapter.ToBlock() with { Included = true }] }, Snapshot!);
            ApplyView(view); OpenNavigationCommand.Execute("4");
            if(SelectedReportChapter.Source.MemberDiagram is {} member)
            {
                MemberDiagramElementId=member.Element;SelectElement(member.Element);
                MemberDiagramAllComponents=member.AllComponents;MemberDiagramInvert=member.Invert;
                MemberDiagramFill=member.Fill;MemberDiagramStation=member.Station;ShowMemberDiagram=true;
            }
            ReportMessage = "Vista della figura richiamata. Modifica le impostazioni nelle schede dedicate, poi aggiorna la figura.";
        }
        catch (Exception ex) { ReportMessage = "Figura non richiamata: " + ex.Message; }
    }
    [RelayCommand] void MoveReportChapter(string? direction)
    {
        if (!CanComposeReport() || SelectedReportChapter is not {} chapter) return;
        int index = ReportChapters.IndexOf(chapter), next = index + (direction == "up" ? -1 : 1);
        if (next >= 0 && next < ReportChapters.Count) { ReportChapters.Move(index, next); MarkReportDirty(); }
    }
    [RelayCommand] void RemoveReportChapter()
    {
        if (!CanComposeReport() || SelectedReportChapter is not {} chapter) return;
        chapter.PropertyChanged -= ChapterChanged; ReportChapters.Remove(chapter); SelectedReportChapter = ReportChapters.FirstOrDefault(); MarkReportDirty();
    }
    public ModelReportDocument CaptureReport() => new() { Title = ReportTitle.Trim(), Author = ReportAuthor.Trim(), ModelName = reportModelName,
        SourceRevision = reportRevision, ImageWidth = ReportImageWidth, WhiteBackground = ReportWhiteBackground, Blocks = ReportChapters.Select(c => c.ToBlock()).ToArray() };
    public void ApplyReport(ModelReportDocument document)
    {
        ModelReportFiles.Validate(document);
        foreach (var c in ReportChapters) c.PropertyChanged -= ChapterChanged;
        ReportChapters.Clear(); ReportTitle = document.Title; ReportAuthor = document.Author;
        ReportImageWidth = document.ImageWidth; ReportWhiteBackground = document.WhiteBackground;
        reportRevision = document.SourceRevision; reportModelName = document.ModelName;
        foreach (var block in document.Blocks) AddChapter(block);
        SelectedReportChapter = ReportChapters.FirstOrDefault(); LastReportPdf = null; ReportDirty = false;
        OnPropertyChanged(nameof(ReportRevisionStatus));
    }
    [RelayCommand] void SaveReportRecipe()
    {
        if (!CanComposeReport()) return;
        try { var document = CaptureReport(); ModelReportFiles.Validate(document); if (services.SaveReport(document)) ReportMessage = "Composizione salvata. Puoi riaprirla e rigenerarla sul modello aggiornato."; }
        catch (Exception ex) { ReportMessage = "Salvataggio non completato: " + ex.Message; }
    }
    [RelayCommand] void LoadReportRecipe()
    {
        if (!CanComposeReport()) return;
        try { if (services.OpenReport(ReportDirty) is {} document) { ApplyReport(document); ReportMessage = "Composizione caricata. " + ReportRevisionStatus; } }
        catch (Exception ex) { ReportMessage = "Apertura non completata: " + ex.Message; }
    }
    [RelayCommand(IncludeCancelCommand = true)] async Task BuildReportAsync(CancellationToken cancellationToken)
    {
        if (!CanComposeReport()) return;
        IsReportRunning = true; ReportPercent = 0;
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var document = CaptureReport(); var snapshot = Snapshot!;
            ModelReportFiles.ValidateReferences(document, snapshot);
            var progress = new Progress<ReportProgress>(p => { if (!disposed && IsReportRunning) { ReportPercent = p.Percent; ReportMessage = p.Message; } });
            var result = await services.ExportReportAsync(document, snapshot, progress, linkedToken.Token);
            if (result != null && !disposed)
            {
                reportRevision = result.Revision; reportModelName = snapshot.Name; LastReportPdf = result.PdfPath; ReportDirty = false;
                OnPropertyChanged(nameof(ReportRevisionStatus)); ReportPercent = 100;
                ReportMessage = $"Report generato: {result.Figures} figure. PDF, HTML e composizione salvati insieme.\n{result.PdfPath}";
            }
        }
        catch (OperationCanceledException) { ReportMessage = "Generazione annullata. La composizione resta disponibile."; }
        catch (Exception ex) { ReportMessage = "Report non generato: " + ex.Message; }
        finally { IsReportRunning = false; }
    }
    bool CanOpenReportPdf() => LastReportPdf != null && !ReportDirty && reportRevision == viewRevision;
    [RelayCommand(CanExecute = nameof(CanOpenReportPdf))] void OpenReportPdf()
    {
        try { services.OpenReportOutput(LastReportPdf!); }
        catch (Exception ex) { ReportMessage = "Documento non aperto: " + ex.Message; }
    }
}
