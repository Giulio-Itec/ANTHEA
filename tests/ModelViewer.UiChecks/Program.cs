using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using Anthea.Calculations;
using HelixToolkit.Wpf.SharpDX;
using X.Core;

static class Program
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    static object? Call(object obj, string method, params object?[] args) => obj.GetType().GetMethod(method, Flags)!.Invoke(obj, args);
    static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    static void Wait(int milliseconds)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Capture(FrameworkElement root, Viewport3DX viewport, string path)
    {
        // Capture our own WPF tree and its native Direct3D render, never the operating-system desktop.
        string render = Path.ChangeExtension(path, ".viewport.png"); viewport.SaveScreen(render);
        var scene = new BitmapImage(); scene.BeginInit(); scene.CacheOption = BitmapCacheOption.OnLoad; scene.UriSource = new Uri(render); scene.EndInit(); scene.Freeze();
        root.UpdateLayout(); int width = (int)Math.Ceiling(root.ActualWidth), height = (int)Math.Ceiling(root.ActualHeight);
        var surface = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); surface.Render(root);
        var visual = new DrawingVisual(); using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(surface, new Rect(0, 0, width, height));
            var point = viewport.TransformToAncestor(root).Transform(new Point());
            drawing.DrawImage(scene, new Rect(point.X, point.Y, viewport.ActualWidth, viewport.ActualHeight));
        }
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); target.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(target)); using var output = File.Create(path); png.Save(output);
        Check(scene.PixelWidth > 200 && scene.PixelHeight > 200 && new FileInfo(render).Length > 5000, "renderer produced a nonempty image: " + Path.GetFileName(path));
    }
    static void CapturePage(FrameworkElement root, string path)
    {
        root.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(root);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(path); png.Save(output);
    }
    [STAThread]
    static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "supporto/artefatti/model-viewer/ui"); Directory.CreateDirectory(output);
        var bindingLog = new StringWriter(); var listener = new TextWriterTraceListener(bindingLog);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        X.Desktop.MainWindow? window = null;
        try
        {
            var app = new TestApp(); app.LoadStyles(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.Appearance")!.GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [false]);
            string archive;
            if (args.Length > 1) archive = Path.GetFullPath(args[1]);
            else
            {
                var doc = ProjectDocuments.CreateArchive(); var p = ProjectDocuments.AddProject(doc, "Progetto UI"); var sf = ProjectDocuments.AddSection(p, "Sottofase UI");
                ProjectDocuments.AddSheet(sf, "str_palo", "Verifica UI"); ProjectModelStore.Set(sf, Sample.Model());
                archive = Path.Combine(output, "ui.programma"); Archivio.Scrivi(archive, doc);
            }
            window = new X.Desktop.MainWindow { Width = 1600, Height = 990, ShowInTaskbar = false };
            Call(window, "LoadFile", archive); window.Show(); Wait(150);
            var document = (JsonObject)window.GetType().GetField("document", Flags)!.GetValue(window)!;
            var owner = document.Array("progetti").OfType<JsonObject>().SelectMany(ProjectModelStore.Containers).First(c => ProjectModelStore.Models(c).Any());
            string id = ProjectModelStore.Models(owner).First().S("id");
            if (args.Contains("--viewer-only"))
            {
                // Host the actual production control on its own, to review the view without project chrome.
                var targets = ProjectModelStore.DescendantSheets(owner).Select(s => new SheetTarget(s.S("id"), s.S("nome"), ProjectModelStore.LinkStatus(owner, s))).ToArray();
                using var isolated = new ModelViewerControl(ProjectModelStore.Read(owner, id), targets, false,
                    imported => ProjectModelStore.Set(owner, imported, id),
                    (sheetId, element, result, component) => ProjectModelStore.Link(owner,
                        ProjectModelStore.DescendantSheets(owner).Single(s => s.S("id") == sheetId), id, element, result, component));
                window.Close(); window = null;
                var preview = new Window { Title = "ANTHEA · Modelli", Content = isolated, Width = 1600, Height = 1020, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                preview.Show(); Wait(1200);
                var state = (ModelViewerViewModel)isolated.DataContext;
                var scene = Children<Viewport3DX>(isolated).Single();
                var realRenderer = isolated.GetType().GetField("renderer", Flags)!.GetValue(isolated)!;
                bool picked = false;
                for (int y = 2; y < 9 && !picked; y++)
                    for (int x = 2; x < 9 && !picked; x++)
                        foreach (var hit in scene.FindHits(new Point(scene.ActualWidth * x / 10, scene.ActualHeight * y / 10)))
                            if (Call(realRenderer, "ElementAt", hit) is int hitId && state.Snapshot!.Elements.Any(e => e.Id == hitId)) { picked = true; break; }
                Check(picked, "ray picking resolves a rendered triangle to its source element");
                var frameTimes = new List<double>();
                foreach (string component in new[] { "Mxx", "Myy", "Fxx", "Fyy", "Mxy", "Vxx" })
                {
                    var frame = new DispatcherFrame(); var watch = Stopwatch.StartNew(); bool rendered = false;
                    EventHandler completed = (_, _) => { if (!rendered) { watch.Stop(); rendered = true; frame.Continue = false; } };
                    scene.OnRendered += completed;
                    state.SelectedComponent = component;
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start();
                    if (!rendered) Dispatcher.PushFrame(frame);
                    timer.Stop(); scene.OnRendered -= completed;
                    Check(rendered, "updated field produces a rendered frame: " + component);
                    frameTimes.Add(watch.Elapsed.TotalMilliseconds);
                }
                File.WriteAllText(Path.Combine(output, "tempi-campi.json"), System.Text.Json.JsonSerializer.Serialize(new { Scope = "Cambio componente con dati caricati, fino a Viewport.OnRendered; esclusi acquisizione, solver e costruzione inviluppo", Milliseconds = frameTimes, Median = frameTimes.Order().Skip(2).Take(2).Average(), Maximum = frameTimes.Max() }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                state.SelectedComponent = "Myy";
                foreach (var (mode, name) in new[] { (2, "modelli-risultati"), (1, "modelli-volumi"), (0, "modelli-mesh") })
                {
                    state.SelectedMode = mode; Wait(500);
                    Check(state.Status.StartsWith("Scena pronta"), "isolated viewer renders mode " + mode);
                    Capture(isolated, scene, Path.Combine(output, name + ".png"));
                }
                state.SelectedPanel = 0;
                state.SelectedTreeItem = state.FindTreeItem("SECT:" + state.Snapshot!.Sections[0].Id);
                state.SelectedMode = 1; Wait(400);
                Check(state.IsSectionSelected && state.SectionWidth > 0 && state.SectionHeight > 0, "section selection exposes its scaled preview");
                Capture(isolated, scene, Path.Combine(output, "modelli-sezioni.png"));
                state.IsolateCommand.Execute(null); Wait(250);
                Check(state.VisibleElementIds?.Count == state.SelectedElementIds.Count, "isolation matches the section membership");
                state.ShowAllCommand.Execute(null); state.ClearSelectionCommand.Execute(null);
                if (state.Snapshot.LocalFrames?.Length > 0)
                {
                    state.SelectedMode = 0; state.SelectedTreeItem = state.FindTreeItem("THIK:3"); state.ShowLocalAxes = true; state.ShowConstraints = true; state.ShowRestraints = true; Wait(450);
                    Check(state.Status.StartsWith("Scena pronta") && !state.Status.Contains("non importate"), "imported local axes and boundary overlays render");
                    Capture(isolated, scene, Path.Combine(output, "modelli-assi-vincoli.png"));
                    state.ShowLocalAxes = false; state.ShowConstraints = false; state.ShowRestraints = false; state.ClearSelectionCommand.Execute(null);
                    state.SelectedMode = 2; state.SelectedAxes = 1; Wait(350);
                    Check(state.HasResults && state.SelectedComponent == "Mmax" && state.Status.StartsWith("Scena pronta"), "principal components use imported values");
                    Capture(isolated, scene, Path.Combine(output, "modelli-principali.png"));
                    state.SelectedAxes = 0; state.SelectedFamily = 1; state.SelectedComponent = "My"; Wait(350);
                    Check(state.HasResults && state.ActiveValues.Count == 905 && state.Status.StartsWith("Scena pronta"), "beam results render all five imported stations");
                    Capture(isolated, scene, Path.Combine(output, "modelli-beam.png"));
                    state.SelectedBeamLocation = 3; state.ForceUnit = "N"; state.LengthUnit = "mm"; Wait(350);
                    Check(state.ActiveValues.Count == 181 && state.ResultUnit == "My [N·mm]", "beam midspan and engineering units update the real field");
                    state.SelectedFamily = 0; state.SelectedAxes = 0; state.SelectedComponent = "Myy"; state.ForceUnit = "kN"; state.LengthUnit = "m";
                }
                state.SelectedPanel = 2; state.QueryCommand.Execute(null); Wait(300);
                Capture(isolated, scene, Path.Combine(output, "modelli-verifiche.png"));
                state.SelectedMode = 0; state.SelectedColorMode = 3; state.ShowDisplayOptions = true; state.SelectedLabelScope = 1;
                state.SelectedTreeItem = state.FindTreeItem("SECT:" + state.Snapshot.Sections[0].Id);
                state.ShowElementIds = true; state.ShowNodeIds = true; state.ShowPropertyIds = true;
                state.IsolateCommand.Execute(null); state.ShowHiddenWireframe = true; Wait(450);
                Check(state.Status.StartsWith("Scena pronta") && scene.Items.OfType<BillboardTextModel3D>().Any(), "IDs and inactive wireframe render with property colors");
                Capture(isolated, scene, Path.Combine(output, "modelli-id-isolamento.png"));
                state.ShowAllCommand.Execute(null); state.ClearSelectionCommand.Execute(null); state.ShowElementIds = state.ShowNodeIds = state.ShowPropertyIds = false;
                state.SelectedColorMode = 4; state.ShowDisplayOptions = false; state.ShowTable = true; state.SelectedTableKind = 1; Wait(450);
                Check(state.TableRows?.Count == state.Snapshot.Elements.Length, "element table is backed by stored elements");
                state.SelectedTableRow = state.TableRows![0]; Wait(250);
                Check(state.SelectedElementIds.Contains((int)state.TableRows[0]["ID"]), "table row selects the corresponding 3D element");
                Capture(isolated, scene, Path.Combine(output, "modelli-gruppi-tabella.png"));
                state.SelectedMode = 2; state.SelectedTableKind = 5; Wait(450);
                Check(state.TableRows!.Count == state.ActiveValues.Count, "result table and scene share the same values and units");
                Capture(isolated, scene, Path.Combine(output, "modelli-risultati-tabella.png"));
                state.ShowTable = false; state.SelectedMode = 0; state.SelectedSurfaceMode = 2; Wait(250);
                Check(state.Status.StartsWith("Scena pronta") && !scene.Items.OfType<MeshGeometryModel3D>().Any(m => m.Geometry?.Indices?.Count > 0), "wireframe mode contains no filled faces");
                // Export through the production compositor, including the WPF legend and current field title.
                typeof(ModelViewerControl).Assembly.GetType("ANTHEA.ModelViewer.Wpf.ViewerImageExporter")!.GetMethod("Save", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, [isolated, scene, Path.Combine(output, "modelli-export.png")]);
                Check(new FileInfo(Path.Combine(output, "modelli-export.png")).Length > 10000, "production image export includes the complete view");
                state.SelectedSurfaceMode = 0; state.SelectedMode = 2; state.ShowTable = true; Wait(300);
                var appearance = typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.Appearance")!;
                var modeType = typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.AppAppearance")!;
                appearance.GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Enum.Parse(modeType, "Dark"), false]); Wait(300);
                var caption = Children<TextBlock>(isolated).First(t => t.Text == "Caso / combinazione");
                Check(caption.Foreground is SolidColorBrush ink && ink.Color.R > 150 && ink.Color.G > 150, "dark theme keeps field captions readable");
                var palette = (LinearGradientBrush)isolated.FindResource("ModelContourPalette");
                Check(palette.GradientStops[4].Color == Color.FromRgb(232, 64, 41), "dark theme preserves the contour legend palette");
                var familyBox = Children<ComboBox>(isolated).First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Famiglia degli elementi");
                familyBox.IsDropDownOpen = true; Wait(150);
                Check(familyBox.IsDropDownOpen && familyBox.Items.Count == 6, "family selector opens all supported formulations");
                familyBox.IsDropDownOpen = false; Wait(100);
                Capture(isolated, scene, Path.Combine(output, "modelli-tema-scuro.png"));
                appearance.GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Enum.Parse(modeType, "Light"), false]);
                listener.Flush(); Check(string.IsNullOrWhiteSpace(bindingLog.ToString()), "no WPF binding errors in isolated viewer");
                File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} checks");
                preview.Close(); app.Shutdown(); return 0;
            }
            Call(window, "ShowModelsHub"); Wait(100);
            Check(Children<Button>(window).Any(b => b.Content?.ToString() == ProjectModelStore.Models(owner).First().S("nome")), "Models workspace exposes imported resources");
            CapturePage((FrameworkElement)window.Content, Path.Combine(output, "00-modelli.png"));
            if (args.Contains("--models-hub-only"))
            {
                listener.Flush(); Check(string.IsNullOrWhiteSpace(bindingLog.ToString()), "no WPF binding errors in Models workspace");
                window.Close(); app.Shutdown(); return 0;
            }
            Call(window, "ShowContainerModel", owner, id); Wait(1200);
            var view = Children<ModelViewerControl>(window).Single(); var vm = (ModelViewerViewModel)view.DataContext; var viewport = Children<Viewport3DX>(view).Single();
            vm.SelectedColorMode = 3; // Keep section and plate-property meshes distinct while checking physical offsets.
            var timings = new List<string>();
            foreach (var (mode, name) in new[] { (0, "01-geometria"), (1, "02-volumi-offset"), (2, "03-risultati-contour") })
            {
                var watch = Stopwatch.StartNew(); vm.SelectedMode = mode; watch.Stop();
                Wait(650); Check(vm.Status.StartsWith("Scena pronta"), "scene ready in mode " + mode);
                Check(viewport.Items.OfType<MeshGeometryModel3D>().Any(m => m.Geometry?.Indices?.Count > 0), "visible mesh in mode " + mode);
                if (args.Length == 1 && mode == 1)
                {
                    var solid = viewport.Items.OfType<MeshGeometryModel3D>().First().Geometry!.Positions!;
                    Check(Math.Abs(solid.Min(p => p.Z) + .85) < 1e-6 && Math.Abs(solid.Max(p => p.Z) + .55) < 1e-6,
                        "rendered plate faces are at offset minus/plus half thickness, not at the analytical plane");
                }
                if (args.Length == 1 && mode == 2)
                {
                    var mesh = (HelixToolkit.SharpDX.MeshGeometry3D)viewport.Items.OfType<MeshGeometryModel3D>().First().Geometry!;
                    int center = Enumerable.Range(0, mesh.Positions!.Count).First(i => Math.Abs(mesh.Positions[i].X + 1) < 1e-6 && Math.Abs(mesh.Positions[i].Y) < 1e-6);
                    Check(Math.Abs(mesh.TextureCoordinates![center].X - .9375) < 1e-6,
                        "quad center uses bilinear value 25, preserving the independent corner values");
                }
                timings.Add(name + ": " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms state+scene; " + vm.Status);
                Capture((FrameworkElement)window.Content, viewport, Path.Combine(output, name + ".png"));
            }
            vm.SelectedQuality = 2; Wait(500); Check(vm.Status.StartsWith("Scena pronta"), "maximum contour quality renders");
            vm.SelectedComponent = "Fxx"; Wait(300); Check(vm.ResultUnit.Contains("kN/m"), "result component selection updates unit label");
            vm.QueryCommand.Execute(null); Check(vm.SelectionInformation.Contains("Spessore"), "WPF view binds query data from the ViewModel");
            Call(window, "ShowModelsHub"); Wait(100);
            Check(!vm.ImportCommand.CanExecute(null), "leaving viewer disposes its ViewModel and disables late imports");
            Check(viewport.EffectsManager == null, "leaving viewer releases GPU manager");
            Call(window, "ShowContainerModel", owner, id); Wait(500);
            Check(Children<ModelViewerControl>(window).Single().DataContext != vm, "reopening creates fresh view state and render resources");
            var sheet = ProjectModelStore.DescendantSheets(owner).First();
            Call(window, "ShowSheet", sheet); Wait(250);
            Check(!Children<ModelViewerControl>(window).Any(), "opening a verification sheet removes the viewer from the WPF tree");
            if (sheet[ProjectModelStore.Binding] != null)
                Check(Children<TextBlock>(window).Any(t => t.Text.Contains("Riferimento aggiornato")), "linked verification sheet displays its model revision status");
            Call(window, "ShowContainerModel", owner, id); Wait(250);
            listener.Flush(); Check(string.IsNullOrWhiteSpace(bindingLog.ToString()), "no WPF binding errors");
            File.WriteAllLines(Path.Combine(output, "tempi.txt"), timings); File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} checks");
            window.Close(); app.Shutdown(); return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex); File.WriteAllText(Path.Combine(output, "errore.txt"), ex + "\n" + bindingLog); window?.Close(); return 1;
        }
    }
}

sealed class TestApp : Application
{
    internal void LoadStyles()
    {
        var document = System.Xml.Linq.XDocument.Load("X.Desktop/App.xaml"); var dictionary = document.Root!.Elements().Single().Elements().Single();
        dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
        dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "local", "clr-namespace:X.Desktop;assembly=ANTHEA");
        foreach (var source in dictionary.Descendants().SelectMany(e => e.Attributes("Source"))) source.Value = "/ANTHEA;component/" + source.Value;
        Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString());
    }
}
