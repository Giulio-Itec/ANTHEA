using Anthea.Application.Concrete;
using Anthea.Application.Walls;
using Anthea.Application.Materials;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.RetainingWall;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    private readonly TextBlock wallCoverStatus = Ui.Text("", 11, color: Ui.Muted);
    private UIElement BuildWallMaterials()
    {
        if (wallCoverStatus.Parent is Panel parent) parent.Children.Remove(wallCoverStatus);
        var m = Data["materials"]!.AsObject();
        var form = Form("materials", RetainingWallFields.Materials(m), handler: key =>
        {
            if (key is "classe_cls" or "classe_acciaio")
            {
                RetainingWallPresentation.ApplyMaterialChoice(Data, key);
                foreach (string field in Forms["materials"].Editors.Keys.Where(k => !k.StartsWith("__"))) Forms["materials"].Set(field, m.S(field), true);
            }
        });
        foreach (var group in RetainingWallFields.MaterialGroups) form.GroupFields(group.Title, [.. group.Keys], group.Expanded);
        var cover = Form("material_cover", RetainingWallFields.Cover, Data["detailing"]!.AsObject());
        return Ui.Stack(Ui.Text("Stessi cataloghi GPC, legami e proprietà del verificatore delle sezioni in calcestruzzo. Nei progetti i materiali e la durabilità seguono i dati comuni del ramo; le differenze sono segnalate nel confronto.", 11, color: Ui.Muted),
            form, Group("Durabilità e copriferro · scheda Materiali", Ui.Stack(cover, wallCoverStatus,
                Ui.Button("Adotta copriferro minimo", () => { try { Commit(); m["cover"] = WallModel.RequiredCover(Data); form.Set("cover", m.S("cover"), true); UpdateFields(); Changed(); } catch (ArgumentException ex) { wallCoverStatus.Text = ex.Message; } })), false),
            BuildGravityMaterial());
    }
    private void UpdateWallMaterials()
    {
        // Visibility and enabled fields: RetainingWallPresentation.InputRules (UpdateFields); here the values calculated from the materials.
        var form = Forms["materials"];
        try
        {
            var preview = Anthea.Application.Presentation.WallMaterialPreview.Read(Data);
            foreach (var (key, value) in preview.Fields) form.Set(key, value, true);
            wallCoverStatus.Text = preview.CoverDescription;
        }
        catch (ArgumentException ex) { foreach (string key in new[] { "__fcd", "__fyd", "__ecm" }) form.Set(key, "—", true); wallCoverStatus.Text = ex.Message; }
    }
}
