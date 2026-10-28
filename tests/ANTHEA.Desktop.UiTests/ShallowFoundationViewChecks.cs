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
    // 26/10/2026, set AH (AH-FON-01): di nuovo solo library_version, 0.19.0.1 -> 0.19.0.2; con 0.19.0.1 tornano le impronte di AG-FON-01.
    // 26/10/2026, F5.15 passo e (Checker 799a6355): di nuovo solo library_version, 0.19.0.2 -> 0.19.0.3; con 0.19.0.2 tornano quelle di AH-FON-01.
    // 27/10/2026, librerie della 1.14.2 (Checker 4a1b0090, AJ-FON-01): di nuovo solo library_version, 0.19.0.3 -> 0.24.0.0; con 0.19.0.3 tornano quelle del passo e.
    // 28/10/2026, librerie della 1.14.4 (AK-FON-01): di nuovo solo library_version, 0.24.0.0 -> 0.25.0.0; con 0.24.0.0 tornano quelle di AJ-FON-01.
    // 28/10/2026, set ufficiale AK: library_version 0.25.0.0 -> 0.25.0.2 (unione con il master di Checker, D4 b e D5 b); con 0.25.0.0 tornano le precedenti.
    // 28/10/2026, set candidato b della 1.15.0 (B-FON-01): library_version 0.25.0.2 -> 0.25.0.3 (Geotechnics ricompilata senza modifiche al sorgente, Checker ffc28d34); con 0.25.0.2 tornano quelle del set AK.
    // Disegni: testo attenuato della Chiara #64748B -> #5B697E (decisione del 27/10/2026, UI-CONTRASTO-01); con #64748B tornano gli elenchi storici.
    private static readonly Dictionary<string, (int Modified, string Data, bool HasResults, string Result)> HistoricFixtures = new(StringComparer.Ordinal)
    {
        ["confirmed-actions"] = (0, "E8C8939775578BBB33D5EAB88C1D346C006BD8F3BC820119D0028A7B611C6CBC", true, "D985F4BEEEB11C9AF8B61608FA33B5466C7DF96F0A3AB56212BEB70D9E8A24C8"),
        ["default"] = (0, "FD745ADB9F5D8CFD1DE449863BE9F9F0779F953BA53E95D6D711ED5CA6123B64", false, "-"),
        ["ec7-fuori-dominio"] = (0, "CEE1ADB72FF04EC044679866AC1A04BAD15C3750FF678400145816D4F81F74EA", true, "265C66F0F8ECBDA087A102FA105D7FEC1C1A78F354AA23330154AE88A38DF682"),
        ["hansen-non-drenato-caratteristico"] = (0, "58461F5ADC2616891086A78F53B71B7BFA5E6B91F56B1A9B69F23F89115D3457", true, "A7080775A7C1FA2AB0A38B8149FEA72433C55885D4C85118D986A1898DB482E3"),
        ["legacy-falda-piano-campagna"] = (0, "A2BCBCEB4793A597044047E383AB7CE3C3C02BE64CBE8A5BFCDFF130067BAEF2", true, "7F4F9AD3FBC5C03D1AA75D3B7AAC9A41EC3BF5EB4858C8BBDA34CF912B755639"),
        ["legacy-soil-profile"] = (0, "62E23996DAE108A99D5A829C756BA03392CE7A84E826DEEF1665A1FC60BF9EC1", true, "16E91B7E2A6ECF983C0FF8EC5078FA40EFB804FD9588B054E27CC5FA2B7CED9B"),
        ["meyerhof-eccentrica-input-raccolti"] = (3, "38BC9F3D1180D0CBC5CFEC7A3828EE73C174EC9F34255974CAFBB4BE1668AAD8", true, "5E31893005E68229FA9EF933249D679F3B089CCC7A47C59280504DCC110301F5"),
        ["semispazio-non-assunto"] = (0, "E154759AD4D69C4F2BC63633DFCC13D89AD5AED4EB0211D1988F72E9FE463989", false, "-"),
        ["strato-incompleto"] = (0, "3234B79F26829E03852A71BFA0CE8F9EFD8DB0D24C780362D6F4FD0CF93B0E76", false, "-"),
        ["terzaghi-nastriforme-falda"] = (0, "E7F8A1E09705A6AE7E2F469DAAEFEED28484092D523B0CE72668014BCBBC2E46", true, "8AF2A3877EC1AF08CED7C2BBF390DD98F34FF999E8366539AEFA221A0BC7626A"),
        ["vesic-circolare-due-sondaggi"] = (0, "682B98CBC8C7B1700FD855F5629E6CDCE2D721A0AEAB721E91DA53D68A0000E1", true, "487A11BDA0C7FE094193E36BECE46B5C893E3C63B095B8CF36A7822932C47B83"),
    };

    /// <summary>
    /// The technical drawings of the historic view, in the order of its tabs, after «Calcola»: for each fixture, index, width of the drawing (height
    /// 420 or 600), type, primitives and SHA-256 of the display list (<see cref="DisplayList"/>, lines joined by «\n»).
    /// </summary>
    private static readonly Dictionary<string, (int Index, int Width, string Type, int Count, string Hash)[]> HistoricDrawings = new(StringComparer.Ordinal)
    {
        ["confirmed-actions"] = [(0, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (1, 640, "FoundationInputPlan", 30, "D2FDCFD8407B1C283F8AE28193341119EA5E9D532053C5E190750BD6F35444C2"), (2, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (3, 640, "FoundationPlanView", 28, "19EB2FB5315463B4AF149EBE177DC788356FB3DE716D7FDF052B29AF0EF7CA74"), (4, 640, "FoundationPlanView", 1226, "636DF18EDEC43BC1D705AD724BF190D8F48C1049E915D63E1863C6B2056FD3B4"), (5, 640, "FoundationPressureSection", 108, "4778ECE8CF523D118DF0D0DFD6F94B4EA0AE7FD4E81015A9FF13B580EA0BC6E7"), (6, 640, "FoundationPressureSection", 108, "016605D4308B85DFBDD31E0295E668BAA53C56BBAAC0A0DC3062914E44B5E3FA"), (7, 640, "FoundationMechanismView", 132, "5D989404F510FA882642A3C6BCBE55E07777F82E9786A78EC2FCF9DD0008080F"), (0, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (1, 900, "FoundationInputPlan", 30, "B7E04C3DD4553369282030A6360E36B92617E10D757357B0C9AC312FE31F213E"), (2, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (3, 900, "FoundationPlanView", 28, "E3B525D03E27C513110EEA5700C7F6FF60F834611BEF4DB490B93D39B2772A27"), (4, 900, "FoundationPlanView", 1226, "20474E7261E28DB2EDBD6A64FAA00C02D0CD6210F10BBE359DE09A45D07890C6"), (5, 900, "FoundationPressureSection", 108, "51DD25096E4B1B21B6607D385C266A1763A5D3F0F74B3DD54CDC44FC013986FA"), (6, 900, "FoundationPressureSection", 108, "96D685DDB10B776D31E067ED4F0FDA383533E4A6FF9EF039C67ACBB651D817E7"), (7, 900, "FoundationMechanismView", 132, "057268D9F3E0D1B9012C125FE6FE48BC575394C0103F7195E8FDE14881C6A93F")],
        ["default"] = [(0, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (1, 640, "FoundationInputPlan", 30, "D2FDCFD8407B1C283F8AE28193341119EA5E9D532053C5E190750BD6F35444C2"), (2, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (0, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (1, 900, "FoundationInputPlan", 30, "B7E04C3DD4553369282030A6360E36B92617E10D757357B0C9AC312FE31F213E"), (2, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB")],
        ["ec7-fuori-dominio"] = [(0, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (1, 640, "FoundationInputPlan", 30, "A05891A4B81F1F01A25744ED400872F9A339163508E83F017B8BE2E111E7F138"), (2, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (0, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (1, 900, "FoundationInputPlan", 30, "A752CC488EC9E0EC5FCDC80AB8902E85E0B4D976DDC9B87F51DEBFD76A3D55C6"), (2, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB")],
        ["hansen-non-drenato-caratteristico"] = [(0, 640, "FoundationSoilView", 59, "E95EEC862E0E36F3278ED609A48FCA393ADD388C4903863E42D7378A1B6981EF"), (1, 640, "FoundationInputPlan", 30, "1145191A3BD6BABE82F633E0161376A57B18AEE320E93742E90CC5AEF5195695"), (2, 640, "FoundationSoilView", 59, "E95EEC862E0E36F3278ED609A48FCA393ADD388C4903863E42D7378A1B6981EF"), (3, 640, "FoundationPlanView", 28, "62CF3CBF86A4332C65F7152B39B58FE07393EC5E4B6CAD0B846EA1AAF9BB0381"), (4, 640, "FoundationPlanView", 1226, "0F4AD6D1B0A21F2DF8C2AA81B3291C2A40A56336E08E33FD5D8FCE78445598FC"), (5, 640, "FoundationPressureSection", 108, "7E329D83CBAAE361DAB18F339F606BE4F677453664369A25DD3FF08CC400559D"), (6, 640, "FoundationPressureSection", 108, "D88C6079CD8B2DB91A50CDF90B7096C7FB8B7D7B305D1EC5F0889D24BCF370FC"), (7, 640, "FoundationMechanismView", 132, "C90909D66A3421B83D3FB101571CC6FA6B31CF3603F89334655E5C576EBDB8DE"), (0, 900, "FoundationSoilView", 59, "F2AF79B6712B5FBB1DEDC083E68905B6A5947EEA1BC0C057FF5D71F59082B8A8"), (1, 900, "FoundationInputPlan", 30, "B3CCD08080E1EACB1709549461F6DF6A64759B479BC6CD49C17974DF89BAFB12"), (2, 900, "FoundationSoilView", 59, "F2AF79B6712B5FBB1DEDC083E68905B6A5947EEA1BC0C057FF5D71F59082B8A8"), (3, 900, "FoundationPlanView", 28, "E62C601FEF5309DC34419E1602F3373D091736E4C840C781157F535CE7183103"), (4, 900, "FoundationPlanView", 1226, "33075F655A88A1368769579E71723E4CB3354A077A52FF7A50E9D7196C5A9302"), (5, 900, "FoundationPressureSection", 108, "5B183C30BA9975B560DDF52FC85BF282B17F12024502BC64A4E7330C5837CC6D"), (6, 900, "FoundationPressureSection", 108, "AC01C5194F0FD8BD8AC3AC9E54E62C025BD19A70D5126BB9F4DC52904ED36BA3"), (7, 900, "FoundationMechanismView", 132, "EE5FE754ADCDDC3757AE60FF14BC5ECD54666117790CBF8EE05B850E05C44776")],
        ["legacy-falda-piano-campagna"] = [(0, 640, "FoundationSoilView", 50, "DC6E2EEE77694917EAC5F495A786A26768DE6A8E3F076BACED9B569759A64BC7"), (1, 640, "FoundationInputPlan", 30, "D2FDCFD8407B1C283F8AE28193341119EA5E9D532053C5E190750BD6F35444C2"), (2, 640, "FoundationSoilView", 50, "DC6E2EEE77694917EAC5F495A786A26768DE6A8E3F076BACED9B569759A64BC7"), (3, 640, "FoundationPlanView", 28, "19EB2FB5315463B4AF149EBE177DC788356FB3DE716D7FDF052B29AF0EF7CA74"), (4, 640, "FoundationPlanView", 1226, "636DF18EDEC43BC1D705AD724BF190D8F48C1049E915D63E1863C6B2056FD3B4"), (5, 640, "FoundationPressureSection", 108, "4778ECE8CF523D118DF0D0DFD6F94B4EA0AE7FD4E81015A9FF13B580EA0BC6E7"), (6, 640, "FoundationPressureSection", 108, "016605D4308B85DFBDD31E0295E668BAA53C56BBAAC0A0DC3062914E44B5E3FA"), (7, 640, "FoundationMechanismView", 132, "31AC6FB749DEF2E3D11AB41A138A60E98CEA9C5DBBF400BF39148A0D8FBC26F8"), (0, 900, "FoundationSoilView", 50, "662E0B411D1174343BBEE5D3DDBCEF18DF864854FFA6DEEB335EF9DAD853BFD6"), (1, 900, "FoundationInputPlan", 30, "B7E04C3DD4553369282030A6360E36B92617E10D757357B0C9AC312FE31F213E"), (2, 900, "FoundationSoilView", 50, "662E0B411D1174343BBEE5D3DDBCEF18DF864854FFA6DEEB335EF9DAD853BFD6"), (3, 900, "FoundationPlanView", 28, "E3B525D03E27C513110EEA5700C7F6FF60F834611BEF4DB490B93D39B2772A27"), (4, 900, "FoundationPlanView", 1226, "20474E7261E28DB2EDBD6A64FAA00C02D0CD6210F10BBE359DE09A45D07890C6"), (5, 900, "FoundationPressureSection", 108, "51DD25096E4B1B21B6607D385C266A1763A5D3F0F74B3DD54CDC44FC013986FA"), (6, 900, "FoundationPressureSection", 108, "96D685DDB10B776D31E067ED4F0FDA383533E4A6FF9EF039C67ACBB651D817E7"), (7, 900, "FoundationMechanismView", 132, "3B004776D27BC9F703527D5E87E20756EA682F97A9B10B854C6CD886F8CB4FD3")],
        ["legacy-soil-profile"] = [(0, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (1, 640, "FoundationInputPlan", 30, "D2FDCFD8407B1C283F8AE28193341119EA5E9D532053C5E190750BD6F35444C2"), (2, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (3, 640, "FoundationPlanView", 28, "19EB2FB5315463B4AF149EBE177DC788356FB3DE716D7FDF052B29AF0EF7CA74"), (4, 640, "FoundationPlanView", 1226, "636DF18EDEC43BC1D705AD724BF190D8F48C1049E915D63E1863C6B2056FD3B4"), (5, 640, "FoundationPressureSection", 108, "4778ECE8CF523D118DF0D0DFD6F94B4EA0AE7FD4E81015A9FF13B580EA0BC6E7"), (6, 640, "FoundationPressureSection", 108, "016605D4308B85DFBDD31E0295E668BAA53C56BBAAC0A0DC3062914E44B5E3FA"), (7, 640, "FoundationMechanismView", 132, "5D989404F510FA882642A3C6BCBE55E07777F82E9786A78EC2FCF9DD0008080F"), (0, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (1, 900, "FoundationInputPlan", 30, "B7E04C3DD4553369282030A6360E36B92617E10D757357B0C9AC312FE31F213E"), (2, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (3, 900, "FoundationPlanView", 28, "E3B525D03E27C513110EEA5700C7F6FF60F834611BEF4DB490B93D39B2772A27"), (4, 900, "FoundationPlanView", 1226, "20474E7261E28DB2EDBD6A64FAA00C02D0CD6210F10BBE359DE09A45D07890C6"), (5, 900, "FoundationPressureSection", 108, "51DD25096E4B1B21B6607D385C266A1763A5D3F0F74B3DD54CDC44FC013986FA"), (6, 900, "FoundationPressureSection", 108, "96D685DDB10B776D31E067ED4F0FDA383533E4A6FF9EF039C67ACBB651D817E7"), (7, 900, "FoundationMechanismView", 132, "057268D9F3E0D1B9012C125FE6FE48BC575394C0103F7195E8FDE14881C6A93F")],
        ["meyerhof-eccentrica-input-raccolti"] = [(0, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (1, 640, "FoundationInputPlan", 30, "4EBADD088ADFD3514CE7B54F853E71988E2ACF4A693FCE4F372990DCFD192F40"), (2, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (3, 640, "FoundationPlanView", 28, "696D5689176D3A133C877268BA7CD61CC55F6394EE5D4160636B11208C3A3135"), (4, 640, "FoundationPlanView", 1226, "D6216E7E66992FE6EFD41AC04429680B79E580C4840163230C21F095B2DC8A80"), (5, 640, "FoundationPressureSection", 108, "524C9D43954FE1C09B2D5BBE4333563E52A419117467CF8465DFBB0AC4327C37"), (6, 640, "FoundationPressureSection", 108, "C9925BB0138B8A17E3BFA60E0EEBDB48797CABE8ACAFF607B651233BAC1797D9"), (7, 640, "FoundationMechanismView", 132, "9D19945809A5081600A2340BBE6276B3649D9A0BF8E3E183DD25378BD802082B"), (0, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (1, 900, "FoundationInputPlan", 30, "CCA039872A62C81A905FEF5455EF5BB9E95AC83C504A70C2D871C65DC4425C66"), (2, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (3, 900, "FoundationPlanView", 28, "CA5384D2C650E6559702102532D06BB529443DEB26D80D7C3918722E8F21D3C4"), (4, 900, "FoundationPlanView", 1226, "1323CA46B919A4080794568A2BC3806023CE8DCF412A32E28A5FD3C88C325840"), (5, 900, "FoundationPressureSection", 108, "FB673B137C4F78B6E85142E275D29CBB03B9B2B78749FA35A5478804A0E5B702"), (6, 900, "FoundationPressureSection", 108, "BFDF45ADDC9C68BFDB7F6FB63B1F96196A4E5F1633806045699187120A5E288A"), (7, 900, "FoundationMechanismView", 132, "A16C0D36BBA898B04C790E44B7FAEE74F5F01C87FA1A6F22E8C134C6AB50543E")],
        ["semispazio-non-assunto"] = [(0, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (1, 640, "FoundationInputPlan", 30, "877D9701DF1BC2E5ABF4E9992C2DEB6932601C3FBC82AB5D96372A1CB4FB9F78"), (2, 640, "FoundationSoilView", 56, "3D060E476AA9B5815286FF0684695643E303C470F43B80EE64904944BA5B1C72"), (0, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB"), (1, 900, "FoundationInputPlan", 30, "B99F0E174DA25B96C5366A5FC77B3A10D080A2E45E40115425BE8B0BB5F5F749"), (2, 900, "FoundationSoilView", 59, "4E0DC7B35EF11E1877D0567D1879E6BA00254543034FBEA0E5ABED01DF9674EB")],
        ["strato-incompleto"] = [(0, 640, "FoundationSoilView", 9, "E801438A91EE72EED6DC7CB530CB2602DFB8F6F7F47EBA9CE81368D7B6BACC39"), (1, 640, "FoundationInputPlan", 30, "1EB09D3689DC467CF34992244C3EBB05BFCCF970F0E3253300AED75F1342044C"), (2, 640, "FoundationSoilView", 9, "E801438A91EE72EED6DC7CB530CB2602DFB8F6F7F47EBA9CE81368D7B6BACC39"), (0, 900, "FoundationSoilView", 9, "C312CB3B1491C639377E94EC67F036C5F73E3B9928A9E4716A4A0B7B00D43225"), (1, 900, "FoundationInputPlan", 30, "DE518521EDD102B236F63DA35B47B3BCD40988BE41156A1C2ED06A7250037665"), (2, 900, "FoundationSoilView", 9, "C312CB3B1491C639377E94EC67F036C5F73E3B9928A9E4716A4A0B7B00D43225")],
        ["terzaghi-nastriforme-falda"] = [(0, 640, "FoundationSoilView", 60, "E246D418A4B16A08A9124D1C9EA9201599B6983B620F0C6693EBEFC59DC232D9"), (1, 640, "FoundationInputPlan", 30, "829A97DEABBE705F9D83BF0F542F015D1DF3E1CD764172D47803A5172AA33F5B"), (2, 640, "FoundationSoilView", 60, "E246D418A4B16A08A9124D1C9EA9201599B6983B620F0C6693EBEFC59DC232D9"), (3, 640, "FoundationPlanView", 28, "4675E9803C2617E39FFDE57FBEC403E474D73EDD89496B248C285056280BF8B9"), (4, 640, "FoundationPlanView", 1226, "F0208C433110723DE1C706EA3F0E3BDDE52EB7434C487AE9CFC0FF68D51B6BE9"), (5, 640, "FoundationPressureSection", 108, "9D4E23F64CCC6999AD72ACA2E8CB076E51B72460C3D3981B6E28C451A756032B"), (6, 640, "FoundationPressureSection", 108, "200C4E59A921299C9A52D9013086F7962F2E4F9F73856E3BFBDB39496D10F279"), (7, 640, "FoundationMechanismView", 132, "CE5F7F58BA1BA3B2C4321A9863F3E29FB0E009A86C83A8B4DF61B11B50A9C456"), (0, 900, "FoundationSoilView", 60, "5E7DA38376EF6E0E618528677FE9641F788EF514B67F36942DC811093472746F"), (1, 900, "FoundationInputPlan", 30, "F74058F63209B5E01B198BC1518782F5FCCA1749E799E6C51EB95DB36989D384"), (2, 900, "FoundationSoilView", 60, "5E7DA38376EF6E0E618528677FE9641F788EF514B67F36942DC811093472746F"), (3, 900, "FoundationPlanView", 28, "3C511B59DD58D751468BC7DE483EFD3CE938A84F8221AEDB6B96EEFA2B79CEC1"), (4, 900, "FoundationPlanView", 1226, "9137E341305495197ABFA40945251C05362A6FBE4A2BBF1E9C7BB5C5A40803B3"), (5, 900, "FoundationPressureSection", 108, "244DFD2718E2BAB2F80DED73FC563776A9B1BCD7218093E59392C7AB3C638A90"), (6, 900, "FoundationPressureSection", 108, "E34FF20D828B540D439A05EACAA3A4DED250AEFE88345B2E200B699E6F55AEFE"), (7, 900, "FoundationMechanismView", 132, "CD22073C7B9FEE796AA86F7B590C5CF45EA2528A55E78281D85DEA8B2ED33DE6")],
        ["vesic-circolare-due-sondaggi"] = [(0, 640, "FoundationSoilView", 51, "45C3E03E29A6A3B06663D7C82548F08A42E8E0FA699D4B9A0B2BED8B8F02FAA9"), (1, 640, "FoundationInputPlan", 30, "1E98E392544219CE834EF9446DA1F2141895D824A8ADE3E92F71A22898D4E5D4"), (2, 640, "FoundationSoilView", 51, "45C3E03E29A6A3B06663D7C82548F08A42E8E0FA699D4B9A0B2BED8B8F02FAA9"), (3, 640, "FoundationPlanView", 27, "142FCBC9BC4FD8AD59CD0669E855688542A6A06ADDB5723F2E37B59AE6AF3C34"), (4, 640, "FoundationPlanView", 922, "6A9001AD29D94238D926964ECD7CBE2DD5366343008D96E8397D674E5F905FE9"), (5, 640, "FoundationPressureSection", 108, "89DEAF3C7F057ADF270B26A481D69F268C600563F41F41E19B90C5032A431515"), (6, 640, "FoundationPressureSection", 108, "B9A1DB8462CA91C17F0DFAE2D80668B1DD9B5F3ABBEF530D3664A61146F231FB"), (7, 640, "FoundationMechanismView", 132, "D9FED8BFD6956FB343448FFD7276742A1F0C36C1274805A161BAE28D335B697C"), (0, 900, "FoundationSoilView", 57, "9F7E6F6C984695E3C40877FC9490DB1982A0B25B699EAEE4FA54ADD8DD49F32E"), (1, 900, "FoundationInputPlan", 30, "CE01A96C90019113A23BE27E658C4631224C2F8B9FBB6AAF7D6A3F06AF5F9DDE"), (2, 900, "FoundationSoilView", 57, "9F7E6F6C984695E3C40877FC9490DB1982A0B25B699EAEE4FA54ADD8DD49F32E"), (3, 900, "FoundationPlanView", 27, "5AA0EAD865BFA350106D1CF57D9F0A041F3C470F71FF8F51DF5CFE01B0ADEBFC"), (4, 900, "FoundationPlanView", 922, "F5C86A6CF137B5F6FB2573A42B951D4DCA6D3DD6D7EBEB5A87CE1604E79A23BC"), (5, 900, "FoundationPressureSection", 108, "BE9A5C4913D38F9230D5EA4722EAB09B8457F43883328C661BE076848E66E6BF"), (6, 900, "FoundationPressureSection", 108, "2C17B59677A65BDFF6FE0133C2A2EF73BAF7AC3414C03B68FA7AA737E33A4F10"), (7, 900, "FoundationMechanismView", 132, "868542F41CEFFDF3618E8E19620FEA7F687FE1DD2976459E96B71ACE3EDAE7BB")],
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
        // F5.15, FN4 (F5-11, rule T6): the section of the failure mechanism is a view state and stays free in a read-only revision; the
        // recording of the historic view has it locked.
        string before = HistoricReadOnly.Replace("scelta meccanismo bloccata", "scelta meccanismo libera", StringComparison.Ordinal), after = Controls(host.Root, NewKey);
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
        Check(host.Editor.Data.ToJsonString(J.Options) == data0 && host.Modified == modified && side.Editor<TextBox>("Width").Text == shown && view.MechanismAxis.SelectedIndex == 1,
            "Sola lettura: digitazione e spunte rifiutate, la vista mostra di nuovo i dati; sezione del meccanismo ammessa senza scrivere (FN4)");
        view.MechanismAxis.SelectedIndex = 0;
        await host.Settle();
        var group = side.Group("metodo");
        group.IsExpanded = false; Click(side.Collect);
        await host.Settle();
        // Regola T6 (F5.15, F5-17): gruppi e «Raccogli input» restano nella copia in memoria del foglio, senza Modified.
        Check(host.Modified == modified && host.Editor.HasResults && host.Editor.Data["vista_fondazioni"]!["metodo"]!.GetValue<bool>() == false
            && host.Editor.Data["vista_fondazioni"]!["input_raccolti"]!.GetValue<bool>(), "Sola lettura: gruppi e «Raccogli input» restano in memoria senza Modified e conservano il risultato (FN3, T6)");
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
