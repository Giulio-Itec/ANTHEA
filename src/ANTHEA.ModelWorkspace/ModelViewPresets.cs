using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

public sealed record ModelCameraState(double[] Position, double[] Look, double[] Up, bool Orthographic, double Width, double FieldOfView);
public sealed record ModelViewPreset
{
    public string Name { get; init; } = "";
    public ModelCameraState? Camera { get; init; }
    public int[]? Visible { get; init; }
    public int[] Selected { get; init; } = [];
    public string? Case { get; init; }
    public string Component { get; init; } = "Myy";
    public string ForceUnit { get; init; } = "kN";
    public string LengthUnit { get; init; } = "m";
    public int Mode { get; init; } = 2;
    public int Family { get; init; }
    public int Axes { get; init; }
    public int Averaging { get; init; }
    public int Scale { get; init; }
    public double Lower { get; init; }
    public double Upper { get; init; }
    public bool Clip { get; init; }
    public int ClipAxis { get; init; }
    public double ClipPercent { get; init; } = 50;
    public bool ClipReverse { get; init; }
    public string ClipNodes { get; init; } = "";
    public bool Mesh { get; init; }
    public bool Outlines { get; init; } = true;
    public double Opacity { get; init; } = 1;
    public int Palette { get; init; }
    public bool ReversePalette { get; init; }
    public int Bands { get; init; }
    public string Background { get; init; } = "#131D2B";
    public override string ToString() => Name;
}

/// <summary>Local UI preferences, scoped to the exact imported revision, separate from engineering data.</summary>
public sealed class ModelViewPresetStore(string directory)
{
    string FileName(string revision)
    {
        if (revision.Length != 64 || revision.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Revisione non valida.");
        return Path.Combine(directory, revision + ".json");
    }
    public ModelViewPreset[] Read(string revision)
    {
        string path = FileName(revision);
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 16_000_000) throw new InvalidDataException("Archivio viste troppo grande.");
        var views = JsonSerializer.Deserialize<ModelViewPreset[]>(File.ReadAllText(path)) ?? [];
        Validate(views); return views;
    }
    public void Write(string revision, IReadOnlyList<ModelViewPreset> views)
    {
        Validate(views); string path = FileName(revision);
        string json=JsonSerializer.Serialize(views);
        if(System.Text.Encoding.UTF8.GetByteCount(json)>16_000_000)throw new InvalidDataException("Archivio viste troppo grande.");
        Directory.CreateDirectory(directory);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try { File.WriteAllText(temporary, json); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    static bool Parallel(double[] a,double[] b)
    {
        double sizeA=a.Max(Math.Abs),sizeB=b.Max(Math.Abs);
        var x=a.Select(v=>v/sizeA).ToArray();var y=b.Select(v=>v/sizeB).ToArray();
        return Enumerable.Range(0,3).All(i=>Math.Abs(x[i]*y[(i+1)%3]-x[(i+1)%3]*y[i])<1e-9);
    }
    static void Validate(IReadOnlyList<ModelViewPreset> views)
    {
        if (views.Count > 100 || views.Any(v => v is null || string.IsNullOrWhiteSpace(v.Name) || v.Name.Length > 80) || views.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != views.Count)
            throw new InvalidDataException("Viste salvate non valide.");
        foreach (var v in views)
        {
            if (v.Selected == null || v.Selected.Length > 500000 || v.Visible?.Length > 500000 || v.Selected.Any(i => i <= 0) || v.Visible?.Any(i => i <= 0) == true ||
                v.Averaging is < 0 or > 1 || v.Palette is < 0 or > 2 || v.Bands is < 0 or > 3 || v.ClipNodes==null || v.Component==null ||
                v.ForceUnit is not ("kN" or "N" or "MN") || v.LengthUnit is not ("m" or "mm" or "cm") ||
                v.Background==null || !System.Text.RegularExpressions.Regex.IsMatch(v.Background,"^#[0-9a-fA-F]{6}$") ||
                v.Scale==3 && v.Lower>=v.Upper ||
                v.Mode is < 0 or > 2 || v.Family is < 0 or > 5 || v.Axes is < 0 or > 1 || v.Scale is < 0 or > 3 || v.ClipAxis is < 0 or > 3 ||
                !double.IsFinite(v.Lower) || !double.IsFinite(v.Upper) || !double.IsFinite(v.ClipPercent) || v.ClipPercent is < 0 or > 100 || v.Opacity is < .1 or > 1 || !double.IsFinite(v.Opacity))
                throw new InvalidDataException("Impostazioni della vista non valide.");
            if (v.Camera is { } c && (new[] { c.Position, c.Look, c.Up }.Any(a => a is null || a.Length != 3 || a.Any(x => !double.IsFinite(x))) ||
                c.Look.All(x => x == 0) || c.Up.All(x => x == 0) || Parallel(c.Look,c.Up) || !double.IsFinite(c.Width) || c.Width <= 0 || !double.IsFinite(c.FieldOfView) || c.FieldOfView is < 1 or > 175))
                throw new InvalidDataException("Camera della vista non valida.");
        }
    }
}
