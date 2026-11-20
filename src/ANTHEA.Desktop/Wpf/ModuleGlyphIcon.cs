using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

/// <summary>
/// The icons of the modules that had none (19/11/2026, approved by the user on the board of the proposals): drawn as the other icons of the
/// modules, in 80 × 80, navy for the structures, gold for actions, loads and bars, the bands of the soil. Each icon is a list of paths in the
/// mini-language of the paths (the same strings of the board), with fill, stroke and width of the line; <see cref="Ui.ModuleIcon"/> uses them
/// for these modules only, the icons that already exist do not change.
/// </summary>
internal sealed class ModuleGlyphIcon : FrameworkElement
{
    private const string Navy = "#0B2A4A", Gold = "#C7953E";

    private sealed record Layer(string? Fill, string? Stroke, double Width, string Path);

    /// <summary>The module drawn.</summary>
    internal string Module { get; init; } = "";

    /// <summary>True for the modules with an icon here.</summary>
    internal static bool Supports(string module) => Layers(module).Count > 0;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        foreach (var layer in Layers(Module))
        {
            var pen = layer.Stroke is null ? null
                : new Pen(Ui.Brush(layer.Stroke), layer.Width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(layer.Fill is null ? null : Ui.Brush(layer.Fill), pen, Geometry.Parse(layer.Path));
        }
    }

