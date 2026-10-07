namespace Materiali;

// UiTests configuration only (X.Materiali.csproj): self-checks of the materials sheet, called by --smoke-materials
// (MaterialsSmokeChecks: active.Check()). Moved unchanged out of App.xaml.cs, Bond.cs, MixAutomation.cs and
// ExposureSelector.cs; Durability.Check and NtcCover.Check are now DurabilityReferenceChecks (supporto/test/Shared).
public sealed partial class MaterialView
{
    public void Check()
    {

        foreach(var item in Classes)
        {
            var m=Material(item.Fck);
            if(!double.IsFinite(m.Ecm) || m.Ecm<=0 || Math.Abs(Math.Abs(m.Fck)-item.Fck)>1e-9)
                throw new Exception("Proprietà non valide: "+item.Name);
        }
        var baseline=Material(30);
        if(Math.Abs(Math.Abs(baseline.Fcm)-38)>1e-6 || Math.Abs(baseline.Ecm-32836.568)>1 ||
           Math.Abs(Math.Abs(baseline.Fctm)-2.896468)>1e-5 ||
           Math.Abs(Math.Abs(baseline.StrainUCompression)-.0035)>1e-8)
            throw new Exception("Valori C30/37 inattesi.");
        choice.SelectedItem="C50/60";
        if(strength.Text!=Number(50)) throw new Exception("Aggiornamento classe non riuscito.");
        choice.SelectedItem="C30/37";
        DurabilityReferenceChecks.CheckDurability();
        CheckBond();
        DurabilityReferenceChecks.CheckNtcCover();
        CheckAutomaticMix();
        CheckExposureSelector();
        exposureChecks["XC1"].IsChecked=false; exposureChecks["XF2"].IsChecked=true;
        if(!coverHeadline.Text.Contains("45")) throw new Exception("XF2 NTC trave errato.");
        choices["deviationControl"].SelectedIndex=1;
        choices["deviationValue"].SelectedItem="5 mm";
        if(!coverHeadline.Text.Contains("40")) throw new Exception("Tolleranza ridotta errata.");
        choices["deviationControl"].SelectedIndex=2;
        choices["deviationValue"].SelectedItem="0 mm";
        if(!coverHeadline.Text.Contains("35")) throw new Exception("Tolleranza zero errata.");
        choices["deviationControl"].SelectedIndex=0;
        if(Selected("deviationValue")!="10 mm" || choices["deviationValue"].Items.Count!=1 || !coverHeadline.Text.Contains("45"))
            throw new Exception("Ripristino tolleranza ordinaria errato.");
        choices["ntcElement"].SelectedIndex=1;
        if(!coverHeadline.Text.Contains("40")) throw new Exception("XF2 NTC piastra errato.");
        choices["coverMethod"].SelectedIndex=1;
        if(coverHeadline.Text!="Da completare") throw new Exception("XF2 EC2 non invalidato.");
        choices["coverMethod"].SelectedIndex=0; choices["ntcElement"].SelectedIndex=0;
        exposureChecks["XF2"].IsChecked=false;

        exposureChecks["XC1"].IsChecked=false; exposureChecks["XS3"].IsChecked=true; exposureChecks["XF4"].IsChecked=true;
        if(!coverHeadline.Text.Contains("60")) throw new Exception("Combinazione esposizioni non aggiornata.");
        numbers["diameter"].Text="NaN";
        if(coverHeadline.Text!="Da completare") throw new Exception("Copriferro obsoleto con input non valido.");
        numbers["diameter"].Text="16";
        choices["consistency"].SelectedIndex=4;
        exposureChecks["XS3"].IsChecked=false; exposureChecks["XF4"].IsChecked=false; exposureChecks["XF2"].IsChecked=true;


    }

    // From Bond.cs
    void CheckBond()
    {
        var good=Bond(30,16,1,1,1.5);
        if(Math.Abs(good.Fbd-3.041291)>1e-5) throw new Exception("Benchmark aderenza C30/37 errato.");
        if(Math.Abs(Bond(30,40,.7,1,1.5).Fbd-good.Fbd*.7*.92)>1e-10) throw new Exception("Coefficienti aderenza errati.");
        if(Bond(90,16,1,1,1.5).Fbd!=Bond(60,16,1,1,1.5).Fbd) throw new Exception("Limite C60/75 mancante.");
        choices["bondCondition"].SelectedIndex=0;
        if(!bondValue.Text.Contains(good.Fbd.ToString("0.00"))) throw new Exception("Aggiornamento aderenza errato: " + bondValue.Text + " · " + bondDetails.Text);
        numbers["bondGamma"].Text="0";
        if(bondValue.Text!="Da completare") throw new Exception("Risultato aderenza obsoleto.");
        numbers["bondGamma"].Text="1,5";
    }

