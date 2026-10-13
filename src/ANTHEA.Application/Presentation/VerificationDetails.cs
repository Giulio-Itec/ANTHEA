namespace Anthea.Application.Presentation;

/// <summary>Text and semantic state shared by the visual cards and nonvisual reports.</summary>
public sealed record VerificationDetail(string Title, bool? Passed, string Values, string Explanation, string Reference)
{
    public string State => Passed is null ? "DA COMPLETARE" : Passed.Value ? "VERIFICATA" : "NON VERIFICATA";
    public string Text => $"{Title}\n{State}\n{Values}\n{Explanation}\n{Reference}\n\n";
}

public sealed class VerificationDetails
{
    private readonly List<VerificationDetail> items = [];
    private string text = "";
    public IReadOnlyList<VerificationDetail> Items => items.AsReadOnly();
    public string Text { get => text; set { Start(); text = value; } }
    public void Start() { items.Clear(); text = ""; }
    public void AddDetail(string title, bool? passed, string values, string explanation, string reference)
    {
        var detail = new VerificationDetail(title, passed, values, explanation, reference);
        items.Add(detail); text += detail.Text;
    }
}
