using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.FoundationWorkspace;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.PileGroup;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Modules.ShallowFoundation;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.ShallowFoundation;

namespace X.Desktop;

/// <summary>
/// --check-fondazioni-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.9b, proposal F5 section 7.3): the XAML view of the shallow
/// foundations (ShallowFoundationSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the window opens it with the MVVM implementation,
/// in windows outside the screen (never activated, no mouse). Parts:
/// <list type="number">
/// <item>Traces through the view: the 15 scenarios of tests/Shared/ShallowFoundationTrace.cs driven through the controls of the view
/// (text boxes, choices and check boxes of the FieldForms, combo boxes of the index choices, expanders, buttons, cells of the layers,
/// section of the mechanism) and read back from the controls, equal line by line to the traces of the historic view frozen in
/// tests/fixtures/f5-fondazioni/storica.</item>
/// <item>For each of the 11 F3 fixtures of the module: the visit (tabs, expanders without state, three appearances) writes nothing and
/// raises no Modified; data at the opening equal to those of the frozen trace of the historic view; the visit of the F3 capture (every
/// expander opened) writes vista_fondazioni as the historic view did; after «Calcola» data, result and the display lists of the technical
/// drawings are those the historic view gave (recorded); the exports (report Word of FoundationReport.Word, PDF of MigraDoc beside it,
/// JSON of the result) are the files the historic view wrote (decision T8).</item>
/// <item>Screenshots of the view in Chiara, Scuro and Molto scuro (folder schermate).</item>
/// <item>Help (FN9, as today): no «?» label, as in the historic view; F1 on the fields, in the table and elsewhere and «Guida» open the guide
/// of the module through the IWikiNavigator of the services, also in a read-only revision.</item>
/// <item>The controls that FoundationWorkspace.UiChecks finds in the historic view (the field Width, the button «raccogli-input», six
/// graphic tabs, the table of the layers) and its checks of the layout, on the new view; AutomationProperties.Name of the historic view
/// kept by the new one.</item>
/// <item>Read-only revision: the same controls refuse the edits as in the historic view; groups, «Raccogli input», tabs, report and
/// «Calcola» stay usable; the section of the mechanism is refused (FN4).</item>
/// <item>«Invia / carica terreno…» through ISoilTransferDialog (T5): commit, module, data and soil sheets passed to the dialog, copy and
/// replacement sent to the window; the interactive bench.</item>
/// </list>
/// The historic view is archived (f5-pass-geo, Q11: supporto/SUPERATI/ui-storica/fondazioni): what the checks compared with it is compared with the values it gave,
/// frozen in its traces or recorded here (<see cref="HistoricFixtures"/>, <see cref="HistoricDrawings"/>, <see cref="HistoricAutomationNames"/>,
/// <see cref="HistoricEditable"/>, <see cref="HistoricReadOnly"/>, <see cref="HistoricNoResult"/>; no «?» label).
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static class ShallowFoundationViewChecks
{
    // ---------------------------------------------------------------- values of the historic view (archived with the Release switch)
    // Recorded by this check on the historic view before its archiving: the capture run on db7ef68d with the values logged
    // (supporto/artefatti/ci/20261023-124229-geo-cattura-3 of the worktree aw-f5-pass-geo), equal to the checks of every run up to the one of
    // the release 1.13.0 (supporto/artefatti/ci/20261023-013037-full-fine-ca-1.13.0 of the main checkout).

    /// <summary>The messages of SheetEditor.ExportResult and ExportReport of the shallow foundations without a result.</summary>
    private const string HistoricNoResult = "Premere Calcola prima di esportare.", HistoricNoReport = "Calcolare prima di esportare.";

    /// <summary>
    /// What the historic view gave for each F3 fixture after the visit of the F3 capture (groups opened) and «Calcola»: the Modified events,
    /// SHA-256 of the data (J.Options, which «Calcola» does not change), whether it had a result and SHA-256 of the result ("-" without).
    /// </summary>
    // 24/10/2026, librerie GPC da origin/master (AG-FON-01): l'impronta del risultato delle 8 fixture con un risultato cambia solo per
    // library_version 0.19.0.0 -> 0.19.0.1 (GPCChecker.Geotechnics ricompilata, algoritmi invariati); con 0.19.0.0 tornano le impronte storiche.
    private static readonly Dictionary<string, (int Modified, string Data, bool HasResults, string Result)> HistoricFixtures = new(StringComparer.Ordinal)
    {
        ["confirmed-actions"] = (0, "E8C8939775578BBB33D5EAB88C1D346C006BD8F3BC820119D0028A7B611C6CBC", true, "45AB778061E6A562CFF77F5B9E8640781AF884806E0B42040A7A48B25ABFFCE2"),
        ["default"] = (0, "FD745ADB9F5D8CFD1DE449863BE9F9F0779F953BA53E95D6D711ED5CA6123B64", false, "-"),
        ["ec7-fuori-dominio"] = (0, "CEE1ADB72FF04EC044679866AC1A04BAD15C3750FF678400145816D4F81F74EA", true, "48E511FD99128F4ABA26AFFE8A3FBF0BC6CB46F55B681C25AD03ED6A025A9C13"),
        ["hansen-non-drenato-caratteristico"] = (0, "58461F5ADC2616891086A78F53B71B7BFA5E6B91F56B1A9B69F23F89115D3457", true, "F004C958B6D8EF20AC9CC02DAEF86E11C6652D865F7163FE10BFC002E479193A"),
        ["legacy-falda-piano-campagna"] = (0, "A2BCBCEB4793A597044047E383AB7CE3C3C02BE64CBE8A5BFCDFF130067BAEF2", true, "9574F1036DD08837B5E458D4B2D8D09856BED1CF16805E99AF25CEF6CB9CE86B"),
        ["legacy-soil-profile"] = (0, "62E23996DAE108A99D5A829C756BA03392CE7A84E826DEEF1665A1FC60BF9EC1", true, "AB60986EA1B260C525094721DE5991EC6BEBFD0030C09726B33ACD951C4D6898"),
        ["meyerhof-eccentrica-input-raccolti"] = (3, "38BC9F3D1180D0CBC5CFEC7A3828EE73C174EC9F34255974CAFBB4BE1668AAD8", true, "13360422C6F96B3FAFDD3711CA31892124839AD1E89D78EEE8D565F651AF8763"),
        ["semispazio-non-assunto"] = (0, "E154759AD4D69C4F2BC63633DFCC13D89AD5AED4EB0211D1988F72E9FE463989", false, "-"),
        ["strato-incompleto"] = (0, "3234B79F26829E03852A71BFA0CE8F9EFD8DB0D24C780362D6F4FD0CF93B0E76", false, "-"),
        ["terzaghi-nastriforme-falda"] = (0, "E7F8A1E09705A6AE7E2F469DAAEFEED28484092D523B0CE72668014BCBBC2E46", true, "AA4BE2A94AB38BACE2F8D7D47E57EC2A1F1E36C140250421542188F15C70338D"),
        ["vesic-circolare-due-sondaggi"] = (0, "682B98CBC8C7B1700FD855F5629E6CDCE2D721A0AEAB721E91DA53D68A0000E1", true, "B32BBD773F0395C60398FAD7CD8213D8DA2DDB2D32C4FB2E6140DBFCBCA970EE"),
    };

    /// <summary>
    /// The technical drawings of the historic view, in the order of its tabs, after «Calcola»: for each fixture, index, width of the drawing (height
    /// 420 or 600), type, primitives and SHA-256 of the display list (<see cref="DisplayList"/>, lines joined by «\n»).
    /// </summary>
    private static readonly Dictionary<string, (int Index, int Width, string Type, int Count, string Hash)[]> HistoricDrawings = new(StringComparer.Ordinal)
    {
        ["confirmed-actions"] = [(0, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (1, 640, "FoundationInputPlan", 30, "9EED8014C56E98DEF90D28C2CEE7631297D453B9E571884E8F652A208357A2A9"), (2, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (3, 640, "FoundationPlanView", 28, "12EAC849C57F7A8885C5854F5E6F4AEE9E313CBCD269CA9F70712CB350E71FF4"), (4, 640, "FoundationPlanView", 1226, "5885087BD950D07959B3547A0946BFACD3C01A78D1FE3C2D4AFD4B98DAD69B3B"), (5, 640, "FoundationPressureSection", 108, "31929574233C3C33162B6F9BC06C989CD56EEF3CA3BF8730156EA2AAB43A2368"), (6, 640, "FoundationPressureSection", 108, "796B999B88C45C96CEB15C85D24A4D350B561ABA9DBAC225CB9116B29ACAE648"), (7, 640, "FoundationMechanismView", 132, "D60EE4E20F92766E389BE7F25DFFF1163C3C3CAE57626B46620FF0BC0D20839B"), (0, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (1, 900, "FoundationInputPlan", 30, "D94FFCDB25C398E52209FE8459D5A598D443C9A280153BE498CDE4E3234480F5"), (2, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (3, 900, "FoundationPlanView", 28, "0EB94B3F84378D3C5E9AEACF98B48D7CEEF309032CDA01A995DE0875BA05EEA6"), (4, 900, "FoundationPlanView", 1226, "3A4EAFA19CF38C1074CDD98F87AA5E3E6A438FB52E65242DBF9B14196906976C"), (5, 900, "FoundationPressureSection", 108, "BCF6128B70E6B624BA7920BE234031241E210EED289289446090B73BACEE9E93"), (6, 900, "FoundationPressureSection", 108, "129565B19E359369563AC6A00D94A4AA59336B779B94301D978522B8A329F527"), (7, 900, "FoundationMechanismView", 132, "EBE8638A393A3D45007FBA2382EA136135810919719D432CD122390418A8D1DF")],
        ["default"] = [(0, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (1, 640, "FoundationInputPlan", 30, "9EED8014C56E98DEF90D28C2CEE7631297D453B9E571884E8F652A208357A2A9"), (2, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (0, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (1, 900, "FoundationInputPlan", 30, "D94FFCDB25C398E52209FE8459D5A598D443C9A280153BE498CDE4E3234480F5"), (2, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84")],
        ["ec7-fuori-dominio"] = [(0, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (1, 640, "FoundationInputPlan", 30, "168653501D40DC5DD89AF2D7B7E54FEB523F18DAB92EF4DC94C05D5421DBF4DF"), (2, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (0, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (1, 900, "FoundationInputPlan", 30, "507F8EF6222A93A7BFD7AA1E1D52F626C16107B129690A87723C1377381146E1"), (2, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84")],
        ["hansen-non-drenato-caratteristico"] = [(0, 640, "FoundationSoilView", 59, "3692437E81FD0DAB0B57B25BE43127C06B169AB3E33399D5B1C4B164CA3174CB"), (1, 640, "FoundationInputPlan", 30, "279DDB6FD5350C657EA4207AC9C3677B474BDAEC2B2147673BEA01EC5C2712BB"), (2, 640, "FoundationSoilView", 59, "3692437E81FD0DAB0B57B25BE43127C06B169AB3E33399D5B1C4B164CA3174CB"), (3, 640, "FoundationPlanView", 28, "DFAFFBD95E72EEE1557B4F85FA71EE9015DFF032D0DBDB8413AE5E9266752D03"), (4, 640, "FoundationPlanView", 1226, "4503E245A7A136ED74ADF5A3964EDCC97B8D2240A732F9A0D43E6E2600E91F6E"), (5, 640, "FoundationPressureSection", 108, "32C8BB214D3FD78156A4E23E7EC9570F7F849336C8EDD6730915ACFA225FA3B3"), (6, 640, "FoundationPressureSection", 108, "7BDC723FA88332C342D3C14C72589FB052016F0394F6ED8FE65B49AAA40EA1A3"), (7, 640, "FoundationMechanismView", 132, "D2C91E8C976F4F2135ECBDBAC0A0362779B31CD812D21802046331A63B2DE388"), (0, 900, "FoundationSoilView", 59, "C360C91748F80279FB15DD95A73EF4AC874A69E59CEBE87F169274E3A039FB0C"), (1, 900, "FoundationInputPlan", 30, "A0F10F776C807F0AD82C11A6D59E6FECB4B4049F7EC5E2559B06455ED255A542"), (2, 900, "FoundationSoilView", 59, "C360C91748F80279FB15DD95A73EF4AC874A69E59CEBE87F169274E3A039FB0C"), (3, 900, "FoundationPlanView", 28, "00323A88FC34C9537293AAC1B37A4B19214F976B5261A2CE479BB546CE7D5593"), (4, 900, "FoundationPlanView", 1226, "D1BD7762A8C4E96EBFF1880F92313931E07E8901891047D1DD9012B5F11DE7BE"), (5, 900, "FoundationPressureSection", 108, "F893287A5679E54F8ECA403D8857312B3ED013BA986175CB1CB7EA37A2BFB6D5"), (6, 900, "FoundationPressureSection", 108, "E8356AEDDDEC38419BDA555678623B6F7954612467C586773DD0D990D6E526B8"), (7, 900, "FoundationMechanismView", 132, "2C74FDD14AA255EDB5E672A9F57961246CC061786CC66341276EA06A62424DAE")],
        ["legacy-falda-piano-campagna"] = [(0, 640, "FoundationSoilView", 50, "6EFB6BADB77BB2230B36340FF440DCE14190251F6F0EE81CCC1DD395EA861374"), (1, 640, "FoundationInputPlan", 30, "9EED8014C56E98DEF90D28C2CEE7631297D453B9E571884E8F652A208357A2A9"), (2, 640, "FoundationSoilView", 50, "6EFB6BADB77BB2230B36340FF440DCE14190251F6F0EE81CCC1DD395EA861374"), (3, 640, "FoundationPlanView", 28, "12EAC849C57F7A8885C5854F5E6F4AEE9E313CBCD269CA9F70712CB350E71FF4"), (4, 640, "FoundationPlanView", 1226, "5885087BD950D07959B3547A0946BFACD3C01A78D1FE3C2D4AFD4B98DAD69B3B"), (5, 640, "FoundationPressureSection", 108, "31929574233C3C33162B6F9BC06C989CD56EEF3CA3BF8730156EA2AAB43A2368"), (6, 640, "FoundationPressureSection", 108, "796B999B88C45C96CEB15C85D24A4D350B561ABA9DBAC225CB9116B29ACAE648"), (7, 640, "FoundationMechanismView", 132, "B9073ACE9EFCF5C55D813E44BA6066EFDB5098E52DD4B690DAC73F3DA4F95968"), (0, 900, "FoundationSoilView", 50, "47F730C646E1AC8EA25F918128B215F5F2D404C12D00A66D4787A75DA2EAAB96"), (1, 900, "FoundationInputPlan", 30, "D94FFCDB25C398E52209FE8459D5A598D443C9A280153BE498CDE4E3234480F5"), (2, 900, "FoundationSoilView", 50, "47F730C646E1AC8EA25F918128B215F5F2D404C12D00A66D4787A75DA2EAAB96"), (3, 900, "FoundationPlanView", 28, "0EB94B3F84378D3C5E9AEACF98B48D7CEEF309032CDA01A995DE0875BA05EEA6"), (4, 900, "FoundationPlanView", 1226, "3A4EAFA19CF38C1074CDD98F87AA5E3E6A438FB52E65242DBF9B14196906976C"), (5, 900, "FoundationPressureSection", 108, "BCF6128B70E6B624BA7920BE234031241E210EED289289446090B73BACEE9E93"), (6, 900, "FoundationPressureSection", 108, "129565B19E359369563AC6A00D94A4AA59336B779B94301D978522B8A329F527"), (7, 900, "FoundationMechanismView", 132, "8541AFDB8F6786012BB2EA42AE2AC6FA50E3CFEEC1A6E251BEEFEE49868D47E5")],
        ["legacy-soil-profile"] = [(0, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (1, 640, "FoundationInputPlan", 30, "9EED8014C56E98DEF90D28C2CEE7631297D453B9E571884E8F652A208357A2A9"), (2, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (3, 640, "FoundationPlanView", 28, "12EAC849C57F7A8885C5854F5E6F4AEE9E313CBCD269CA9F70712CB350E71FF4"), (4, 640, "FoundationPlanView", 1226, "5885087BD950D07959B3547A0946BFACD3C01A78D1FE3C2D4AFD4B98DAD69B3B"), (5, 640, "FoundationPressureSection", 108, "31929574233C3C33162B6F9BC06C989CD56EEF3CA3BF8730156EA2AAB43A2368"), (6, 640, "FoundationPressureSection", 108, "796B999B88C45C96CEB15C85D24A4D350B561ABA9DBAC225CB9116B29ACAE648"), (7, 640, "FoundationMechanismView", 132, "D60EE4E20F92766E389BE7F25DFFF1163C3C3CAE57626B46620FF0BC0D20839B"), (0, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (1, 900, "FoundationInputPlan", 30, "D94FFCDB25C398E52209FE8459D5A598D443C9A280153BE498CDE4E3234480F5"), (2, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (3, 900, "FoundationPlanView", 28, "0EB94B3F84378D3C5E9AEACF98B48D7CEEF309032CDA01A995DE0875BA05EEA6"), (4, 900, "FoundationPlanView", 1226, "3A4EAFA19CF38C1074CDD98F87AA5E3E6A438FB52E65242DBF9B14196906976C"), (5, 900, "FoundationPressureSection", 108, "BCF6128B70E6B624BA7920BE234031241E210EED289289446090B73BACEE9E93"), (6, 900, "FoundationPressureSection", 108, "129565B19E359369563AC6A00D94A4AA59336B779B94301D978522B8A329F527"), (7, 900, "FoundationMechanismView", 132, "EBE8638A393A3D45007FBA2382EA136135810919719D432CD122390418A8D1DF")],
        ["meyerhof-eccentrica-input-raccolti"] = [(0, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (1, 640, "FoundationInputPlan", 30, "1C73E61510F5752DD423E5A059453BF2791B1057A4686A740453F822E78FD072"), (2, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (3, 640, "FoundationPlanView", 28, "F9E397C23B6177558AD2452FE2643889C2C4C8BFB5E0B882F803257395E31900"), (4, 640, "FoundationPlanView", 1226, "CAC3FB88CA4D3E8F899F8186E27E3BE34F7820521E97D540395114236BE473C0"), (5, 640, "FoundationPressureSection", 108, "BC9CA697A0C26AAAF3381ED47AC4179CEAF6BE1F8DAD62CCD6DBF88747839FA5"), (6, 640, "FoundationPressureSection", 108, "928B280EAEEEE378944E30D33CD1BEBCD0B4E8E82D349FF6F74E35989C4D09D4"), (7, 640, "FoundationMechanismView", 132, "F8E8B66790B4709A70A1E3A8D113C575B8A61CEBDEAAE74FAF7BC2BA170FD85B"), (0, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (1, 900, "FoundationInputPlan", 30, "126E87C0E46AAFF3A094133C1D610754232CCA65ABDDC862996843C67E3F4C76"), (2, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (3, 900, "FoundationPlanView", 28, "21EA164758C68D6195C42EA219FE93DCE5553CC418A2C0FF8E672DCC9370570B"), (4, 900, "FoundationPlanView", 1226, "EA77D1D53E549A6F98D91ACBA7EBD823B00893C6C4BE9B4A9273B6D430C1AA3E"), (5, 900, "FoundationPressureSection", 108, "D98453E752C37952C3316C185B975C2069BC4FD71023F30B0B5DC606967611A4"), (6, 900, "FoundationPressureSection", 108, "E8B2148C6111FE3FB5BF4345E7593E6B02784D93DF93D95D6929A093FDF146E6"), (7, 900, "FoundationMechanismView", 132, "56583E61F621E1752632C6C38DC65EC00B66F3A1C92195C5A4320C2AA465AC17")],
        ["semispazio-non-assunto"] = [(0, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (1, 640, "FoundationInputPlan", 30, "97E67BC31933947D5DDD060744D8F7D9933223F0C439B54C06F61AC969929C43"), (2, 640, "FoundationSoilView", 56, "8132D0C5103F765BD7CBEB6F3E7BAD5A17E58A6F556F58CD8779C3C73CF7E3F6"), (0, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84"), (1, 900, "FoundationInputPlan", 30, "928D19983BBB4C03A1F366D5BBCE21990BDBDCF30B3EDA9A439BC7C0FF5870CC"), (2, 900, "FoundationSoilView", 59, "997242221F6ADDF714D1BCDC53DB74CC3D31B8DA337FAC4D01246C10E1217D84")],
        ["strato-incompleto"] = [(0, 640, "FoundationSoilView", 9, "922E89FCC850B243129CC7B2A97462D6B58DC299E670ACF05575E40F93455F84"), (1, 640, "FoundationInputPlan", 30, "AC64B629DA9A6B1CF16B7448624C4E811FFF79E97D093DDFCAE768707FF21CB4"), (2, 640, "FoundationSoilView", 9, "922E89FCC850B243129CC7B2A97462D6B58DC299E670ACF05575E40F93455F84"), (0, 900, "FoundationSoilView", 9, "51D0DFE799952B52E0D6187198AE7C7A8BFEA092AFE1A632D79C495B135E2134"), (1, 900, "FoundationInputPlan", 30, "C9A7642F75AF997EBCE11B59236087FDCB95B34886061123BF42CFCDAF658935"), (2, 900, "FoundationSoilView", 9, "51D0DFE799952B52E0D6187198AE7C7A8BFEA092AFE1A632D79C495B135E2134")],
        ["terzaghi-nastriforme-falda"] = [(0, 640, "FoundationSoilView", 60, "74B80CD2F36AAB517C2A416C77AABA35C40264A5F3C189066F4ABC9A9B7A7271"), (1, 640, "FoundationInputPlan", 30, "48735D78E20EEE9D01978806168C53CC5E7EE255ADC33063573F0202FABF7146"), (2, 640, "FoundationSoilView", 60, "74B80CD2F36AAB517C2A416C77AABA35C40264A5F3C189066F4ABC9A9B7A7271"), (3, 640, "FoundationPlanView", 28, "38BD162E62BBBC31C24082BCBDA277D869006A8FFC0CFCDE84A7D724F3D637E5"), (4, 640, "FoundationPlanView", 1226, "067807047D03BFAE4C3CEE5889DA19D373F9412366A557A8F3F9DAF77ECCA3AE"), (5, 640, "FoundationPressureSection", 108, "22135C1F39B493B8C9A5AA4F6AEEF8129A36FDDBC4FF61E1DB7EAEECC3E4DEB0"), (6, 640, "FoundationPressureSection", 108, "84266999BFF9BBF5CDB3B14D54779411202B05D3D82E557712F6971D17BA9974"), (7, 640, "FoundationMechanismView", 132, "CD2A931D546CFC5E3CD4EB89620DDD182CA61BA5C0299066498764707AC3D758"), (0, 900, "FoundationSoilView", 60, "4391CED9FD0EE5021710033A208A92017F5A52495CB11351CF2EA1403514B686"), (1, 900, "FoundationInputPlan", 30, "E624642C14D2EF45CEBEA25A266B74697AC9C993CB23E40080B87A00CF695722"), (2, 900, "FoundationSoilView", 60, "4391CED9FD0EE5021710033A208A92017F5A52495CB11351CF2EA1403514B686"), (3, 900, "FoundationPlanView", 28, "3064623CD931DF47C41B1A8FDBC2CC021835E9DD6B9A9AF8502548A615AF92B6"), (4, 900, "FoundationPlanView", 1226, "E2AB1773B0D78912238A92100841D28ABD69546A771BEE2836EB48CA303FBD69"), (5, 900, "FoundationPressureSection", 108, "1496B66E23189EA65E4B1BB0112BCA1BAE7A31ACCEF61E4E8892845C11A2C5B6"), (6, 900, "FoundationPressureSection", 108, "5217635848E35E01713BF799939857D43D3A3A746A6A06E33DCE04268DEB8631"), (7, 900, "FoundationMechanismView", 132, "74DBCB13C8EE8156E111E7793622EE84818BD951BCEE6CD24AA0A33DB66B175E")],
        ["vesic-circolare-due-sondaggi"] = [(0, 640, "FoundationSoilView", 51, "7D37BC8246ABBFACF422330A079F952A270FC8ADF8508CADD12AC554FECBB92C"), (1, 640, "FoundationInputPlan", 30, "31DA568CD307F11CAC2734C68E00A1DD4BF7ED8681204F4A2F6CDC32DB611486"), (2, 640, "FoundationSoilView", 51, "7D37BC8246ABBFACF422330A079F952A270FC8ADF8508CADD12AC554FECBB92C"), (3, 640, "FoundationPlanView", 27, "45B2A7364A7B2857119C821BB9272A056890D7285650A0A5D93AF00F0E452E53"), (4, 640, "FoundationPlanView", 922, "D44AFA6F09545733651829B3F2CC2BE2342CB80C8C77D4DF6E64A1AAE91C1D93"), (5, 640, "FoundationPressureSection", 108, "46CB6788CE82F2DA2302D260C0BEB27B61A3BC232DC05490E551F4D2DF0C7F31"), (6, 640, "FoundationPressureSection", 108, "112454328B96A0BC08165A2A05AF8420E4C90A7167A3FB82266529CA17E108C6"), (7, 640, "FoundationMechanismView", 132, "5BB2FE2142FF9C8C1DC1190199F2DE5408BA95B238BB8F9CC907BDCF68B01559"), (0, 900, "FoundationSoilView", 57, "AC2384C7F8A4DACBEB049AB01633AF00FC50FF64A25EBA4A3F8167ED9EE09477"), (1, 900, "FoundationInputPlan", 30, "681C8945E8679C4429BB8457FC30AF37AF6634CDF14C93D2526985E1AA0D2D7E"), (2, 900, "FoundationSoilView", 57, "AC2384C7F8A4DACBEB049AB01633AF00FC50FF64A25EBA4A3F8167ED9EE09477"), (3, 900, "FoundationPlanView", 27, "395EE93549D0779E95A79CF3BE94F9140693FCB560E6A289D51A33607325E28E"), (4, 900, "FoundationPlanView", 922, "4B5C0EBD70A0727F0340EF5A33379A0630FB6A55B822125163341FD0D579E80F"), (5, 900, "FoundationPressureSection", 108, "C35AA2FB907B39CB02A26B3AC0D477BDEA74BE76C92B78564052FD06333D5856"), (6, 900, "FoundationPressureSection", 108, "7427F95656D2EC2F4A9BA6E43E1EADDE8081EA9E600BF1B9A0340C59BA5332DC"), (7, 900, "FoundationMechanismView", 132, "F3E5F718693C2D6E4C60A19DF486B125B128C05FFCC3D97FC6D576B06937A07E")],
    };

    /// <summary>The AutomationProperties.Name of the visible elements of the historic view (calculated, groups opened), by fixture.</summary>
    private static readonly Dictionary<string, string[]> HistoricAutomationNames = new(StringComparer.Ordinal)
    {
        ["default"] = ["Calcestruzzo", "Cu [kPa]", "Diagramma", "Presenza falda", "Profondità falda zf", "Resistenza fck", "Spessore S [m]", "Tipologia", "c′ [kPa]", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]"],
        ["confirmed-actions"] = ["Calcestruzzo", "Cu [kPa]", "Diagramma", "Presenza falda", "Profondità falda zf", "Resistenza fck", "Spessore S [m]", "Tipologia", "c′ [kPa]", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]"],
        ["vesic-circolare-due-sondaggi"] = ["Calcestruzzo", "Cu [kPa]", "Diagramma", "Presenza falda", "Profondità falda zf", "Resistenza fck", "Spessore S [m]", "Tipologia", "c′ [kPa]", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]"],
        ["hansen-non-drenato-caratteristico"] = ["Calcestruzzo", "Cu [kPa]", "Diagramma", "Presenza falda", "Profondità falda zf", "Resistenza fck", "Spessore S [m]", "Tipologia", "c′ [kPa]", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]"],
    };

    /// <summary>The controls of the historic view (fixture vesic-circolare-due-sondaggi, tab «Rottura») that accept the user, before and after InspectRevision (<see cref="Controls"/>).</summary>
    private const string HistoricEditable = "pulsante +_Sondaggio attivo pulsante Aggiungi_strato attivo pulsante Calcola attivo pulsante Guida attivo pulsante Invia_/_carica_terreno… attivo pulsante Report_Word_e_PDF attivo pulsante [−] attivo pulsante [−] attivo pulsante raccogli attivo pulsante −_Sondaggio attivo scelta Compression libera scelta Condition libera scelta Method libera scelta Profile libera scelta Shape libera scelta classe_cls libera scelta cls_diagramma libera scelta meccanismo libera scelta sondaggio_fondazione libera scelta strato:tipologia libera scelta strato:tipologia libera spunta IncludeDepthFactors libera spunta azioni_confermate libera spunta presenza_falda libera spunta strato_portante_semiinfinito libera tabella coefficienti sola lettura tabella strati modificabile testo Depth libero testo HorizontalX libero testo Normal libero testo RelativeDensity libero testo Surcharge libero testo WaterUnitWeight libero testo Width libero testo fck_mpa libero testo nome_combinazione libero testo profondita_falda libero testo strato:angolo_attrito libero testo strato:angolo_attrito libero testo strato:coesione_efficace libero testo strato:coesione_efficace libero testo strato:coesione_non_drenata libero testo strato:coesione_non_drenata libero testo strato:peso_specifico libero testo strato:peso_specifico libero testo strato:peso_specifico_saturo libero testo strato:peso_specifico_saturo libero testo strato:spessore libero testo strato:spessore libero";

    private const string HistoricReadOnly = "pulsante +_Sondaggio spento pulsante Aggiungi_strato spento pulsante Calcola attivo pulsante Guida attivo pulsante Invia_/_carica_terreno… spento pulsante Report_Word_e_PDF attivo pulsante [−] spento pulsante [−] spento pulsante raccogli attivo pulsante −_Sondaggio spento scelta Compression bloccata scelta Condition bloccata scelta Method bloccata scelta Profile bloccata scelta Shape bloccata scelta classe_cls bloccata scelta cls_diagramma bloccata scelta meccanismo bloccata scelta sondaggio_fondazione bloccata scelta strato:tipologia bloccata scelta strato:tipologia bloccata spunta IncludeDepthFactors bloccata spunta azioni_confermate bloccata spunta presenza_falda bloccata spunta strato_portante_semiinfinito bloccata tabella coefficienti sola lettura tabella strati sola lettura testo Depth bloccato testo HorizontalX bloccato testo Normal bloccato testo RelativeDensity bloccato testo Surcharge bloccato testo WaterUnitWeight bloccato testo Width bloccato testo fck_mpa bloccato testo nome_combinazione bloccato testo profondita_falda bloccato testo strato:angolo_attrito bloccato testo strato:angolo_attrito bloccato testo strato:coesione_efficace bloccato testo strato:coesione_efficace bloccato testo strato:coesione_non_drenata bloccato testo strato:coesione_non_drenata bloccato testo strato:peso_specifico bloccato testo strato:peso_specifico bloccato testo strato:peso_specifico_saturo bloccato testo strato:peso_specifico_saturo bloccato testo strato:spessore bloccato testo strato:spessore bloccato";

    /// <summary>SHA-256 of a node as the checks compare the data (J.Options).</summary>
    private static string Hash(JsonNode node) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(node.ToJsonString(J.Options))));

    private const string Module = ShallowFoundationScenarios.Module;

    /// <summary>The Wiki of the services of the check: it records the sections opened, in order.</summary>
    private sealed class RecordingWiki : IWikiNavigator
    {
        internal List<string?> Shown { get; } = [];
        public void Show(string? uri = null) => Shown.Add(uri);
    }

    private static int checks;
    private static readonly List<string> Log = [];

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks++;
        Log.Add("OK " + message);
    }

    internal static async Task Run(string root, string output)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della prova deve essere vuota.");
        Directory.CreateDirectory(output);
        checks = 0; Log.Clear();
        // The culture of the captures of the historic view (it-IT), also in the operations that the dispatcher runs outside this flow.
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var wiki = new RecordingWiki();
        var removals = new List<string>();
        var editors = Editors(wiki, (title, question) => { removals.Add(title + ": " + question); return true; });
        try
        {
            await Traces(root, output, editors);
            Check(removals.Count > 0 && removals.All(q => q == "Stratigrafia: Eliminare il sondaggio selezionato e i suoi strati?"),
                "«− Sondaggio» chiede la conferma con il testo della vista storica (" + removals.Count + " domande, risposta sì come nelle tracce)");
            await FixturesAndExports(root, output, editors);
            await Screenshots(root, Path.Combine(output, "schermate"), editors);
            await Help(root, editors, wiki);
            await Identifiers(root, output, editors);
            await ReadOnly(root, editors);
            await SoilTransfer(root);
            InteractiveBench(root);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML delle fondazioni superficiali (MvvmSheetEditor, fuori schermo)", new UTF8Encoding(false));
    }

    /// <summary>The editors of the window with the MVVM foundations, a Wiki navigator that records what it opens and the answer to the removals.</summary>
    private static SheetEditorFactory Editors(IWikiNavigator wiki, Func<string, string, bool>? delete = null, ModuleRegistry? modules = null)
    {
        var bench = ModuleHostBench.Services();
        var services = new PresentationServices(bench.Messages, new ScriptedConfirmations { Delete = delete }, bench.Files, bench.Dialogs, bench.Clipboard, bench.Links, wiki);
        return new SheetEditorFactory(ModuleHostBench.Implementations([ModuleHostBench.Argument, Module + "=mvvm"]), modules ?? DesktopComposition.CreateModules(), services,
            new CalculationLimiter(CalculationLimiter.DefaultMaximum));
    }

    // ---------------------------------------------------------------- hosting

    /// <summary>A sheet in a window outside the screen, laid out at 1400×1050 like the sheet area of the main window.</summary>
    private sealed class Hosted : IDisposable
    {
        private readonly Window window;
        internal ISheetEditor Editor { get; }
        internal int Modified, Reports;

        internal Hosted(ISheetEditor editor, bool dark = false)
        {
            Editor = editor;
            editor.Modified += () => Modified++;
            editor.ReportRequested += () => Reports++;
            window = new Window
            {
                Content = editor.View, Width = 1400, Height = 1050, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Fondazioni " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
            Realize(Root);
        }

        internal FrameworkElement Root => Editor.View;

        internal ShallowFoundationSheetViewModel Sheet => (ShallowFoundationSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal ShallowFoundationSheetView View => Walk(Root).OfType<ShallowFoundationSheetView>().Single();

        internal void Layout()
        {
            window.UpdateLayout();
            Root.UpdateLayout();
        }

        internal async Task Settle()
        {
            Layout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Layout();
            Realize(Root);
        }

        /// <summary>Opens every expander, as the visit of the F3 capture (the groups write vista_fondazioni, as today).</summary>
        internal async Task ExpandAll()
        {
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (var expander in Walk(Root).OfType<Expander>().ToArray()) expander.IsExpanded = true;
                await Settle();
            }
        }

        internal byte[] Png()
        {
            Layout();
            var element = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
        }

        public void Dispose()
        {
            window.Content = null;
            window.Close();
            Editor.Dispose();
        }
    }

    /// <summary>The logical and the visual tree, as the visit of the F3 capture walks it (F3EquivalenceCapture.Walk); without
    /// <paramref name="popups"/> the content of the popups (lists of the combo boxes) is left out.</summary>
    internal static IEnumerable<DependencyObject> Walk(DependencyObject root, bool popups = true)
    {
        var pending = new Stack<DependencyObject>(); var seen = new HashSet<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop(); if (!seen.Add(current)) continue;
            yield return current;
            if (!popups && current is Popup) continue;
            var children = LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>().ToList();
            if (current is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) children.Add(VisualTreeHelper.GetChild(current, i));
            foreach (var child in children.AsEnumerable().Reverse()) pending.Push(child);
        }
    }

    /// <summary>
    /// Every control of the historic view exists, built in code, also where the user does not see it; the XAML view builds the content
    /// that WPF has not laid out yet (closed expanders, collapsed inputs, tabs not shown, rows hidden by the rules of the sheet) only when it
    /// is shown. Here that content is measured outside the visual tree and the templates of its controls are applied, so that the controls
    /// can be read and driven as in the historic trace (a control the user cannot use still does nothing). Nothing is written into the sheet.
    /// </summary>
    internal static void Realize(FrameworkElement root)
    {
        int known = -1;
        for (int pass = 0; pass < 20; pass++)
        {
            var elements = Walk(root, popups: false).OfType<FrameworkElement>().ToArray();
            foreach (var element in elements)
            {
                if (!ReferenceEquals(element, root) && element is not Window && VisualTreeHelper.GetParent(element) is null && !(element.IsMeasureValid && element.IsArrangeValid))
                {
                    element.Measure(new Size(760, 700));
                    element.Arrange(new Rect(0, 0, 760, 700));
                }
                element.ApplyTemplate();
            }
            if (elements.Length == known) return;
            known = elements.Length;
        }
    }

    private static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    /// <summary>A click as the user makes it (ButtonBase.OnClick: the Click event, then the command), only when the button is usable.</summary>
    private static void Click(ButtonBase button)
    {
        if (!Usable(button)) return;
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
    }

    private static string? BoundPath(DependencyObject element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    /// <summary>The data captured by a drawing of FoundationViews.cs (its JsonObject parameter), as the historic trace reads it.</summary>
    private static JsonObject? Captured(object? view) => view is null ? null : view.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(f => f.FieldType == typeof(JsonObject)).GetValue(view) as JsonObject;

    // ---------------------------------------------------------------- the view, read and driven through its controls

    /// <summary>The controls of the new view, found by their bindings and by the field of their FieldForm row.</summary>
    private sealed class ViewSide(Hosted host)
    {
        private IEnumerable<DependencyObject> All => Walk(host.Root);

        internal T Editor<T>(string key) where T : FrameworkElement
        {
            var found = All.OfType<T>().Where(e => e.DataContext is FieldViewModel f && f.Key == key && e.TemplatedParent is not Control).ToArray();
            return found.Length == 1 ? found[0] : throw new InvalidOperationException($"Controllo {typeof(T).Name} del campo {key}: {found.Length} trovati.");
        }

        internal bool Has<T>(string key) where T : FrameworkElement => All.OfType<T>().Any(e => e.DataContext is FieldViewModel f && f.Key == key && e.TemplatedParent is not Control);

        /// <summary>The row of a FieldForm field (Row of FieldForm.xaml): its visibility follows the rules of the sheet.</summary>
        internal Grid Row(string key) => All.OfType<Grid>().Single(g => g.Name == "Row" && g.DataContext is FieldViewModel f && f.Key == key);

        private T Named<T>(string name, string key) where T : FrameworkElement
        {
            var found = All.OfType<T>().Where(t => t.Name == name && t.DataContext is FieldViewModel f && f.Key == key).ToArray();
            return found.Length == 1 ? found[0] : throw new InvalidOperationException($"{name} del campo {key}: {found.Length} trovati.");
        }

        /// <summary>The combo box of an index choice (ChoiceRow) and its row.</summary>
        internal (ComboBox Choice, Grid Row)? IndexChoice(string key)
        {
            var choice = All.OfType<ComboBox>().SingleOrDefault(c => c.DataContext is ShallowFoundationChoiceViewModel v && v.Key == key);
            if (choice is null) return null;
            var row = All.OfType<Grid>().Single(g => g.Name == "ChoiceRow" && ReferenceEquals(g.DataContext, choice.DataContext));
            return (choice, row);
        }

        internal Expander Group(string key)
        {
            string path = "Groups[" + ShallowFoundationScenarios.GroupKeys.ToList().IndexOf(key).ToString(CultureInfo.InvariantCulture) + "].IsExpanded";
            return All.OfType<Expander>().Single(e => BoundPath(e, Expander.IsExpandedProperty) == path);
        }

        internal Button Command(string path) => All.OfType<Button>().Single(b => BoundPath(b, ButtonBase.CommandProperty) == path);

        internal Button Collect => All.OfType<Button>().Single(b => Equals(b.Tag, "raccogli-input"));

        /// <summary>The editor of a cell of the layers: the control of the row bound to the key.</summary>
        private FrameworkElement Cell(ShallowFoundationSheetView view, JsonRowViewModel row, string key) => Walk(view.Layers).OfType<FrameworkElement>()
            .Single(e => ReferenceEquals(e.DataContext, row) && (e is TextBox t && BoundPath(t, TextBox.TextProperty) == "[" + key + "]"
                || e is ComboBox c && BoundPath(c, Selector.SelectedItemProperty) == "[" + key + "]"));

        internal async Task Apply(ShallowFoundationStep step)
        {
            var view = host.View;
            switch (step.Kind)
            {
                case ShallowFoundationStep.Commit: host.Editor.Commit(); break;
                case ShallowFoundationStep.Calculate: Click(Command("CalculateCommand")); break;
                case ShallowFoundationStep.ReadOnly: host.Editor.InspectRevision(); break;
                case ShallowFoundationStep.Field:
                {
                    var box = Editor<TextBox>(step.Key!);
                    if (Usable(box) && Usable(Row(step.Key!)) && !box.IsReadOnly) box.Text = step.Text;
                    break;
                }
                case ShallowFoundationStep.Choice:
                    if (IndexChoice(step.Key!) is var (choice, choiceRow)) { if (Usable(choice) && Usable(choiceRow)) choice.SelectedItem = step.Text; }
                    else
                    {
                        var combo = Editor<ComboBox>(step.Key!);
                        if (Usable(combo) && Usable(Row(step.Key!))) combo.SelectedItem = step.Text;
                    }
                    break;
                case ShallowFoundationStep.Option:
                {
                    var box = Editor<CheckBox>(step.Key!);
                    if (Usable(box) && Usable(Row(step.Key!))) box.IsChecked = box.IsChecked != true;
                    break;
                }
                case ShallowFoundationStep.Group: { var group = Group(step.Key!); group.IsExpanded = !group.IsExpanded; break; }
                case ShallowFoundationStep.Collect: Click(Collect); break;
                case ShallowFoundationStep.Survey: if (Usable(view.Survey)) view.Survey.SelectedIndex = step.Index; break;
                case ShallowFoundationStep.AddSurvey: Click(Command("AddSurveyCommand")); break;
                case ShallowFoundationStep.RemoveSurvey: Click(Command("RemoveSurveyCommand")); break;
                case ShallowFoundationStep.Layer:
                {
                    var grid = view.Layers;
                    if (grid.IsReadOnly || !Usable(grid)) break;
                    // The cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged), as the historic table.
                    var cell = Cell(view, (JsonRowViewModel)grid.Items[step.Row], step.Key!);
                    if (cell is TextBox text) text.Text = step.Text;
                    else ((ComboBox)cell).SelectedItem = step.Text;
                    break;
                }
                case ShallowFoundationStep.AddLayer: Click(Command("AddLayerCommand")); break;
                case ShallowFoundationStep.RemoveLayer:
                {
                    var row = view.Layers.Items[step.Row];
                    if (Walk(view.Layers).OfType<Button>().SingleOrDefault(b => ReferenceEquals(b.DataContext, row) && Equals(b.Content, "[−]")) is { } remove) Click(remove);
                    break;
                }
                case ShallowFoundationStep.Mechanism:
                    // The section exists only with the graphics of a result, in the «Rottura» tab.
                    if (view.MechanismAxis.Parent is UIElement { Visibility: Visibility.Visible } && Usable(view.MechanismAxis)) view.MechanismAxis.SelectedIndex = step.Index;
                    break;
                case ShallowFoundationStep.Report: Click(Command("ReportCommand")); break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            await host.Settle();
        }

        internal ShallowFoundationTraceState State()
        {
            var view = host.View;
            var data = host.Editor.Data;
            var trace = new ShallowFoundationTraceState
            {
                Data = data, Modified = host.Modified, ReportRequests = host.Reports, Result = host.Editor.Result, State = view.Status.Text,
                Editable = !Editor<TextBox>("Width").IsReadOnly,
                InputsCollapsed = view.InputScroll.Visibility != Visibility.Visible, CollectText = (string)Collect.Content,
                Surveys = ((IEnumerable)view.Survey.ItemsSource).Cast<string>().ToArray(), SelectedSurvey = view.Survey.SelectedIndex,
                Layers = view.Layers.Items.Cast<JsonRowViewModel>().Select(r => ShallowFoundationTraceState.WithoutColour(r.Values)).ToList(),
                HalfSpace = Editor<CheckBox>("strato_portante_semiinfinito").IsChecked == true,
                Headline = view.Headline.Text, HasDetails = view.Details.Visibility == Visibility.Visible,
                Summary = view.Summary.Items.Cast<string>().ToList(),
                Coefficients = ((IEnumerable)view.Coefficients.ItemsSource).Cast<string[]>().ToList(),
                CoefficientColumns = view.Coefficients.Columns.Select(c => (string)c.Header).ToArray(),
                Notes = view.Notes.Items.Cast<string>().ToList(),
                InputDrawings = ReferenceEquals(view.InputSoil.Data, data) && ReferenceEquals(view.InputPlan.Data, data),
                SoilDrawing = view.SoilHost.Content is FoundationSoilView soil && ReferenceEquals(soil.Data, data),
            };
            if (view.Additive.Visibility == Visibility.Visible)
            {
                trace.AdditiveTitle = view.AdditiveTitle.Text;
                trace.Additive = ((IEnumerable)view.Additive.ItemsSource).Cast<string[]>().ToList();
                trace.AdditiveColumns = view.Additive.Columns.Select(c => (string)c.Header).ToArray();
            }
            foreach (string key in ShallowFoundationScenarios.FieldKeys) trace.Fields.Add(FieldState(key));
            foreach (string key in ShallowFoundationScenarios.GroupKeys) trace.Groups.Add((key, Group(key).IsExpanded));
            ReadGraphics(view, trace);
            return trace;
        }

        private ShallowFoundationFieldState FieldState(string key)
        {
            if (IndexChoice(key) is var (choice, choiceRow))
                return new(key, "scelta", choiceRow.Children.OfType<TextBlock>().Single(t => t.Name == "ChoiceLabel").Text, "", choice.SelectedItem as string ?? "",
                    ((IEnumerable)choice.ItemsSource).Cast<string>().ToArray(), choiceRow.Visibility == Visibility.Visible);
            bool visible = Row(key).Visibility == Visibility.Visible;
            string unit = Named<TextBlock>("UnitText", key).Text;
            if (Has<TextBox>(key)) return new(key, "campo", Named<TextBlock>("LabelText", key).Text, unit, Editor<TextBox>(key).Text, null, visible);
            if (Has<ComboBox>(key))
            {
                var combo = Editor<ComboBox>(key);
                return new(key, "scelta", Named<TextBlock>("LabelText", key).Text, unit, combo.SelectedItem as string ?? "", ((IEnumerable)combo.ItemsSource).Cast<string>().ToArray(), visible);
            }
            return new(key, "opzione", Named<TextBlock>("CheckLabel", key).Text, unit, PileGroupTrace.YesNo(Editor<CheckBox>(key).IsChecked == true), null, visible);
        }

        private void ReadGraphics(ShallowFoundationSheetView view, ShallowFoundationTraceState trace)
        {
            var messages = All.OfType<TextBlock>().Where(t => Equals(t.Tag, "graphics-message")).ToArray();
            if (messages.Length != 4) throw new InvalidOperationException("Messaggi delle schede dei risultati: " + messages.Length);
            if (messages.All(m => m.Visibility == Visibility.Visible))
            {
                var texts = messages.Select(m => m.Text).Distinct().ToArray();
                if (texts.Length != 1) throw new InvalidOperationException("Messaggi diversi nelle schede dei risultati.");
                trace.GraphicsMessage = texts[0];
                return;
            }
            if (messages.Any(m => m.Visibility == Visibility.Visible)) throw new InvalidOperationException("Schede dei risultati in parte senza grafici.");
            trace.Graphics = Captured(view.AreaHost.Content);
            if (!ReferenceEquals(Captured(view.PressureHost.Content), trace.Graphics)) throw new InvalidOperationException("Pressioni di un altro risultato.");
            trace.PressureText = view.PressureText.Text;
            trace.ContactText = view.ContactText.Text;
            trace.MechanismAxes = ((IEnumerable)view.MechanismAxis.ItemsSource).Cast<string>().ToArray();
            trace.MechanismAxis = view.MechanismAxis.SelectedIndex;
            trace.Mechanism = Captured(view.MechanismHost.Content);
            trace.MechanismText = view.MechanismText.Text;
        }
    }

    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in ShallowFoundationScenarios.All(root))
        {
            var trace = new List<string>();
            var host = new Hosted(editors.Open(Module, ShallowFoundationScenarios.FixtureData(root, scenario.Fixture)));
            try
            {
                await host.Settle();
                Check(host.Modified == 0, scenario.Name + ": apertura senza Modified");
                var side = new ViewSide(host);
                trace.AddRange(side.State().Lines("apertura", compareFrozenVersion: true));
                foreach (var step in scenario.Steps)
                {
                    if (step.Kind == ShallowFoundationStep.Reopen)
                    {
                        var data = host.Editor.Data;
                        host.Dispose();
                        host = new Hosted(editors.Open(Module, data));
                        await host.Settle();
                        side = new ViewSide(host);
                    }
                    else await side.Apply(step);
                    trace.AddRange(side.State().Lines(step.ToString(), compareFrozenVersion: true));
                }
            }
            finally { host.Dispose(); }
            PileGroupTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(ShallowFoundationScenarios.Reference(root), scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? PileGroupTrace.FirstDifference(PileGroupTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == 15, $"Tracce della vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe" + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    // ---------------------------------------------------------------- 2. visit, drawings and exports

    private static async Task FixturesAndExports(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        DrawingsFolder = Path.Combine(output, "disegni");
        Directory.CreateDirectory(DrawingsFolder);
        var normalizer = new TextNormalizer();
        var fixtures = ShallowFoundationScenarios.Fixtures(root);
        Check(fixtures.Count == 11, "Fixture F3 delle fondazioni: " + fixtures.Count);
        int drawings = 0, exported = 0;
        foreach (string id in fixtures)
        {
            var data = ShallowFoundationScenarios.FixtureData(root, id);
            using var mvvm = new Hosted(editors.Open(Module, data));
            Check(mvvm.Editor is MvvmSheetEditor, id + ": editor MVVM");
            string opened = mvvm.Editor.Data.ToJsonString(J.Options);
            var trace = PileGroupTrace.Read(Path.Combine(ShallowFoundationScenarios.Reference(root), "fixture-" + id + ".txt"));
            Check(trace[0] == "== apertura" && "dati " + PileGroupTrace.Compact(mvvm.Editor.Data) == trace.First(l => l.StartsWith("dati ", StringComparison.Ordinal)),
                id + ": dati all'apertura uguali a quelli della vista storica (traccia congelata), nell'ordine in memoria");
            // The visit of the user: every tab, the expanders without saved state, the three appearances.
            var view = mvvm.View;
            for (int i = 0; i < view.GraphicsTabs.Items.Count; i++) { view.GraphicsTabs.SelectedIndex = i; await mvvm.Settle(); }
            view.GraphicsTabs.SelectedIndex = 0;
            foreach (var expander in Walk(mvvm.Root).OfType<Expander>().Where(e => BindingOperations.GetBinding(e, Expander.IsExpandedProperty) is null).ToArray()) expander.IsExpanded = true;
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == opened && mvvm.Modified == 0,
                id + ": la visita (schede, espansori senza stato, tre aspetti) non scrive e non solleva Modified");
            // The visit of the F3 capture opens the groups too: the same writes of vista_fondazioni as the historic view.
            var historic = HistoricFixtures[id];
            await mvvm.ExpandAll();
            Check(Hash(mvvm.Editor.Data) == historic.Data && mvvm.Modified == historic.Modified,
                $"{id}: gruppi aperti come nella visita F3, stesse scritture di vista_fondazioni ({mvvm.Modified} Modified) della vista storica");
            Click(new ViewSide(mvvm).Command("CalculateCommand")); await mvvm.Settle();
            mvvm.Editor.Commit();
            var result = mvvm.Editor.Result;
            Check(Hash(mvvm.Editor.Data) == historic.Data && mvvm.Editor.HasResults == historic.HasResults && (result is null ? "-" : Hash(result)) == historic.Result,
                id + ": dati, stato e risultato dopo «Calcola» uguali a quelli della vista storica");
            drawings += SameDrawings(id, mvvm);
            if (!mvvm.Editor.HasResults)
            {
                Check(Fails(() => mvvm.Editor.ExportResult(Path.Combine(folder, id + "-mvvm.json")), out string a) && a == HistoricNoResult,
                    id + ": senza risultato l'esportazione dei risultati è rifiutata con il messaggio della vista storica («" + a + "»)");
                Check(Fails(() => mvvm.Editor.ExportReport(Path.Combine(folder, id + "-mvvm.docx"), "x", []), out string c) && c == HistoricNoReport,
                    id + ": senza risultato il report è rifiutato con il messaggio della vista storica («" + c + "»)");
                continue;
            }
            string title = "Fixture F3 · " + Module + " · " + id;
            mvvm.Editor.ExportResult(Path.Combine(folder, id + "-mvvm.json"));
            mvvm.Editor.ExportReport(Path.Combine(folder, id + "-mvvm.docx"), title, []);
            // The exports of the historic view: SheetEditor.ExportResult wrote the result with J.Options, SheetEditor.ExportReport the Word of
            // FoundationReport.Word and the PDF of FoundationReport.Pdf (MigraDoc) beside it, on the result.
            File.WriteAllBytes(Path.Combine(folder, id + "-attesa.json"), Encoding.UTF8.GetBytes(result!.ToJsonString(J.Options)));
            File.WriteAllBytes(Path.Combine(folder, id + "-attesa.docx"), FoundationReport.Word(title, result));
            File.WriteAllBytes(Path.Combine(folder, id + "-attesa.pdf"), FoundationReport.Pdf(title, result));
            Check(File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, id + "-attesa.json"))),
                id + ": JSON del risultato uguale byte per byte a quello della vista storica");
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, id + "-attesa.docx")), normalizer);
            Check(part is null, id + ": report Word uguale in ogni parte a quello della vista storica (date del documento normalizzate)" + (part is null ? "" : ", diversa " + part));
            byte[] pdf = File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.pdf"));
            Check(SamePdf(pdf, File.ReadAllBytes(Path.Combine(folder, id + "-attesa.pdf"))), id + ": PDF affiancato uguale a quello della vista storica, scritto da FoundationReport.Pdf (MigraDoc), non ricavato dal DOCX (T8; date e identificativi normalizzati)");
            exported++;
        }
        Log.Add($"   disegni confrontati: {drawings}; fixture esportate: {exported}");
    }

    /// <summary>Where the display lists of two different drawings are written, to read the difference.</summary>
    private static string? DrawingsFolder;

    private static bool Fails(Action action, out string message)
    {
        try { action(); message = ""; return false; }
        catch (InvalidOperationException error) { message = error.Message; return true; }
    }

    /// <summary>
    /// The technical drawings of the view (FoundationViews.cs, in the order of their tabs) drawn at the two sizes: the same types and the same
    /// display lists, primitive by primitive, as the historic view (<see cref="HistoricDrawings"/>). The view hosts the drawings unchanged, so
    /// equal lists mean equal data.
    /// </summary>
    private static int SameDrawings(string id, Hosted mvvm)
    {
        var a = Walk(mvvm.Root).OfType<FoundationPlot>().ToArray();
        var expected = HistoricDrawings[id];
        string[] types = expected.Where(d => d.Width == 640).OrderBy(d => d.Index).Select(d => d.Type).ToArray();
        Check(a.Select(d => d.GetType().Name).SequenceEqual(types),
            $"{id}: disegni tecnici della vista uguali per tipo e ordine a quelli della storica ({string.Join(", ", a.Select(d => d.GetType().Name))})");
        int compared = 0;
        foreach (var (width, height) in new[] { (640, 420), (900, 600) })
            for (int i = 0; i < a.Length; i++)
            {
                var x = DisplayList(Copy(a[i]), width, height);
                var y = expected.Single(d => d.Index == i && d.Width == width);
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", x))));
                if (hash != y.Hash && DrawingsFolder is { } folder) File.WriteAllLines(Path.Combine(folder, $"{id}-{i}-{width}-mvvm.txt"), x, new UTF8Encoding(false));
                Check(x.Count == y.Count && hash == y.Hash, $"{id}: {a[i].GetType().Name} [{i}] {width}×{height}: stesso elenco di disegno della vista storica ({x.Count} primitivi)");
                compared++;
            }
        return compared;
    }

    /// <summary>
    /// A new drawing of the same type with the data the hosted one received (the parameters of its constructor, or Data), outside every
    /// tree: drawn at the same sizes, the two views give their drawings the same data exactly when the display lists are equal.
    /// </summary>
    private static FoundationPlot Copy(FoundationPlot hosted)
    {
        var type = hosted.GetType();
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var constructor = type.GetConstructors(Members).Single();
        var fields = type.GetFields(Members);
        var copy = (FoundationPlot)constructor.Invoke(constructor.GetParameters().Select(p => fields.Single(f => f.Name == "<" + p.Name + ">P").GetValue(hosted)).ToArray());
        if (type.GetProperty("Data", Members) is { } data) data.SetValue(copy, data.GetValue(hosted));
        return copy;
    }

    /// <summary>The display list of a drawing laid out at the given size: groups, shapes (geometry, fill, pen) and texts with their position.</summary>
    private static List<string> DisplayList(FrameworkElement drawing, double width, double height)
    {
        drawing.Width = width; drawing.Height = height;
        drawing.Measure(new Size(width, height)); drawing.Arrange(new Rect(0, 0, width, height));
        var lines = new List<string>();
        if (VisualTreeHelper.GetDrawing(drawing) is { } content) Write(content, lines);
        return lines;

        static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string P(Point p) => N(p.X) + "," + N(p.Y);
        static string B(Brush? brush) => brush switch { null => "-", SolidColorBrush s => s.Color.ToString(CultureInfo.InvariantCulture), _ => brush.GetType().Name };
        static string Pen(Pen? pen) => pen is null ? "-" : B(pen.Brush) + " " + N(pen.Thickness) + (pen.DashStyle is { Dashes.Count: > 0 } dash ? " tratto " + string.Join(";", dash.Dashes.Select(N)) : "");
        static string G(Geometry? geometry) => geometry is null ? "-" : PathGeometry.CreateFromGeometry(geometry).ToString(CultureInfo.InvariantCulture);
        static void Write(Drawing node, List<string> lines)
        {
            switch (node)
            {
                case DrawingGroup group:
                    lines.Add("gruppo " + N(group.Opacity) + " " + G(group.ClipGeometry));
                    foreach (var child in group.Children) Write(child, lines);
                    lines.Add("fine");
                    break;
                case GeometryDrawing shape: lines.Add("forma " + G(shape.Geometry) + " " + B(shape.Brush) + " " + Pen(shape.Pen)); break;
                case GlyphRunDrawing text:
                    lines.Add("testo " + new string(text.GlyphRun.Characters.ToArray()) + " " + P(text.GlyphRun.BaselineOrigin) + " " + N(text.GlyphRun.FontRenderingEmSize) + " " + B(text.ForegroundBrush));
                    break;
                default: lines.Add(node.GetType().Name); break;
            }
        }
    }

    /// <summary>First part of two Word documents that differs, with the dates of core.xml and of the text normalized as in F3; null when equal.</summary>
    private static string? DifferentPart(byte[] a, byte[] b, TextNormalizer normalizer)
    {
        using var first = new ZipArchive(new MemoryStream(a)); using var second = new ZipArchive(new MemoryStream(b));
        var names = first.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(second.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal))) return "elenco delle parti";
        foreach (string name in names)
        {
            byte[] x = Read(first.GetEntry(name)!), y = Read(second.GetEntry(name)!);
            bool equal = name is "docProps/core.xml" or "word/document.xml"
                ? Normalize(name, x, normalizer) == Normalize(name, y, normalizer) : x.SequenceEqual(y);
            if (!equal) return name;
        }
        return null;
        static byte[] Read(ZipArchiveEntry entry) { using var stream = entry.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray(); }
        static string Normalize(string name, byte[] bytes, TextNormalizer normalizer) => name == "docProps/core.xml"
            ? normalizer.Normalize(Encoding.UTF8.GetString(bytes)) : normalizer.NormalizeReportTimestamps(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>Two PDF files equal but for what changes at every rendering: dates of creation and modification (also in the XMP
    /// metadata), identifiers of the file and of the XMP document, random tags of the embedded font subsets (six capital letters before «+»).</summary>
    private static bool SamePdf(byte[] a, byte[] b)
    {
        static string Text(byte[] bytes)
        {
            string text = Encoding.Latin1.GetString(bytes);
            text = Regex.Replace(text, @"\(D:[0-9+\-Z']+\)", "(D:)");
            text = Regex.Replace(text, @"/ID\s*\[[^\]]*\]", "/ID[]");
            text = Regex.Replace(text, @"(?<=/(?:FontName|BaseFont)/)[A-Z]{6}\+", "SUBSET+");
            text = Regex.Replace(text, @"(<xmp:\w*Date>)[^<]*(<)", "$1$2");
            return Regex.Replace(text, @"uuid:[0-9a-fA-F-]+", "uuid:");
        }
        return a.SequenceEqual(b) || Text(a) == Text(b);
    }

    // ---------------------------------------------------------------- 3. screenshots

    private static async Task Screenshots(string root, string folder, SheetEditorFactory editors)
    {
        Directory.CreateDirectory(folder);
        var cases = new[] { "confirmed-actions", "meyerhof-eccentrica-input-raccolti", "vesic-circolare-due-sondaggi" };
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            foreach (string id in cases)
            {
                using var host = new Hosted(editors.Open(Module, ShallowFoundationScenarios.FixtureData(root, id)), mode != AppAppearance.Light);
                await host.Editor.CalculateAsync();
                // The first fixture with every expander open; the others as saved (inputs collapsed, groups closed).
                if (id == "confirmed-actions") await host.ExpandAll();
                var tabs = Walk(host.Root).OfType<TabControl>().First();
                tabs.SelectedIndex = id == "confirmed-actions" ? 0 : id == "vesic-circolare-due-sondaggi" ? 3 : 2;
                await host.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"vista-{id}-{suffix}.png"), host.Png());
                written++;
            }
        }
        Appearance.Set(AppAppearance.Light, false);
        Check(written == 9, "Schermate della vista nei tre aspetti: " + written + " PNG in " + folder);
    }

    // ---------------------------------------------------------------- 4. help

    private static async Task Help(string root, SheetEditorFactory editors, RecordingWiki wiki)
    {
        var data = ShallowFoundationScenarios.FixtureData(root, "vesic-circolare-due-sondaggi");
        string guide = Anthea.Wiki.WikiCatalog.ForModule(Module)?.Id ?? throw new InvalidOperationException("Guida delle fondazioni assente nella Wiki");
        using var host = new Hosted(editors.Open(Module, data));
        await host.ExpandAll();
        static string[] Labels(DependencyObject root) => Walk(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
            .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
        string[] after = Labels(host.Root);
        Check(after.Length == 0, "Etichette «?» visibili uguali alla vista storica (FN9): nessuna, la Wiki non ha argomenti per i campi delle fondazioni");
        var side = new ViewSide(host);
        var view = host.View;
        var cell = Walk(view.Layers).OfType<TextBox>().First(t => t.DataContext is JsonRowViewModel);
        wiki.Shown.Clear();
        F1(side.Editor<TextBox>("Width"));
        F1(side.Editor<TextBox>("profondita_falda"));
        F1(side.Editor<TextBox>("fck_mpa"));
        F1(cell);
        F1(view.GraphicsTabs);
        Click(side.Command("GuideCommand"));
        Check(wiki.Shown.SequenceEqual([guide, guide, guide, guide, guide, guide]),
            "F1 su un numero, sulla falda, sul calcestruzzo, in una cella degli strati e fuori dai campi, e «Guida»: la guida del modulo dal navigatore della Wiki ("
            + string.Join(", ", wiki.Shown) + ")");
        host.Editor.InspectRevision();
        await host.Settle();
        wiki.Shown.Clear();
        F1(side.Editor<TextBox>("Width"));
        F1(side.Editor<TextBox>("profondita_falda"));
        Click(side.Command("GuideCommand"));
        Check(wiki.Shown.SequenceEqual([guide, guide, guide]), "F1 e «Guida» anche in sola lettura");
        // The historic view raised no Modified with the groups opened and the help (recorded).
        Check(host.Modified == 0, "L'aiuto non modifica il foglio");
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    // ---------------------------------------------------------------- 5. identifiers of FoundationWorkspace.UiChecks and automation names

    /// <summary>
    /// FoundationWorkspace.UiChecks (tests/FoundationWorkspace.Checks, method Ui) finds in the historic view the TextBox with Tag «Width», the
    /// button with Tag «raccogli-input», the first TabControl with six tabs and a DataGrid, and checks the layout; the same on the new view
    /// (the field Width is found by the key of its FieldForm row and keeps the automation name of the label). The suite itself is
    /// rewritten at the switch of the Release, after the test of the user.
    /// </summary>
    private static async Task Identifiers(string root, string output, SheetEditorFactory editors)
    {
        var data = ShallowFoundationScenarios.FixtureData(root, "confirmed-actions");
        using (var host = new Hosted(editors.Open(Module, data)))
        {
            await host.Settle();
            var side = new ViewSide(host);
            var width = side.Editor<TextBox>("Width");
            Check(AutomationProperties.GetName(width) == "Larghezza B [m]", "UiChecks: il campo Width esiste, con il nome di automazione della sua etichetta");
            Click(side.Command("CalculateCommand")); await host.Settle();
            var result = host.Editor.Result;
            Check(result is not null, "UiChecks: risultato presente con la libreria approvata");
            width.Text = "2,5"; await host.Settle();
            Check(host.Editor.Result is null, "UiChecks: una modifica invalida il risultato");
            width.Text = "2"; Click(side.Command("CalculateCommand")); await host.Settle();
            host.View.Details.IsExpanded = true; await host.Settle();
            Click(side.Command("CalculateCommand")); await host.Settle();
            Check(host.View.Details.Visibility == Visibility.Visible && !host.View.Details.IsExpanded && host.Modified == 2,
                "Dettagli della verifica chiusi a ogni calcolo, come l'espansore che la vista storica ricreava; stato della vista non salvato");
            var tabs = Visual(host.Root).OfType<TabControl>().First();
            Check(tabs.Items.Count == 6, "UiChecks: sei schede riservate ai grafici");
            for (int i = 0; i < tabs.Items.Count; i++)
            {
                tabs.SelectedIndex = i; await host.Settle();
                Check(Visual(host.Root).OfType<TextBox>().Any(t => ReferenceEquals(t, width) && t.IsVisible) && Visual(host.Root).OfType<DataGrid>().Any(g => g.IsVisible),
                    "UiChecks: dati e strati restano visibili con il grafico " + i);
            }
            tabs.SelectedIndex = 3; await host.Settle();
            var collect = Visual(host.Root).OfType<Button>().Single(b => Equals(b.Tag, "raccogli-input"));
            var previous = host.Editor.Result; double before = tabs.ActualWidth;
            Click(collect); await host.Settle();
            Check(tabs.ActualWidth > before + 200 && ReferenceEquals(previous, host.Editor.Result), $"UiChecks: «Raccogli input» amplia i grafici ({before:0} → {tabs.ActualWidth:0} px) e conserva il risultato");
            Check(host.Editor.Data["vista_fondazioni"]!["input_raccolti"]!.GetValue<bool>(), "UiChecks: stato dei pannelli salvato nel foglio");
            File.WriteAllBytes(Path.Combine(output, "input-raccolti.png"), host.Png());
            Click(collect); await host.Settle();
        }
        foreach (string id in new[] { "default", "confirmed-actions", "vesic-circolare-due-sondaggi", "hansen-non-drenato-caratteristico" })
        {
            var fixture = ShallowFoundationScenarios.FixtureData(root, id);
            using var host = new Hosted(editors.Open(Module, fixture));
            Click(new ViewSide(host).Command("CalculateCommand"));
            await host.ExpandAll();
            static HashSet<string> Names(DependencyObject root) => Walk(root).OfType<UIElement>().Where(e => e.IsVisible)
                .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
            var before = HistoricAutomationNames[id].ToHashSet(StringComparer.Ordinal); var after = Names(host.Root);
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Check(missing.Length == 0, $"{id}: nomi di automazione della vista storica conservati ({before.Count}: {string.Join(", ", before.Order(StringComparer.Ordinal))})"
                + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
        }
    }

    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--) pending.Push(VisualTreeHelper.GetChild(node, i));
        }
    }

    // ---------------------------------------------------------------- 6. read-only

    /// <summary>Field keys of the labels of the historic InputForms and of the columns of the layers, to name the controls of both views alike.</summary>
    private static readonly IReadOnlyDictionary<string, string> LabelKeys = ShallowFoundationFields.Water
        .Concat(ShallowFoundationFields.Concrete(new JsonObject { ["classe_cls"] = "Personalizzato" }, []))
        .Select(f => (f.Label, f.Key)).Concat(ShallowFoundationFields.LayerColumns.Select(f => (f.Label, "strato:" + f.Key)))
        .Concat(ShallowFoundationFields.MethodOptions.Select(f => (f.Label, f.Key)))
        .Append((Anthea.Application.Presentation.ShallowFoundationPresentation.HalfSpaceLabel, "strato_portante_semiinfinito"))
        .ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);

    /// <summary>Which visible controls accept the user, in the view (kind, key and state), in ordinal order, as recorded for the historic view.</summary>
    private static string Controls(DependencyObject root, Func<FrameworkElement, string?> key) => string.Join(" ", Walk(root).OfType<FrameworkElement>().Where(e => e.IsVisible)
        .Select(e => e switch
        {
            TextBox t when t.TemplatedParent is not Control => "testo " + key(t) + (t.IsReadOnly || !t.IsEnabled ? " bloccato" : " libero"),
            ComboBox c => "scelta " + key(c) + (c.IsEnabled ? " libera" : " bloccata"),
            CheckBox c => "spunta " + key(c) + (c.IsEnabled ? " libera" : " bloccata"),
            Button b when b.TemplatedParent is not Control => "pulsante " + key(b) + (b.IsEnabled ? " attivo" : " spento"),
            DataGrid g => "tabella " + key(g) + (g.IsReadOnly ? " sola lettura" : " modificabile"),
            _ => null
        }).Where(s => s is not null).Order(StringComparer.Ordinal));

    private static string? Common(FrameworkElement e) => e switch
    {
        Button b => b.Content as string is "Raccogli input" or "Mostra input" ? "raccogli" : (b.Content as string)?.Replace(" ", "_", StringComparison.Ordinal),
        ComboBox c when c.ItemsSource is IEnumerable items && items.Cast<object>().Contains("Sezione X · larghezza B") => "meccanismo",
        _ when AutomationProperties.GetName(e) is { Length: > 0 } name && LabelKeys.TryGetValue(name, out var mapped) && e is not TextBox { DataContext: FieldViewModel } => mapped,
        _ => null
    };

    private static string? NewKey(FrameworkElement e) => e switch
    {
        DataGrid g => g.Name switch { "Layers" => "strati", "Coefficients" => "coefficienti", _ => "additive" },
        _ when e.DataContext is FieldViewModel f => f.Key,
        _ => Common(e) ?? e.Tag as string
    };

    private static async Task ReadOnly(string root, SheetEditorFactory editors)
    {
        var data = ShallowFoundationScenarios.FixtureData(root, "vesic-circolare-due-sondaggi");
        using var host = new Hosted(editors.Open(Module, data));
        Click(new ViewSide(host).Command("CalculateCommand"));
        await host.ExpandAll();
        // The «Rottura» tab, where the section of the mechanism is shown.
        Walk(host.Root).OfType<TabControl>().First().SelectedIndex = 5; await host.Settle();
        string editableBefore = HistoricEditable, editableAfter = Controls(host.Root, NewKey);
        Log.Add("   modificabile, storica: " + editableBefore);
        Log.Add("   modificabile, nuova:   " + editableAfter);
        Check(editableBefore == editableAfter, "Foglio modificabile: stessi controlli liberi della vista storica");
        // The historic view raised no Modified in the same visit (recorded).
        int modified = 0;
        Check(modified == host.Modified, "Stesse scritture della visita prima della revisione");
        host.Editor.InspectRevision();
        await host.Settle();
        string before = HistoricReadOnly, after = Controls(host.Root, NewKey);
        Log.Add("   sola lettura, storica: " + before);
        Log.Add("   sola lettura, nuova:   " + after);
        Check(before == after, "Sola lettura: stessi controlli bloccati e liberi della vista storica");
        var side = new ViewSide(host);
        var view = host.View;
        string data0 = host.Editor.Data.ToJsonString(J.Options), shown = side.Editor<TextBox>("Width").Text;
        side.Editor<TextBox>("Width").Text = "9";
        side.Editor<CheckBox>("presenza_falda").IsChecked = !side.Editor<CheckBox>("presenza_falda").IsChecked;
        view.MechanismAxis.SelectedIndex = 1;
        await host.Settle();
        Check(host.Editor.Data.ToJsonString(J.Options) == data0 && host.Modified == modified && side.Editor<TextBox>("Width").Text == shown && view.MechanismAxis.SelectedIndex == 0,
            "Sola lettura: digitazione, spunte e sezione del meccanismo (FN4) rifiutate, la vista mostra di nuovo i dati");
        var group = side.Group("metodo");
        group.IsExpanded = false; Click(side.Collect);
        await host.Settle();
        Check(host.Modified == modified + 2 && host.Editor.HasResults && host.Editor.Data["vista_fondazioni"]!["metodo"]!.GetValue<bool>() == false
            && host.Editor.Data["vista_fondazioni"]!["input_raccolti"]!.GetValue<bool>(), "Sola lettura: gruppi e «Raccogli input» scrivono vista_fondazioni e conservano il risultato (FN3, T6)");
        Click(side.Collect); Click(side.Command("ReportCommand")); Click(side.Command("CalculateCommand"));
        await host.Settle();
        Check(host.Reports == 1 && host.Editor.HasResults, "Sola lettura: report e «Calcola» disponibili");
    }

    // ---------------------------------------------------------------- 7. soil transfer and interactive bench

    /// <summary>The historic dialog behind ISoilTransferDialog, scripted: what it receives and what it answers.</summary>
    private sealed class ScriptedTransfer : ISoilTransferDialog
    {
        internal readonly Queue<SoilTransferChoice?> Answers = new();
        internal readonly List<(string Module, string Data, int Sources)> Calls = [];
        public Task<SoilTransferChoice?> ShowAsync(string module, JsonObject data, IReadOnlyList<SoilSource> sources)
        {
            Calls.Add((module, data.ToJsonString(J.Options), sources.Count));
            return Task.FromResult(Answers.Dequeue());
        }
    }

    private static async Task SoilTransfer(string root)
    {
        var transfer = new ScriptedTransfer();
        // Replace only the module under test; keep every module that the Release opens in MVVM, including Spettri.
        var defaults = DesktopComposition.CreateModules();
        var editors = Editors(new RecordingWiki(), modules: new ModuleRegistry(defaults.SheetModuleIds.Select(id =>
            id == Module ? new ShallowFoundationSheetModule(transfer: transfer) : defaults.SheetModule(id))));
        var data = ShallowFoundationScenarios.FixtureData(root, "vesic-circolare-due-sondaggi");
        using var host = new Hosted(editors.Open(Module, data));
        await host.Settle();
        var copies = new List<(string Module, string Name)>(); var replacements = new List<JsonObject>();
        host.Editor.ModuleCopyRequested += (module, _, name) => copies.Add((module, name));
        host.Editor.SoilReplacementRequested += replacements.Add;
        var sourceData = new JsonObject();
        host.Editor.SoilSources = () => [("Stratigrafia di prova", "geo_stratigrafia", sourceData, "id-1")];
        var button = new ViewSide(host).Command("TransferSoilCommand");
        Check(Equals(button.Content, "Invia / carica terreno…") && button.IsEnabled, "«Invia / carica terreno…» disponibile nel foglio modificabile");
        var copy = new JsonObject { ["copia"] = true }; var replacement = new JsonObject { ["sostituito"] = true };
        transfer.Answers.Enqueue(new SoilTransferChoice(copy, "geo_palo_verticale", "Palo dal terreno"));
        transfer.Answers.Enqueue(new SoilTransferChoice(replacement));
        transfer.Answers.Enqueue(null);
        string before = host.Editor.Data.ToJsonString(J.Options);
        for (int i = 0; i < 3; i++) { Click(button); await host.Settle(); }
        Check(transfer.Calls.Count == 3 && transfer.Calls.All(c => c.Module == Module && c.Data == before && c.Sources == 1),
            "Il dialogo riceve modulo, dati confermati e fogli terreno del progetto, come SheetEditor.OpenSoilTransfer");
        Check(copies.SequenceEqual([("geo_palo_verticale", "Palo dal terreno")]) && replacements.Count == 1 && ReferenceEquals(replacements[0], replacement)
            && host.Modified == 0 && host.Editor.Data.ToJsonString(J.Options) == before,
            "Copia in un nuovo foglio e sostituzione del terreno inviate alla finestra; chiusura senza scelta senza effetti");
        host.Editor.InspectRevision(); await host.Settle();
        Check(!button.IsEnabled, "«Invia / carica terreno…» spento in sola lettura, come sotto RevisionInspection");
    }

    /// <summary>
    /// ANTHEA.exe --interactive (UiTests) and the Release: the factory of the composition root, with or without the bench choice, opens the MVVM
    /// foundations with the services of the product.
    /// </summary>
    private static void InteractiveBench(string root)
    {
        var composition = new DesktopComposition(() => null);
        var factory = ModuleHostBench.SheetEditors([ModuleHostBench.Interactive, ModuleHostBench.Argument, Module + "=mvvm"], composition);
        using (var foundation = factory.Open(Module, ShallowFoundationScenarios.FixtureData(root, "default")))
            Check(foundation is MvvmSheetEditor { Sheet: ShallowFoundationSheetViewModel sheet } && sheet.ConcreteForm.WikiNavigator is WpfWikiNavigator
                && sheet.TransferSoilCommand.CanExecute(null) && !sheet.HasResults, "Banco interattivo: fondazioni MVVM con i servizi del prodotto (Wiki, trasferimento del terreno), senza calcolo all'apertura");
        using (var other = factory.Open("geo_palo_verticale", Anthea.Application.Modules.ModuleDirectory.CreateData("geo_palo_verticale")))
            Check(other is MvvmSheetEditor, "Banco interattivo: gli altri moduli come nella Release (il palo verticale con la vista MVVM dal 23/10)");
        Check(composition.SheetEditors.Open(Module, ShallowFoundationScenarios.FixtureData(root, "default")) is MvvmSheetEditor { Sheet: ShallowFoundationSheetViewModel } release && Dispose(release),
            "Release: la radice di composizione apre le fondazioni con la vista MVVM");
    }

    private static bool Dispose(IDisposable value) { value.Dispose(); return true; }
}
