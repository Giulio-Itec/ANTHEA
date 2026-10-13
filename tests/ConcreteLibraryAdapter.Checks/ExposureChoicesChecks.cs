using System.Collections.Generic;
using Anthea.Calculations;

/// <summary>F2.7 A11: preserve the existing choices and protect the shared catalog from UI mutations.</summary>
internal static class ExposureChoicesChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string[] expected = ["Da scegliere", "X0", "XC1", "XC2", "XC3", "XF1", "XC4", "XD1", "XS1", "XA1", "XA2", "XF2", "XF3", "XD2", "XD3", "XS2", "XS3", "XA3", "XF4"];
        check(ConcreteLibraryMapping.ExposureChoices.SequenceEqual(expected), "7c: ordine o voci delle esposizioni cambiati");
        check(ConcreteLibraryMapping.ExposureChoices.SequenceEqual(Ntc2018Checks.Exposures), "7c: catalogo diverso dal riferimento legacy");
        var choices = (IList<string>)ConcreteLibraryMapping.ExposureChoices;
        bool rejected = false;
        try { choices[0] = "alterato"; } catch (NotSupportedException) { rejected = true; }
        check(rejected, "7c: catalogo condiviso modificabile da IList");
        var copy = ConcreteLibraryMapping.ExposureChoices.ToArray(); copy[1] = "alterato";
        check(ConcreteLibraryMapping.ExposureChoices.SequenceEqual(expected), "7c: una copia della UI modifica il catalogo");
        var legacyCopy = Ntc2018Checks.Exposures.ToArray(); legacyCopy[1] = "alterato";
        check(Ntc2018Checks.Exposures.SequenceEqual(expected), "7c: il riferimento legacy è modificabile");
        check(Anthea.Application.Concrete.ConcreteEditor.ExposureChoices.SequenceEqual(expected), "7c: il contratto applicativo altera il catalogo");
        var applicationCopy = Anthea.Application.Concrete.ConcreteEditor.ExposureChoices; applicationCopy[1] = "alterato";
        check(Anthea.Application.Concrete.ConcreteEditor.ExposureChoices.SequenceEqual(expected), "7c: la vista modifica il catalogo tramite il contratto");
        check(File.ReadAllText(Path.Combine(root, "src/ANTHEA.Application/Concrete/ConcreteEditor.cs")).Contains("ConcreteLibraryMapping.ExposureChoices.ToArray()"), "7c: contratto applicativo senza fonte condivisa");
        foreach (var file in new[] { "X.Desktop/Wpf/ConcreteStress.cs", "X.Desktop/Wpf/HorizontalWorkspace.cs",
            "X.Desktop/Wpf/RetainingWallWorkspace.Materials.cs", "X.Calculations/RetainingWall.cs" })
        {
            string source = File.ReadAllText(Path.Combine(root, file));
            check(source.Contains(file.StartsWith("X.Desktop/") ? "ConcreteEditor.ExposureChoices" : "ConcreteLibraryMapping.ExposureChoices") && !source.Contains("Ntc2018Checks.Exposures"),
                "7c: la vista o validazione usa ancora il catalogo legacy: " + file);
        }
    }
}
