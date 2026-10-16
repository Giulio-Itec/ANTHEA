using Anthea.Application.Presentation;
using X.Core;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    private void SectionMaterialChanged(string key)
    {
        var input = Data["sezione"]!.AsObject();
        foreach (string field in HorizontalPilePresentation.ApplySectionMaterial(input, key)) sectionFields.Set(field, input.S(field), true);
        if (key == "second_inner_enabled") ShowInnerRing(true);
        Changed();
    }

    /// <summary>Second ring of bars, as «Secondo anello interno» of the c.a. section: its fields appear only when it is active and
    /// start, like there, from the bars of the first ring and a clear distance of 30 mm.</summary>
    private void ShowInnerRing(bool fill)
    {
        var input = Data["sezione"]!.AsObject(); bool enabled = input.B("second_inner_enabled");
        if (fill) foreach (string key in HorizontalPilePresentation.FillInnerRing(input)) sectionFields.Set(key, input.S(key), true);
        foreach (string key in HorizontalPilePresentation.InnerRingKeys) sectionFields.ShowField(key, enabled);
    }

    private void UpdateSectionMaterialValues()
    {
        var input = Data["sezione"]!.AsObject();
        foreach (var (key, value) in HorizontalPreview.ConcreteStrengths(input))
            sectionFields.Set(key, value, true);
    }
}