    // From MixAutomation.cs
    void CheckAutomaticMix()
    {
        void Expect(string key,string expected){if(compositionValues[key].Text!=expected)throw new Exception($"ATECAP {key}: {compositionValues[key].Text}, atteso {expected}");}
        numbers["aggregate"].Text="32";
        foreach(var (code,ratio,cement) in new[]{("X0","Non prescritto","Non prescritto"),("XC1","0,60","300"),("XC2","0,60","300"),("XC3","0,55","320"),("XC4","0,50","340"),("XS1","0,50","340"),("XS2","0,45","360"),("XS3","0,45","360"),("XD1","0,55","320"),("XD2","0,50","340"),("XD3","0,45","360"),("XA1","0,55","320"),("XA2","0,50","340"),("XA3","0,45","360"),("XF1","0,50","320"),("XF2","0,50","340"),("XF4","0,45","360"),("XF3","0,50","340")})
        {exposureSelector.SelectedItem=code;Expect("ratio",ratio);Expect("cement",cement);}
        Expect("air","4,0");numbers["aggregate"].Text="16";Expect("air","5,0");
        foreach(var d in new[]{"20","18","8"}){numbers["aggregate"].Text=d;Expect("air","Da definire");}
        numbers["aggregate"].Text="NaN";Expect("dmax","Da definire");Expect("air","Da definire");
        numbers["aggregate"].Text="32";exposureChecks["XS3"].IsChecked=true;Expect("ratio","0,45");Expect("cement","360");Expect("air","4,0");
        exposureSelector.SelectedItem="X0";Expect("ratio","Non prescritto");Expect("cement","Non prescritto");Expect("air","Non prescritto");
        exposureChecks["XC4"].IsChecked=true;Expect("ratio","Da definire");Expect("cement","Da definire");
        exposureSelector.SelectedItem="XC4";choices["life"].SelectedIndex=1;
        if(!compositionSource.Text.Contains("100 anni"))throw new Exception("Nota vita utile assente.");
        choices["cement"].SelectedIndex=1;Expect("cement","340");Expect("chloride","Cl 0,40");
        if(compositionValues.Values.Any(x=>!x.IsReadOnly))throw new Exception("Campo automatico modificabile.");
        choices["cement"].SelectedIndex=0;choices["life"].SelectedIndex=0;numbers["aggregate"].Text="20";exposureSelector.SelectedItem="XC1";
    }

    // From ExposureSelector.cs
    void CheckExposureSelector()
    {
        foreach(var (codes,expected) in new[]{("X0 XC1 XC2 XC3 XF1","ordinaria"),("XC4 XD1 XS1 XA1 XA2 XF2 XF3","aggressiva"),("XD2 XD3 XS2 XS3 XA3 XF4","molto aggressiva")})
            foreach(var code in codes.Split(' '))
            {
                exposureSelector.SelectedItem=code;
                if(!minimumConcreteClass.IsReadOnly || minimumConcreteClass.Text!=MinimumConcrete.Label(MinimumConcrete.Fck(code))) throw new Exception("Classe minima non aggiornata: "+code);
                if(Active().Length!=1||Active()[0].Code!=code||exposureDetails.Text!=ExposureDescriptions[code]||exposureExamples.Text!=ExposureExamples[code]||string.IsNullOrWhiteSpace(exposureExamples.Text)||exposureAggressiveness.Text!="Aggressività: "+expected)
                    throw new Exception("Menu/descrizione/aggressività non aggiornati: "+code);
            }
        exposureSelector.SelectedItem="XC1";exposureChecks["XS3"].IsChecked=true;
        if(!combinedExposure.Text.Contains("molto aggressiva")||minimumConcreteClass.Text!="C35/45")throw new Exception("Combinazione non aggiornata.");
        exposureSelector.SelectedItem="XC4";
        if(Active().Length!=1||minimumConcreteClass.Text!="C32/40")throw new Exception("Cambio menu non propagato al calcolo.");
        exposureSelector.SelectedItem="XC1";
    }
}
