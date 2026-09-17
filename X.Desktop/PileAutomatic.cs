using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

public sealed partial class FoglioEditor
{
    private readonly System.Windows.Forms.Timer pileTimer=new(){Interval=450};
    private int pileRevision;
    private void InitializePileAutomatic()
    {
        calculate.Visible=false;
        pileTimer.Tick+=async(_,_)=>{if(Busy)return;pileTimer.Stop();if(!IsDisposed&&IsHandleCreated)await CalculateAsync();};
        Disposed+=(_,_)=>pileTimer.Dispose();
        sondages!.SelectedIndexChanged+=(_,_)=>{UpdatePileProfile();RefreshPileNq();};
        UpdatePileProfile();QueuePileCalculation();
    }
    private void QueuePileCalculation()
    {
        if(!PileCapacity||building||IsDisposed)return;
        pileRevision++;pileTimer.Stop();pileTimer.Start();
        status.Text="Aggiornamento automatico in attesa…";
        UpdatePileRawResult();
        UpdatePileCoefficients();
    }
    private void UpdatePileRawResult()
    {
        if(!PileCapacity)return;
        RefreshPileNq();
        var page=outputs.TabPages.Cast<TabPage>().FirstOrDefault(p=>p.Text=="Risultati JSON");
        if(page?.Controls.OfType<TextBox>().FirstOrDefault() is TextBox raw)raw.Text=Result?.ToJsonString(J.Options)??"Dati da completare · aggiornamento automatico";
    }
    private void UpdatePileProfile()
    {
        if(!PileCapacity)return;
        stratigraphy.SelectedIndex=sondages?.SelectedIndex??-1;
        stratigraphy.ShowAll=expandedCard==5;
        stratigraphy.Invalidate();
    }
    private void UpdatePileCoefficients()
    {
        if(sondages is null)return;
        var g=Data["generali"]!;double diameter=g.D("diametro");
        for(int s=0;s<sondages.TabCount;s++)
        {
            var grid=GetAll(sondages.TabPages[s]).OfType<DataGridView>().Single();
            if(!grid.Columns.Contains("__k"))continue;
            double depth=0;bool validDepth=true;var rows=Data.Array("stratigrafie")[s]!.AsArray();
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i]!;var cells=grid.Rows[i].Cells;
                var (k,mu)=Calcolo.CoefficientiLaterali(g,row);double? cu=J.Number(row["coesione_non_drenata"]);
                cells["__k"].Value=k?.ToString("0.###")??"—";cells["__mu"].Value=mu?.ToString("0.###")??"—";
                cells["__alfa"].Value=row.S("tipologia")=="Coesivo"&&cu>=0?Calcolo.CoefficienteAlfa(g,cu.Value).ToString("0.###"):"—";
                double? thickness=J.Number(row["spessore"]),phi=J.Number(row["angolo_attrito"]);
                validDepth&=thickness>0;if(validDepth)depth+=thickness!.Value;
                cells["__nq"].Value="—";cells["__nq"].ToolTipText="Nq al fondo dello strato (z/D); non è necessariamente il valore alla punta del palo.";
                if(validDepth&&double.IsFinite(depth)&&diameter>0&&phi is not null)
                {
                    var detail=Nq.Dettaglio(phi.Value,depth/diameter,diameter>.8);
                    cells["__nq"].Value=detail.D("nq").ToString("0.###");
                    cells["__nq"].ToolTipText=$"Nq al fondo dello strato: z={depth:0.###} m, z/D={depth/diameter:0.###}."+(detail.B("limite_phi")||detail.B("limite_rapporto")?" Valore limitato al bordo dell'abaco.":"");
                }
            }
        }
    }

    internal async Task VerifyPileAutomatic(string directory)
    {
        if(!PileCapacity)return;
        async Task Settled()
        {
            for(int i=0;i<100&&(pileTimer.Enabled||Busy);i++)await Task.Delay(50);
            if(pileTimer.Enabled||Busy)throw new InvalidOperationException("Calcolo automatico non terminato.");
        }
        await Settled();
        if(calculate.Visible||Result is null)throw new InvalidOperationException("Calcolo automatico iniziale non disponibile.");
        var saved=(JsonObject)Data.DeepClone();
        var reference=GetAll(outputs).OfType<Plot>().Single(p=>p.Name=="nq");
        if(reference.XMinimum!=25||reference.Series.SelectMany(s=>s.Points).Any(p=>p[0]<25))throw new InvalidOperationException("Asse Nq non parte da 25°.");
        var grid=GetAll(sondages!).OfType<DataGridView>().First();
        foreach(var key in new[]{"__k","__mu","__alfa","__nq","nc"})if(!grid.Columns.Contains(key))throw new InvalidOperationException("Coefficiente assente: "+key);
        var first=Data.Array("stratigrafie")[0]![0]!;var km=Calcolo.CoefficientiLaterali(Data["generali"]!,first);
        if(grid.Rows[0].Cells["__k"].Value?.ToString()!=km.K?.ToString("0.###"))throw new InvalidOperationException("k mostrato diverso dal motore.");
        void Capture(string name){PerformLayout();using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,ClientRectangle);bitmap.Save(Path.Combine(directory,name+".png"));}
        expandedCard=4;LayoutCards();Capture("palo_stratigrafia_estesa");grid.FirstDisplayedScrollingColumnIndex=grid.Columns["__k"].Index;Capture("palo_coefficienti");grid.FirstDisplayedScrollingColumnIndex=grid.Columns["__strato"].Index;
        var second=Data.Array("stratigrafie")[0]!.DeepClone();second!.AsArray().Last()!["angolo_attrito"]="32";
        Data.Array("stratigrafie").Add(second);RebuildSondages();sondages!.SelectedIndex=1;Changed();await Settled();expandedCard=-1;LayoutCards();
        if(reference.Markers.Count!=1||reference.Markers[0].X!=32||!reference.Markers[0].Label.StartsWith("S2:"))throw new InvalidOperationException("Il punto Nq non segue la stratigrafia selezionata.");
        if(!stratigraphy.VisibleIndices.SequenceEqual(new[]{1}))throw new InvalidOperationException("Il profilo non segue la selezione.");
        int profileHeight=originalCards[5].Height,profileTop=originalCards[5].Top,profileWidth=originalCards[5].Width;
        expandedCard=5;LayoutCards();if(stratigraphy.VisibleIndices.Length!=Data.Array("stratigrafie").Count)throw new InvalidOperationException("Il profilo esteso non mostra tutte le stratigrafie.");
        if(originalCards[5].Height!=profileHeight||originalCards[5].Top!=profileTop||originalCards[5].Width<=profileWidth||!originalCards[4].Visible||!originalCards[6].Visible)throw new InvalidOperationException("Espansione del profilo non esclusivamente laterale.");
        var dimensions=stratigraphy.PileDimensions(0);if(dimensions.Any(d=>Math.Abs(d.Bottom-d.Top-d.Thickness)>1e-9))throw new InvalidOperationException("Quote degli strati incoerenti.");Capture("palo_profili_estesi");
        Data["stratigrafie"]=saved["stratigrafie"]!.DeepClone();RebuildSondages();sondages.SelectedIndex=0;expandedCard=-1;LayoutCards();
        string length=generalForm!.Editors["lunghezza"].Text;
        generalForm.Set("lunghezza","");await Settled();if(Result is not null||reference.Markers.Count!=0)throw new InvalidOperationException("Risultati o punto Nq obsoleti su input incompleto.");
        generalForm.Set("lunghezza",length);await Settled();if(Result is null)throw new InvalidOperationException("Il calcolo automatico non riparte.");
        var pending=CalculateAsync();generalForm.Set("lunghezza",(Data["generali"].D("lunghezza")+.1).ToString(System.Globalization.CultureInfo.InvariantCulture));generalForm.Set("lunghezza",length);await pending;await Settled();
        if(!JsonNode.DeepEquals(Result,Calcolo.Calcola(Data)))throw new InvalidOperationException("Risultato automatico non riferito agli ultimi dati.");
        RefreshPileNq();
        var actualCurve=reference.Series.Single(s=>s.Highlighted);double ratio=Data["generali"].D("lunghezza")/Data["generali"].D("diametro");
        if(actualCurve.Points.Any(p=>Math.Abs(p[1]-Nq.Dettaglio(p[0],ratio,Data["generali"].D("diametro")>.8).D("nq"))>1e-9))throw new InvalidOperationException("Curva effettiva Nq incoerente.");
        var tip=Result!.Array("dettagli").Last()!.Array("sondaggi")[0]!;
        if(reference.Markers.Count!=1||Math.Abs(reference.Markers[0].Y-tip.D("nq"))>1e-9||reference.Markers[0].X!=tip.D("phi_punta"))throw new InvalidOperationException("Punto Nq diverso dal risultato alla punta.");
        if(verification.Columns[0].HeaderCell.Style.SelectionBackColor!=Ui.Navy||verification.Columns[1].HeaderText!="NEd [kN]"||verification.Columns[2].HeaderText!="Rd [kN]")throw new InvalidOperationException("Intestazioni verifica non corrette.");
        foreach(var form in new[]{generalForm!,normativeForm!})foreach(var label in GetAll(form).OfType<Label>().Where(c=>c.Parent is TableLayoutPanel t&&t.GetColumn(c)==0))if(label.Text.Contains('\n')||label.Width<TextRenderer.MeasureText(label.Text,label.Font,Size.Empty,TextFormatFlags.SingleLine).Width)throw new InvalidOperationException("Etichetta non contenuta su una riga: "+label.Text);
        if(GetAll(generalForm!).Where(c=>c is Label or TextBox or ComboBox or CheckBox).Any(c=>c.Font.Unit!=GraphicsUnit.Pixel||c.Font.Size!=15))throw new InvalidOperationException("Testi dei dati generali di dimensione diversa.");
        expandedCard=6;LayoutCards();outputs.SelectedTab=reference.Parent as TabPage;Capture("palo_nq_effettivo");expandedCard=-1;LayoutCards();
        if(Data.Array("stratigrafie").Any(s=>s!.AsArray().Any(r=>r!.AsObject().Any(p=>p.Key.StartsWith("__")))))throw new InvalidOperationException("Colonne di presentazione salvate nei dati.");
    }
}
