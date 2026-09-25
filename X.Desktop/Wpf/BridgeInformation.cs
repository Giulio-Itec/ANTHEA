using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private void ShowModelInformation()
    {
        var content = Ui.Stack(
            Block("Criteri di calcolo", Ui.Text(BridgeSection.Method + "\n\n" +
                "Larghezze efficaci secondo EN 1993-1-5:2006, §4.4 e tabelle 4.1–4.2. Per ogni situazione si sommano i contributi delle fasi e si aggiorna la sezione efficace fino a convergenza.", 12)),
            Block("Campo del modello", Ui.Text(BridgeSection.Scope, 12)),
            Block("Come leggere le fasi", Ui.Text(
                "La situazione selezionata comprende gli incrementi delle fasi attive fino a quella fase. La selezione aggiorna grafico, riepilogo e tabelle anche nel pannello di controllo. " +
                "Geometria e materiali sono comuni; tensioni, larghezze efficaci e parte esclusa dell’anima dipendono dalle azioni. I rapporti σ/limite sono controlli locali.\n\n" +
                "L’anima semitrasparente con contorno arancio è la porzione esclusa dal modello a larghezze efficaci per tensioni normali, non una deformata né una previsione di instabilità a taglio. " +
                "Le piattabande sono controllate come sbalzi con la tensione più compressiva nello spessore.", 12)),
            Block("Omogeneizzazione e convenzioni", Ui.Text(
                "n₀ = Ea/Ecm\nn = n₀ · (1 + ψL · φ)\nφ = (n/n₀ − 1) / ψL\n\n" +
                "A*, Ix* e W* sono riferiti all’acciaio strutturale. Le aree delle barre sostituiscono il corrispondente calcestruzzo; il rapporto Es/Ea è mantenuto. " +
                "y è misurato dall’interfaccia verso l’alto. Ogni ΔMx è riferito alla quota y_ref assegnata; il trasporto a G include N·(yG−y_ref).\n\n" +
                "Checker integra le pareti sottili sulla linea media e le armature come aree concentrate: l’inerzia di integrazione può differire dall’inerzia geometrica completa. " +
                "La tabella Fasi e proprietà riporta entrambe le informazioni e il residuo dell’equilibrio N–Mx, controllato indipendentemente (limite 1E−5).", 12)),
            Block("Librerie e documentazione", Ui.Text(
                "Model: cataloghi, distribuzione delle barre, SectionH e proprietà omogeneizzate. Checker: tensioni elastiche delle fasi composte. " +
                "ANTHEA: tensioni acciaio/soletta esclusa, iterazione delle larghezze efficaci e sovrapposizione delle tensioni.\n\n" +
                "Confronti con i test Bridge di Checker: supporto/docs/sezione-mista-ponte.md.", 12)));
        var dialog = Ui.Dialog(this, "Sezione composta · informazioni sul modello", new Border(), 780, 700);
        dialog.Content = Ui.Paper(Ui.Dock(Scroll(content), bottom: Ui.Bar(Ui.Button("Chiudi", dialog.Close))), 18);
        dialog.ShowDialog();
    }
}
