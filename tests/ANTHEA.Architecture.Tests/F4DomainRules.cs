using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Syntax candidates, not a proof of engineering semantics. Existing occurrences are individually fingerprinted; additions fail.</summary>
internal static class F4DomainRules
{
    internal sealed record Finding(string File, string Rule, string Owner, string Fingerprint, int Count, string Example);
    internal sealed record Baseline(int Schema, string SourceCommit, string Note, Finding[] Findings);
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    static readonly HashSet<string> Maths = ["Pow", "Sqrt", "Cbrt", "Exp", "Log", "Log2", "Log10", "Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Atan2", "Sinh", "Cosh", "Tanh"];
    static readonly HashSet<double> UnitFactors = [1000, 1e6, 1e9, .001, 1e-6, 1e-9];
    static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview, preprocessorSymbols: ["NET", "NET8_0", "NET8_0_OR_GREATER", "RELEASE"]);

    internal static int Run(string root, string output, IEnumerable<string> sources, bool inventory)
    {
        int checks = SelfTest();
        var all = sources.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        var observed = all.SelectMany(path => Scan(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path))).ToArray();
        File.WriteAllText(Path.Combine(output, "f4-findings.json"), JsonSerializer.Serialize(new Baseline(1, "candidate",
            "Syntax candidates. Engineering, adapter and drawing occurrences still require review; no blanket exemption for a folder.", observed), JsonOptions));
        File.WriteAllText(Path.Combine(output, "f4-counts.json"), JsonSerializer.Serialize(new {
            Files = all.Length, Findings = observed.Sum(f => f.Count),
            Rules = observed.GroupBy(f => f.Rule).ToDictionary(g => g.Key, g => g.Sum(f => f.Count)), SelfTests = checks
        }, JsonOptions));
        if (inventory)
        {
            string message = $"PASS F4 inventory: {all.Length} files, {observed.Sum(f => f.Count)} candidates, {checks} detector checks";
            File.WriteAllText(Path.Combine(output, "esito.txt"), message); Console.WriteLine(message); return checks;
        }
        string path = Path.Combine(root, "tests/ANTHEA.Architecture.Tests/f4-domain-baseline.json");
        var baseline = JsonSerializer.Deserialize<Baseline>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Baseline F4 assente.");
        Require(baseline.Schema == 1 && baseline.SourceCommit.Length == 40 && baseline.Note.Length > 20, "Provenienza baseline F4 non valida.");
        Require(baseline.Findings.Select(Key).Distinct().Count() == baseline.Findings.Length, "Baseline F4 con duplicati.");
        var additions = Additions(baseline.Findings, observed);
        File.WriteAllText(Path.Combine(output, "f4-additions.json"), JsonSerializer.Serialize(additions, JsonOptions));
        Require(additions.Length == 0, "Nuove segnalazioni F4: " + string.Join("; ", additions.Take(12).Select(f => $"{f.File}: {f.Rule} {f.Owner} ({f.Example})")));
        var remaining = observed.ToDictionary(Key, f => f.Count, StringComparer.Ordinal);
        var removed = baseline.Findings.Where(f => remaining.GetValueOrDefault(Key(f)) < f.Count).ToArray();
        File.WriteAllText(Path.Combine(output, "f4-removed.json"), JsonSerializer.Serialize(removed, JsonOptions));
        Require(removed.Length == 0, "Residui F4 rimossi: ridurre anche la baseline nello stesso commit, senza aggiungere eccezioni.");
        Console.WriteLine($"F4: {all.Length} files, {observed.Sum(f => f.Count)} inherited syntax candidates, no additions; {checks} detector checks");
        return checks + 4;
    }

    internal static Finding[] Scan(string file, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, ParseOptions);
        var root = tree.GetCompilationUnitRoot();
        Require(!tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error), "Sintassi non analizzabile: " + file);
        var list = new List<Finding>();
        var mathNames = new HashSet<string>(StringComparer.Ordinal) { "Math", "MathF", "System.Math", "System.MathF", "global::System.Math", "global::System.MathF" };
        bool mathStatic = false;
        foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (u.Name?.ToString() is "System.Math" or "System.MathF")
            {
                if (u.Alias is not null) mathNames.Add(u.Alias.Name.Identifier.ValueText);
                if (u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword)) mathStatic = true;
            }
            if (IsNonWpfLayer(file) && (u.Name?.ToString().StartsWith("System.Windows", StringComparison.Ordinal) == true
                || u.Name?.ToString().StartsWith("Microsoft.Office", StringComparison.Ordinal) == true)) Add("R7-desktop-dependency", u);
        }
        void Add(string rule, SyntaxNode node)
        {
            string normalized = string.Join(" ", node.DescendantTokens().Select(t => t.Text));
            string owner = string.Join(".", node.Ancestors().Reverse().Select(n => n switch {
                BaseTypeDeclarationSyntax t => t.Identifier.ValueText,
                MethodDeclarationSyntax m => m.Identifier.ValueText,
                ConstructorDeclarationSyntax c => c.Identifier.ValueText,
                LocalFunctionStatementSyntax l => l.Identifier.ValueText,
                PropertyDeclarationSyntax p => p.Identifier.ValueText, _ => ""
            }).Where(v => v.Length > 0));
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner + "|" + normalized)));
            list.Add(new(file, rule, owner, fingerprint, 1, normalized.Length > 180 ? normalized[..180] : normalized));
        }
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string name = call.Expression switch { MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText, IdentifierNameSyntax i => i.Identifier.ValueText,
                MemberBindingExpressionSyntax m => m.Name.Identifier.ValueText, _ => "" };
            string receiver = call.Expression is MemberAccessExpressionSyntax member ? member.Expression.ToString() : "";
            var arguments = call.ArgumentList.Arguments;
            if (Maths.Contains(name) && (mathNames.Contains(receiver) || receiver.Length == 0 && mathStatic)) Add("R1-math", call);
            if (name == "D" && arguments.Count > 0) Add("R4-implicit-number", call);
            if (name == "S" && arguments.Count >= 2 && String(arguments[1].Expression) == "NTC 2018"
                || name == "StartsWith" && arguments.Count > 0 && String(arguments[0].Expression) == "NTC") Add("R3-standard-fallback", call);
            if (name is "StartsWith" or "Equals" or "Contains" && arguments.Any(a => EngineeringText(String(a.Expression)))) Add("R5-text-outcome", call);
        }
        foreach (var binary in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (binary.Kind() is SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.ModuloExpression)
            {
                foreach (var side in new[] { binary.Left, binary.Right })
                {
                    if (!Number(side, out double number)) continue;
                    if (number is not (0 or 1 or -1 or 2 or -2 or 100 or -100)) Add("R2-numeric-literal", side);
                    if (UnitFactors.Contains(Math.Abs(number))) Add("R6-unit-factor", side);
                }
            }
            if (binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
                && (EngineeringText(String(binary.Left)) || EngineeringText(String(binary.Right)))) Add("R5-text-outcome", binary);
        }
        if (IsNonWpfLayer(file))
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                if (creation.Type.ToString() is "Window" or "UserControl" or "SheetEditor" or "System.Windows.Window")
                    Add("R7-desktop-dependency", creation);
        return list.GroupBy(Key).Select(g => g.First() with { Count = g.Count() }).OrderBy(Key, StringComparer.Ordinal).ToArray();
    }

    static bool IsNonWpfLayer(string file) => file.StartsWith("X.Core/", StringComparison.Ordinal)
        || file.StartsWith("src/ANTHEA.Presentation/", StringComparison.Ordinal)
        || file.StartsWith("src/ANTHEA.Application/", StringComparison.Ordinal)
        || file.StartsWith("src/ANTHEA.Reports/", StringComparison.Ordinal)
        || file.StartsWith("src/ANTHEA.Serialization/", StringComparison.Ordinal)
        || file.StartsWith("X.Calculations/", StringComparison.Ordinal);
    static string? String(ExpressionSyntax node) => node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) ? literal.Token.ValueText : null;
    static bool EngineeringText(string? value) => value is not null && (value.StartsWith("Soddisfatt", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("Non soddisfatt", StringComparison.OrdinalIgnoreCase) || value is "Satisfied" or "NotSatisfied" or "Verificato" or "Non verificato");
    static bool Number(ExpressionSyntax node, out double value)
    {
        if (node is ParenthesizedExpressionSyntax parentheses) return Number(parentheses.Expression, out value);
        if (node is PrefixUnaryExpressionSyntax unary && unary.Kind() is SyntaxKind.UnaryMinusExpression or SyntaxKind.UnaryPlusExpression
            && Number(unary.Operand, out value)) { if (unary.IsKind(SyntaxKind.UnaryMinusExpression)) value = -value; return true; }
        if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NumericLiteralExpression))
        { value = Convert.ToDouble(literal.Token.Value, CultureInfo.InvariantCulture); return true; }
        value = 0; return false;
    }
    static string Key(Finding value) => value.File + "|" + value.Rule + "|" + value.Owner + "|" + value.Fingerprint;
    internal static Finding[] Additions(Finding[] previous, Finding[] current)
    {
        var allowed = previous.ToDictionary(Key, f => f.Count, StringComparer.Ordinal);
        return current.Where(f => f.Count > allowed.GetValueOrDefault(Key(f))).ToArray();
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    static int SelfTest()
    {
        int checks = 0;
        void Check(bool condition, string message) { Require(condition, message); checks++; }
        const string path = "src/ANTHEA.Application/Probe.cs";
        bool Has(string rule, string expression, string prefix = "") => Scan(path, prefix + " class C { double M(dynamic d, double x) { " + expression + " } }").Any(f => f.Rule.StartsWith(rule, StringComparison.Ordinal));
        Check(Has("R1", "return Math.Sqrt(x);"), "R1 Math non rilevato");
        Check(Has("R1", "return Mth.Pow(x, 2);", "using Mth = System.Math;"), "R1 alias non rilevato");
        Check(Has("R1", "return Sqrt(x);", "using static System.Math;"), "R1 using static non rilevato");
        Check(Has("R2", "return x * .26;"), "R2 coefficiente non rilevato");
        Check(!Has("R2", "return x * 2;"), "R2 fattore elementare");
        Check(Has("R3", "return d.S(\"norma\", \"NTC 2018\");"), "R3 norma di riserva non rilevata");
        Check(Has("R3", "return d.Standard.StartsWith(\"NTC\") ? 1 : 0;"), "R3 prefisso norma non rilevato");
        Check(!Has("R3", "var label = \"NTC 2018\"; return x;"), "R3 etichetta di presentazione");
        Check(Has("R4", "return d.D(\"value\");"), "R4 dato mancante non rilevato");
        Check(Has("R4", "return d.D(\"value\", 0);"), "R4 default esplicito da censire");
        Check(Has("R5", "return d.Status.StartsWith(\"Soddisfatto\") ? 1 : 0;"), "R5 esito testuale non rilevato");
        Check(Has("R5", "return d.Status == \"Satisfied\" ? 1 : 0;"), "R5 uguaglianza testuale non rilevata");
        Check(Has("R6", "return x * 1000;"), "R6 conversione non rilevata");
        Check(Has("R6", "return x / 1e-3;"), "R6 esponente non rilevato");
        Check(!Has("R6", "const double MetresToMillimetres = 1000; return x * MetresToMillimetres;"), "R6 conversione nominata");
        Check(Has("R7", "var w = new Window(); return x;"), "R7 WPF non rilevato");
        Check(Has("R7", "return x;", "using System.Windows.Media;"), "R7 namespace WPF non rilevato");
        Check(!Scan("X.Desktop/Wpf/Probe.cs", "using System.Windows; class C { Window M() => new Window(); }").Any(f => f.Rule.StartsWith("R7")), "R7 WPF nella UI");
        Check(!Has("R1", "// Math.Sqrt(x)\n var label = \"Math.Pow(x, 3)\"; return x;"), "Commenti o stringhe analizzati come codice");
        Check(!Has("R4", "return d.Required(\"value\");"), "Lettura validata marcata come ripiego");
        const string original = "class C { double M(double x) => x * .26; }";
        var before = Scan(path, original);
        Check(Additions(before, Scan(path, original.Replace(".26", ".27"))).Length > 0, "Coefficiente cambiato a conteggio invariato");
        Check(Additions(before, Scan(path, original.Replace("x * .26", "x * .26 + x * .26"))).Length > 0, "Duplicazione non rilevata");
        Check(Additions(before, Scan(path, original.Replace("x * .26", "x"))).Length == 0, "Riduzione rifiutata");
        Check(Additions(before, Scan(path, "class C {\n double M(double x) => x  *  .26; }")).Length == 0, "Formattazione altera impronta");
        Check(Additions(before, Scan("src/ANTHEA.Application/Other.cs", original)).Length > 0, "Nuovo file mascherato dalla baseline");
        bool syntaxError = false; try { Scan(path, "class {"); } catch (InvalidOperationException) { syntaxError = true; }
        Check(syntaxError, "Sorgente non analizzabile ignorato");
        return checks;
    }
}
