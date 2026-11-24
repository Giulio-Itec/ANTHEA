using System.IO;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Application.Documents;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;
using X.Desktop.Views.Shell;
using OleData = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace X.Desktop;

internal static class QuickSectionTransferWpfChecks
{
    internal static async Task Run(string directory, Action<bool, string> check)
    {
        foreach (string module in new[] { SheetLayout.QuickSectionModule, SheetLayout.QuickCompositeModule })
        {
            var sourceServices = new ShellDraftChecks.DraftServices();
            var targetServices = new ShellDraftChecks.DraftServices();
            var (source, sourceMode) = ModuleAloneWpfChecks.Compose(module, sourceServices);
            var (target, targetMode) = ModuleAloneWpfChecks.Compose(module, targetServices);
            bool sourceClosed = false;
            source.Window.Closed += (_, _) => sourceClosed = true;
            try
            {
                sourceMode.Start(source, StartupRequest.Parse(["--modulo", module]));
                targetMode.Start(target, StartupRequest.Parse(["--modulo", module]));
                await Idle(target.Window);
                var moving = source.Shell.Workspace.Active!;
                var movingTab = source.Shell.Windows.TabOf(moving)!;
                var destination = target.Shell.Workspace.Active!;
                var destinationTab = target.Shell.Windows.TabOf(destination)!;
                moving.Sheet["nome"] = "Sezione Ω – à";
                string sourcePath = Path.Combine(directory, module + "-origine-trasferimento.programma");
                sourceServices.NextSavePath = sourcePath;
                source.Shell.Workspace.Save(moving.Owner, false);
                byte[] original = File.ReadAllBytes(sourcePath);
                string payload = source.Shell.Windows.PrepareSectionDrag(movingTab)!;

                // Force the native IDataObject/HGLOBAL conversion used across processes; no clipboard or simulated CLR serialization.
                var data = ShellTabInput.SectionData(payload, module);
                var native = new DataObject(new NativeData((OleData)data));
                string? received = ShellTabInput.ReadSection(native);
                check(received == payload && ShellTabInput.ReadSection(native) == payload && !native.GetDataPresent(DataFormats.UnicodeText),
                    module + ": dati completi e Unicode attraversano OLE, rilettura stabile e nessun formato testo generico");
                var window = Application.Current.Windows.OfType<DetachedShellWindow>().Single(w => ReferenceEquals(w.DataContext, destinationTab.Host));
                var header = Descendants(window).OfType<FrameworkElement>().Single(e => ShellTabInput.GetIsEnabled(e) && ReferenceEquals(e.DataContext, destinationTab));
                check(ShellTabInput.Over(header, null, 1, module) == DragDropEffects.Move
                    && ShellTabInput.CompleteExternalDrop(header, received, 1), module + ": rilascio esterno sul titolo della sezione accettato");
                await Idle(window);
                check(!moving.IsDisposed && destinationTab.Host!.Tabs.Count == 2 && !target.Window.IsVisible
                    && target.Shell.Windows.Main.Tabs.Count == 0, module + ": destinazione completa prima della rimozione, ANTHEA resta nascosta");
                check(JsonNode.DeepEquals(moving.Sheet["dati"], target.Shell.Workspace.Active!.ViewModel.Data)
                    && target.Shell.Workspace.Active.Sheet["nome"]!.ToString() == "Sezione Ω – à",
                    module + ": editor reale con tutti i dati, combinazioni, impostazioni e nome invariati");
                source.Shell.Windows.CompleteSectionDrag(movingTab, payload);
                await Idle(source.Window);
                check(sourceClosed && source.Tracker.Hosts.Count == 0 && moving.IsDisposed && !target.Window.IsVisible
                    && window.IsVisible && target.Tracker.Hosts.Count == 1,
                    module + ": dopo il trasferimento dell’ultima sezione si chiude solo l’istanza di origine");
                string path = Path.Combine(directory, module + "-destinazione-trasferimento.programma");
                targetServices.NextSavePath = path;
                target.Shell.Workspace.Save(destination.Owner, false);
                check(QuickSectionDocuments.Sections(DocumentService.Open(path)).Count == 2 && File.ReadAllBytes(sourcePath).SequenceEqual(original)
                    && sourceServices.Failures.Count == 0 && targetServices.Failures.Count == 0,
                    module + ": raccolta unita salvata, file originale conservato, nessun errore");
            }
            finally
            {
                if (!sourceClosed) source.Window.Close();
                target.Window.Close();
                source.Shell.Dispose(); target.Shell.Dispose();
                await Idle(target.Window);
            }
        }
    }

    private static async Task Idle(Window window)
    {
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var inner in Descendants(child)) yield return inner;
        }
    }

    private sealed class NativeData(OleData source) : OleData
    {
        public void GetData(ref FORMATETC format, out STGMEDIUM medium) => source.GetData(ref format, out medium);
        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => source.GetDataHere(ref format, ref medium);
        public int QueryGetData(ref FORMATETC format) => source.QueryGetData(ref format);
        public int GetCanonicalFormatEtc(ref FORMATETC format, out FORMATETC output) => source.GetCanonicalFormatEtc(ref format, out output);
        public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release) => source.SetData(ref format, ref medium, release);
        public IEnumFORMATETC EnumFormatEtc(DATADIR direction) => source.EnumFormatEtc(direction);
        public int DAdvise(ref FORMATETC format, ADVF flags, IAdviseSink sink, out int connection) => source.DAdvise(ref format, flags, sink, out connection);
        public void DUnadvise(int connection) => source.DUnadvise(connection);
        public int EnumDAdvise(out IEnumSTATDATA? enumerator) => source.EnumDAdvise(out enumerator);
    }
}