    private static IReadOnlyList<Layer> Layers(string module) => module switch
    {
        // Palificata orizzontale: tre pali sotto il plinto, l'interazione fra i pali e la forza orizzontale.
        "geo_efficienza_orizzontale" => [
            new("#EDE6DA", null, 0, "M6 46 Q23 41 40 46 T74 46 V56 H6 Z"),
            new("#D9CDB9", null, 0, "M6 56 Q23 51 40 56 T74 56 V64 H6 Z"),
            new("#BFAE93", null, 0, "M6 64 Q23 59 40 64 T74 64 V72 H6 Z"),
            new(Navy, null, 0, "M14 22 H70 V30 H14 Z"),
            new(Navy, null, 0, "M21 30 H27 V62 L24 67 L21 62 Z"),
            new(Navy, null, 0, "M39 30 H45 V62 L42 67 L39 62 Z"),
            new(Navy, null, 0, "M57 30 H63 V62 L60 67 L57 62 Z"),
            new(null, Gold, 2, "M29 46 Q33 50 37 46"),
            new(null, Gold, 2, "M47 46 Q51 50 55 46"),
            new(null, Gold, 3, "M2 26 H9"),
            new(Gold, null, 0, "M9 22 L15 26 L9 30 Z")],
        // Paratie: la paratia fra il terreno a monte e lo scavo, con il tirante.
        "geo_paratie" => [
            new("#EDE6DA", null, 0, "M6 22 H37 V74 H6 Z"),
            new("#D9CDB9", null, 0, "M6 50 H37 V74 H6 Z"),
            new("#EDE6DA", null, 0, "M43 50 H74 V74 H43 Z"),
            new("#D9CDB9", null, 0, "M43 62 H74 V74 H43 Z"),
            new(Navy, null, 0, "M37 10 H43 V74 H37 Z"),
            new(null, Gold, 3, "M37 26 L18 37"),
            new(Gold, null, 0, "M9.5 39 A4.5 4.5 0 1 0 18.5 39 A4.5 4.5 0 1 0 9.5 39 Z")],
        // Viscosit� e maturazione: le curve nel tempo.
        "mat_reologia" => [
            new(null, Navy, 3, "M14 12 V66 H70"),
            new(null, Navy, 2, "M16 64 C30 54 46 48 68 46"),
            new(null, Gold, 3.5, "M16 62 C26 36 40 26 68 22")],
        // Sezione c.a. rapida: la sezione con le barre e il diagramma delle tensioni.
        "str_sezione_composta_rapida" => [
            new("#DAE2E9", Navy, 2, "M10 12 H61 V24 H10 Z"),
            new(Navy, null, 0, "M26 24 H46 V29 H39 V61 H49 V66 H23 V61 H33 V29 H26 Z"),
            new(Gold, null, 0, "M65 22 H76 L65 42 Z M65 42 L77 66 H65 Z")],
        "str_sezione_ca_rapida" => [
            new("#FFFFFF", Navy, 3, "M16 12 H48 V68 H16 Z"),
            new(Gold, null, 0, "M20.8 20 A3.2 3.2 0 1 0 27.2 20 A3.2 3.2 0 1 0 20.8 20 Z"),
            new(Gold, null, 0, "M36.8 20 A3.2 3.2 0 1 0 43.2 20 A3.2 3.2 0 1 0 36.8 20 Z"),
            new(Gold, null, 0, "M20.8 60 A3.2 3.2 0 1 0 27.2 60 A3.2 3.2 0 1 0 20.8 60 Z"),
            new(Gold, null, 0, "M28.8 60 A3.2 3.2 0 1 0 35.2 60 A3.2 3.2 0 1 0 28.8 60 Z"),
            new(Gold, null, 0, "M36.8 60 A3.2 3.2 0 1 0 43.2 60 A3.2 3.2 0 1 0 36.8 60 Z"),
            new("#E9D3A6", Gold, 2, "M54 12 H70 L54 38 Z"),
            new(null, Navy, 3, "M54 60 H66"),
            new(Navy, null, 0, "M66 56 L72 60 L66 64 Z")],
        // Carichi degli edifici: il telaio con il carico distribuito.
        "act_carichi_edifici" => [
            new(null, Gold, 2.5, "M16 12 H64"),
            new(null, Gold, 2.5, "M22 12 V21"),
            new(Gold, null, 0, "M18 21 L22 27 L26 21 Z"),
            new(null, Gold, 2.5, "M34 12 V21"),
            new(Gold, null, 0, "M30 21 L34 27 L38 21 Z"),
            new(null, Gold, 2.5, "M46 12 V21"),
            new(Gold, null, 0, "M42 21 L46 27 L50 21 Z"),
            new(null, Gold, 2.5, "M58 12 V21"),
            new(Gold, null, 0, "M54 21 L58 27 L62 21 Z"),
            new(null, Navy, 3, "M14 30 H66 M14 50 H66 M18 30 V70 M40 30 V70 M62 30 V70"),
            new(null, Navy, 3, "M8 70 H72")],
        // Combinazioni: la somma delle azioni.
        "act_combinazioni" => [
            new(null, Navy, 4.5, "M54 20 V14 H20 L38 40 L20 66 H54 V60"),
            new(Gold, null, 0, "M58 52 h5 v14 h-5 Z"),
            new(Gold, null, 0, "M65 42 h5 v24 h-5 Z"),
            new(Gold, null, 0, "M72 48 h5 v18 h-5 Z")],
        // Effetti differiti: la clessidra.
        "act_effetti_differiti" => [
            new(null, Navy, 4, "M20 12 H60 M20 68 H60"),
            new(null, Navy, 3, "M24 14 C24 30 38 34 38 40 C38 46 24 50 24 66 M56 14 C56 30 42 34 42 40 C42 46 56 50 56 66"),
            new(Gold, null, 0, "M29 24 H51 C49 31 42 34 40 38 C38 34 31 31 29 24 Z"),
            new(Gold, null, 0, "M28 64 C31 56 36 53 40 52 C44 53 49 56 52 64 Z"),
            new(null, Gold, 1.5, "M40 38 V52")],
        // Fasi costruttive: i conci gi� posti e quello successivo.
        "act_fasi_costruttive" => [
            new("#CAD6E0", Navy, 2.5, "M10 52 H30 V68 H10 Z"),
            new("#CAD6E0", Navy, 2.5, "M30 40 H50 V68 H30 Z"),
            new(null, Gold, 3, "M50 32 H70 V68 H50 Z"),
            new(null, Gold, 3, "M60 4 V18"),
            new(Gold, null, 0, "M56 19 L60 25 L64 19 Z"),
            new(null, Navy, 3, "M6 68 H74")],
        // Gru e carriponte: la via di corsa, il carrello e il gancio.
        "act_gru_carriponte" => [
            new(null, Navy, 4, "M14 22 V70 M66 22 V70"),
            new(null, Navy, 5, "M8 22 H72"),
            new(Gold, null, 0, "M32 25 H48 V33 H32 Z"),
            new(null, Navy, 2, "M40 33 V48"),
            new(null, Gold, 3, "M40 48 V55 A5 5 0 1 1 35 55"),
            new(null, Navy, 3, "M8 70 H72")],
        // Azioni idrauliche: l'acqua e la spinta sulla parete.
        "act_idrauliche" => [
            new("#CAD6E0", null, 0, "M8 22 Q14 18 20 22 T32 22 T44 22 L46 22 V70 H8 Z"),
            new(Navy, null, 0, "M46 10 H54 V70 H46 Z"),
            new(null, Gold, 3, "M37 34 H40 M31 46 H40 M25 58 H40"),
            new(Gold, null, 0, "M40 30 L46 34 L40 38 Z"),
            new(Gold, null, 0, "M40 42 L46 46 L40 50 Z"),
            new(Gold, null, 0, "M40 54 L46 58 L40 62 Z"),
            new(null, Navy, 3, "M6 70 H74")],
        // Incendio: la fiamma sotto la trave.
        "act_incendio" => [
            new(Navy, null, 0, "M10 14 H70 V22 H10 Z"),
            new(Gold, null, 0, "M40 70 C24 70 20 56 28 46 C30 52 34 54 34 54 C32 44 38 36 44 30 C44 40 52 44 54 52 C58 62 52 70 40 70 Z"),
            new("#F3E2BF", null, 0, "M40 70 C33 70 31 63 35 57 C36 61 39 62 39 62 C39 56 42 52 45 49 C46 56 50 58 49 63 C48 68 45 70 40 70 Z"),
            new(null, Navy, 3, "M14 72 H66")],
        // Neve: la neve sulla copertura.
        "act_neve" => [
            new("#CAD6E0", Navy, 3, "M20 46 H60 V70 H20 Z"),
            new(null, Navy, 3.5, "M12 46 L40 24 L68 46"),
            new("#FFFFFF", "#9FB2C4", 2, "M12 43 L40 21 L68 43 L68 38 L40 16 L12 38 Z"),
            new(null, Gold, 2, "M20 7 V17 M15.7 9.5 L24.3 14.5 M15.7 14.5 L24.3 9.5"),
            new(null, Gold, 2, "M60 5 V15 M55.7 7.5 L64.3 12.5 M55.7 12.5 L64.3 7.5"),
            new(null, Navy, 3, "M8 70 H72")],
        // Precompressione: il cavo e le forze alle testate.
        "act_precompressione" => [
            new("#CAD6E0", Navy, 3, "M14 28 H66 V52 H14 Z"),
            new(null, Gold, 3, "M17 34 Q40 62 63 34"),
            new(null, Gold, 3, "M2 34 H8"),
            new(Gold, null, 0, "M8 30 L14 34 L8 38 Z"),
            new(null, Gold, 3, "M78 34 H72"),
            new(Gold, null, 0, "M72 30 L66 34 L72 38 Z"),
            new(Navy, null, 0, "M20 52 L16 60 H24 Z"),
            new(Navy, null, 0, "M60 52 L56 60 H64 Z")],
        // Ripartizione del traffico: il carico sull'impalcato e le quote delle travi.
        "act_ripartizione_traffico" => [
            new(Navy, null, 0, "M6 30 H74 V36 H6 Z"),
            new(Navy, null, 0, "M16 36 H22 V54 H16 Z M37 36 H43 V54 H37 Z M58 36 H64 V54 H58 Z"),
            new(null, Gold, 3, "M27 8 V21"),
            new(Gold, null, 0, "M23 22 L27 28 L31 22 Z"),
            new(Gold, null, 0, "M16 60 H22 V74 H16 Z"),
            new(Gold, null, 0, "M37 60 H43 V69 H37 Z"),
            new(Gold, null, 0, "M58 60 H64 V64 H58 Z")],
        // Spettri NTC: lo spettro orizzontale e quello verticale.
        "act_spettri_ntc" => [
            new(null, Navy, 3, "M12 10 V68 H72"),
            new(null, Navy, 1.8, "M14 60 L20 44 H28 C36 54 50 62 70 64"),
            new(null, Gold, 3.5, "M14 50 L24 22 H38 C46 40 54 52 70 58")],
        // Temperatura negli edifici: l'edificio e il termometro.
        "act_temperatura_edifici" => [
            new("#CAD6E0", Navy, 3, "M10 26 H46 V70 H10 Z"),
            new(Navy, null, 0, "M17 34 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M17 46 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M17 58 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M33 34 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M33 46 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M33 58 h6 v6 h-6 Z"),
            new("#FFFFFF", Navy, 2.5, "M58 18 A4 4 0 0 1 66 18 V52 A8 8 0 1 1 58 52 Z"),
            new(Gold, null, 0, "M60.5 30 H63.5 V58 H60.5 Z"),
            new(Gold, null, 0, "M57 59 A5 5 0 1 0 67 59 A5 5 0 1 0 57 59 Z"),
            new(null, Navy, 3, "M6 70 H74")],
        // Temperatura nei ponti: l'impalcato e il termometro.
        "act_temperatura_ponti" => [
            new(Navy, null, 0, "M6 40 H54 V46 H6 Z"),
            new(Navy, null, 0, "M14 46 H18 V70 H14 Z M42 46 H46 V70 H42 Z"),
            new("#FFFFFF", Navy, 2.5, "M62 16 A4 4 0 0 1 70 16 V48 A8 8 0 1 1 62 48 Z"),
            new(Gold, null, 0, "M64.5 28 H67.5 V54 H64.5 Z"),
            new(Gold, null, 0, "M61 55 A5 5 0 1 0 71 55 A5 5 0 1 0 61 55 Z"),
            new(null, Navy, 3, "M6 70 H56")],
        // Traffico sui ponti: il mezzo pesante sull'impalcato.
        "act_traffico_ponti" => [
            new(Navy, null, 0, "M6 48 H74 V54 H6 Z"),
            new(Navy, null, 0, "M18 54 H24 V72 H18 Z M56 54 H62 V72 H56 Z"),
            new(Gold, null, 0, "M14 28 H46 V43 H14 Z"),
            new(Gold, null, 0, "M47 32 H56 L61 38 V43 H47 Z"),
            new(Navy, null, 0, "M18.5 44 A3.5 3.5 0 1 0 25.5 44 A3.5 3.5 0 1 0 18.5 44 Z"),
            new(Navy, null, 0, "M34.5 44 A3.5 3.5 0 1 0 41.5 44 A3.5 3.5 0 1 0 34.5 44 Z"),
            new(Navy, null, 0, "M50.5 44 A3.5 3.5 0 1 0 57.5 44 A3.5 3.5 0 1 0 50.5 44 Z")],
        // Urti veicolari: il veicolo contro la pila.
        "act_urti_veicolari" => [
            new(Navy, null, 0, "M50 10 H60 V64 H50 Z"),
            new(Gold, null, 0, "M6 60 V52 L12 50 L18 43 H30 L36 50 L40 52 V60 Z"),
            new(Navy, null, 0, "M10 60 A4 4 0 1 0 18 60 A4 4 0 1 0 10 60 Z"),
            new(Navy, null, 0, "M28 60 A4 4 0 1 0 36 60 A4 4 0 1 0 28 60 Z"),
            new(null, Gold, 3, "M30 34 H42"),
            new(Gold, null, 0, "M42 30 L48 34 L42 38 Z"),
            new(null, Navy, 3, "M4 64 H76")],
        // Vento sugli edifici: le linee del vento sull'edificio.
        "act_vento_edifici" => [
            new("#CAD6E0", Navy, 3, "M40 18 H68 V70 H40 Z"),
            new(Navy, null, 0, "M46 26 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M46 38 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M46 50 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M57 26 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M57 38 h6 v6 h-6 Z"),
            new(Navy, null, 0, "M57 50 h6 v6 h-6 Z"),
            new(null, Gold, 3, "M6 28 H28 C34 28 34 20 29 20"),
            new(null, Gold, 3, "M6 40 H32"),
            new(null, Gold, 3, "M6 52 H26 C32 52 32 60 27 60"),
            new(null, Navy, 3, "M34 70 H74")],
        // Vento sui ponti: le linee del vento sull'impalcato.
        "act_vento_ponti" => [
            new(Navy, null, 0, "M32 40 H76 V46 H32 Z"),
            new(Navy, null, 0, "M40 46 H44 V70 H40 Z M64 46 H68 V70 H64 Z"),
            new(null, Gold, 3, "M6 30 H28 C34 30 34 22 29 22"),
            new(null, Gold, 3, "M6 42 H32"),
            new(null, Gold, 3, "M6 54 H26 C32 54 32 62 27 62"),
            new(null, Navy, 3, "M30 70 H76")],
        _ => []
    };
}
