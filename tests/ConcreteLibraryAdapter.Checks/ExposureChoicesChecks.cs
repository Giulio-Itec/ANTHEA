using System.Collections.Generic;
using Anthea.Calculations;

/// <summary>F2.7 A11: preserve the existing choices and protect the shared catalog from UI mutations.</summary>
internal static class ExposureChoicesChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string[] expected = ["Da scegliere", "X0", "XC1", "XC2", "XC3", "XF1", "XC4", "XD1", "XS1", "XA1", "XA2", "XF2", "XF3", "XD2", "XD3", "XS2", "XS3", "XA3", "XF4"];
        check(ConcreteLibraryMapping.ExposureChoices.SequenceEqual(expected), "7c: ordine o voci delle esposizioni cambiati");
        check(ConcreteLibraryMapping.ExposureChoices.SequenceEqual(LegacyNtc2018Checks.Exposures), "7c: catalogo diverso dal riferimento legacy");
        var choices = (IList<string>)ConcreteLibraryMapping.ExposureChoices;
        bool rejected = false;
        try { choices[0] = "alterato"; } catch (NotSupportedException) { rejected = true; }
        check(rejected, "7c: catalogo condiviso modificabile da IList");
        var copy = ConcreteLibraryMapping.ExposureChoices.ToArray(); copy[1] = "alterato";
        check(ConcreteLibraryMapping.ExposureChoices.SequenceEqual(expected), "7c: una copia della UI modifica il catalogo");
        var legacyCopy = LegacyNtc2018Checks.Exposures.ToArray(); legacyCopy[1] = "alterato";
        check(LegacyNtc2018Checks.Exposures.SequenceEqual(expected), "7c: il riferimento legacy è modificabile");
        check(Anthea.Application.Concrete.ConcreteEditor.ExposureChoices.SequenceEqual(expected), "7c: il contratto applicativo altera il catalogo");
        var applicationCopy = Anthea.Application.Concrete.ConcreteEditor.ExposureChoices; applicationCopy[1] = "alterato";
        check(Anthea.Application.Concrete.ConcreteEditor.ExposureChoices.SequenceEqual(expected), "7c: la vista modifica il catalogo tramite il contratto");
        check(File.ReadAllText(Path.Combine(root, "src/ANTHEA.Application/Concrete/ConcreteEditor.cs")).Contains("ConcreteLibraryMapping.ExposureChoices.ToArray()"), "7c: contratto applicativo senza fonte condivisa");
        // F5.7 and F5.9: the fields of the horizontal pile and of the wall (exposure included) moved from RetainingWallWorkspace.Materials.cs to RetainingWallFields, shared by
        // the historic view and its ViewModel; the same check follows them.
        // F5.10 (passaggio della Release, 23/10/2026): the families of the serviceability of the RC section are in its ViewModel; the historic
        // ConcreteStress.cs is archived in supporto/SUPERATI/ui-storica/sezione-ca.
        foreach (var file in new[] { "src/ANTHEA.Presentation/Modules/ConcreteSection/ConcreteStressPanelViewModel.cs", "src/ANTHEA.Presentation/Modules/HorizontalPile/HorizontalPileFields.cs",
            "src/ANTHEA.Presentation/Modules/RetainingWall/RetainingWallFields.cs", "X.Calculations/RetainingWall.cs" })
        {
            string source = File.ReadAllText(Path.Combine(root, file));
            check(source.Contains(file.StartsWith("X.Calculations/") ? "ConcreteLibraryMapping.ExposureChoices" : "ConcreteEditor.ExposureChoices") && !source.Contains("LegacyNtc2018Checks.Exposures"),
                "7c: la vista o validazione usa ancora il catalogo legacy: " + file);
        }
    }
}
