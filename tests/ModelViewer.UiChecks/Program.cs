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
                foreach (var (mode, name) in new[] { (2, "modelli-risultati"), (1, "modelli-volumi"), (0, "modelli-mesh") })
                {
                    state.SelectedMode = mode; Wait(500);
                    Check(state.Status.StartsWith("Scena pronta"), "isolated viewer renders mode " + mode);
                    Capture(isolated, scene, Path.Combine(output, name + ".png"));
                }
                listener.Flush(); Check(string.IsNullOrWhiteSpace(bindingLog.ToString()), "no WPF binding errors in isolated viewer");
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
