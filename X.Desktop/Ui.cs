using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

public static class Ui
{
    public static readonly Color Navy=Color.FromArgb(11,42,74),Blue=Color.FromArgb(11,92,173),Bg=Color.FromArgb(243,245,248),Muted=Color.FromArgb(100,116,139);
    public static Button Button(string title,Action action,bool primary=false)
    {
        var b=new Button{Text=title,AutoSize=true,Height=32,FlatStyle=FlatStyle.Flat,BackColor=primary?Navy:Color.White,ForeColor=primary?Color.White:Navy,Padding=new Padding(6,2,6,2),Margin=new Padding(4)};b.FlatAppearance.BorderColor=Color.FromArgb(220,226,233);b.Click+=(_,_)=>action();return b;
    }
    public static DataGridView Grid(bool editable=false)
    {
        var grid=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,AllowUserToAddRows=false,AllowUserToDeleteRows=false,ReadOnly=!editable,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,EnableHeadersVisualStyles=false,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize};
        grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(232,239,247);grid.ColumnHeadersDefaultCellStyle.ForeColor=Navy;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(232,239,247);grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Navy;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(247,249,252);grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(220,234,248);grid.DefaultCellStyle.SelectionForeColor=Color.Black;grid.RowTemplate.Height=28;grid.DataError+=(_,e)=>{e.ThrowException=false;};return grid;
    }
    public static TabPage Tab(TabControl tabs,string title,Control control)
    {
        var p=new TabPage(title){BackColor=Color.White,Padding=new Padding(8)};control.Dock=DockStyle.Fill;p.Controls.Add(control);tabs.TabPages.Add(p);return p;
    }
    public static Panel WithToolbar(Control content,params Control[] buttons)
    {
        var panel=new Panel{Dock=DockStyle.Fill};var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=44,WrapContents=false,AutoScroll=true,BackColor=Bg};bar.Controls.AddRange(buttons);content.Dock=DockStyle.Fill;panel.Controls.Add(content);panel.Controls.Add(bar);return panel;
    }
    public static string? Ask(IWin32Window owner,string title,string initial="")
    {
        using var dialog=new Form{Text=title,Width=450,Height=165,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false};var text=new TextBox{Text=initial,Left=18,Top=18,Width=395};var ok=new Button{Text="Conferma",DialogResult=DialogResult.OK,Left=218,Top=65,Width=95};var cancel=new Button{Text="Annulla",DialogResult=DialogResult.Cancel,Left=320,Top=65,Width=95};dialog.Controls.AddRange([text,ok,cancel]);dialog.AcceptButton=ok;dialog.CancelButton=cancel;return dialog.ShowDialog(owner)==DialogResult.OK?text.Text:null;
    }
}

public sealed record Field(string Key,string Label,string Unit="",string[]? Choices=null,bool Bool=false,bool ReadOnly=false);
public sealed class InputForm:UserControl
{
    private readonly ToolTip fieldTips=new();
    private readonly bool sectionStyle;
    private readonly bool capacityStyle;
    private bool displayOnly;
    private readonly Dictionary<string,int> headingRows=new();
    public int ContentHeight=>Controls.OfType<TableLayoutPanel>().First().PreferredSize.Height;
    public readonly Dictionary<string,Control> Editors=new();private readonly Dictionary<string,Control> labels=new();
    public int MinimumContentWidth{get;private set;}
    public InputForm(JsonObject values,IEnumerable<Field> fields,Action<string> changed,bool capacityStyle=false)
    {
        this.capacityStyle=capacityStyle;
        AutoScaleMode=AutoScaleMode.None;Dock=DockStyle.Fill;AutoScroll=true;BackColor=Color.White;var definitions=fields.ToArray();sectionStyle=definitions.Any(f=>f.Key is "shape" or "classe_cls" or "longitudinal_bar_count");
        var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=4,Padding=new Padding(sectionStyle?0:4)};
        table.ColumnStyles.Add(new(SizeType.Percent,100));table.ColumnStyles.Add(new(SizeType.Absolute,sectionStyle?42:56));table.ColumnStyles.Add(new(SizeType.Absolute,sectionStyle?90:110));table.ColumnStyles.Add(new(SizeType.Absolute,sectionStyle?44:66));int row=0;
        foreach(var field in definitions)
        {
            string? group=field.Key switch{"classe_cls"=>"Calcestruzzo","__esu"=>"Acciaio · B450C","longitudinal_bar_count"=>"Longitudinale","top_bar_count"=>"Superiore","bottom_bar_count"=>"Inferiore","side_bar_count_per_side"=>"Laterale · per lato","transverse_bar_diameter_mm"=>"Armatura trasversale",_=>null};
            if(group is not null){table.RowStyles.Add(new(SizeType.Absolute,26));var heading=new Label{Text=group,Font=new Font("Segoe UI",9,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};table.Controls.Add(heading,0,row);table.SetColumnSpan(heading,4);headingRows[field.Key]=row++;}
            (string caption,string symbol)=field.Key switch
            {
                "numero_pali_x"=>("Pali in X",""),"numero_pali_y"=>("Pali in Y",""),"interasse_x"=>("Interasse X",""),"interasse_y"=>("Interasse Y",""),
                "__ec2"=>("Deformazione","εc2"),"__ecu"=>("Ultima","εcu"),"fck_mpa"=>("Resistenza","fck"),"__fcd"=>("Progetto","fcd"),"__ecm"=>("Modulo medio","Ecm"),"__esu"=>("Ultima (informativa)","εsu"),"__fyd"=>("Progetto","fyd"),"__esyd"=>("Snervamento","εsyd"),"n"=>("Omogeneizzazione","n"),"steel_modulus_mpa"=>("Modulo","Es"),"classe_cls"=>("Classe",""),
                "longitudinal_bar_count" or "top_bar_count" or "bottom_bar_count" or "side_bar_count_per_side"=>("Quantità","n"),"longitudinal_bar_diameter_mm" or "top_bar_diameter_mm" or "bottom_bar_diameter_mm" or "side_bar_diameter_mm"=>("Diametro","Ø"),"transverse_bar_diameter_mm"=>("Diametro staffa","Øt"),"transverse_spacing_mm"=>("Passo","s"),
                "diameter_mm"=>("Diametro","D"),"width_mm"=>("Larghezza","b"),"height_mm"=>("Altezza","h"),"flange_width_mm"=>("Larghezza ala","bf"),"web_width_mm"=>("Anima","bw"),"flange_thickness_mm"=>("Spessore ala","hf"),"cover_mm"=>("Copriferro netto","c"),
                "diametro"=>(field.Label.Contains("perforazione")?"Diametro di perforazione":"Diametro palo",field.Label.Contains("perforazione")?"Db":"D"),"lunghezza"=>("Lunghezza palo","L"),"peso_specifico_palo"=>("Peso specifico","γ"),"profondita_falda"=>("Profondità falda","zf"),"azione_compressione"=>("Azione assiale di progetto - Compressione","NEd,c"),"azione_trazione"=>("Azione assiale di progetto - Trazione","NEd,t"),
                "__xi3"=>("Coefficiente di correlazione","ξ3"),"__xi4"=>("Coefficiente di correlazione","ξ4"),"sicurezza_laterale_compressione"=>("Sicurezza laterale - Compressione","γs"),"sicurezza_laterale_trazione"=>("Sicurezza laterale - Trazione","γt"),"sicurezza_base"=>("Sicurezza di base","γb"),"peso_palo_sfavorevole"=>("Peso proprio palo - Sfavorevole","γG,sfav"),"peso_palo_favorevole"=>("Peso proprio palo - Favorevole","γG,fav"),_ =>(field.Label,"")
            };
            table.RowStyles.Add(new(SizeType.Absolute,capacityStyle?36:sectionStyle?26:34));var label=new Label{Text=caption,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=false,Font=new Font("Segoe UI",8),Margin=Padding.Empty};fieldTips.SetToolTip(label,field.Label+(field.Unit!=""?" ["+field.Unit+"]":""));labels[field.Key]=label;table.Controls.Add(label,0,row);Control editor;
            var symbolLabel=new Label{Text=symbol,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Ui.Muted,Font=new Font("Segoe UI",8),Margin=Padding.Empty};table.Controls.Add(symbolLabel,1,row);
            if(capacityStyle){symbolLabel.ForeColor=Ui.Navy;symbolLabel.Font=new Font("Segoe UI",9,FontStyle.Bold);}
            if(field.Bool)
            {
                var check=new CheckBox{Checked=values.B(field.Key),Dock=DockStyle.Fill};check.CheckedChanged+=(_,_)=>{values[field.Key]=check.Checked;changed(field.Key);};editor=check;
            }
            else if(field.Choices is not null)
            {
                var cb=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DropDownWidth=320};cb.Items.AddRange(field.Choices);var value=values.S(field.Key);if(value!=""&&!cb.Items.Contains(value))cb.Items.Add(value);cb.SelectedItem=value;cb.SelectedIndexChanged+=(_,_)=>{values[field.Key]=cb.Text;changed(field.Key);};editor=cb;
            }
            else
            {
                var text=new NumericField(values.S(field.Key),field.ReadOnly){Dock=DockStyle.Fill};if(sectionStyle)text.SectionStyle();if(capacityStyle)text.Rounded=false;if(!field.ReadOnly)text.TextChanged+=(_,_)=>{if(displayOnly)return;values[field.Key]=text.Text;changed(field.Key);};editor=text;
            }
            editor.Font=capacityStyle?new Font("Segoe UI",15,FontStyle.Regular,GraphicsUnit.Pixel):new Font("Segoe UI",sectionStyle?8:9);editor.Margin=new Padding(4,2,4,2);Editors[field.Key]=editor;table.Controls.Add(editor,2,row);
            if(field.Bool||field.Key=="metodo")
            {table.Controls.Remove(label);table.Controls.Remove(symbolLabel);label.Dispose();symbolLabel.Dispose();labels[field.Key]=editor;if(field.Bool)((CheckBox)editor).Text=caption;table.SetColumn(editor,0);table.SetColumnSpan(editor,4);}
            else if(field.Choices is not null&&field.Unit==""&&!sectionStyle){table.Controls.Remove(symbolLabel);symbolLabel.Dispose();table.SetColumn(editor,1);table.SetColumnSpan(editor,3);}
            else table.Controls.Add(new Label{Text=field.Unit,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Ui.Muted,Font=new Font("Segoe UI",8),Margin=Padding.Empty},3,row);
            row++;
        }
        if(definitions.Any(f=>f.Key=="metodo")){table.ColumnStyles[1].Width=0;table.ColumnStyles[2].Width=64;table.ColumnStyles[3].Width=26;}
        if(definitions.Any(f=>f.Key=="verticali_indagate")){table.ColumnStyles[1].Width=65;table.ColumnStyles[2].Width=74;table.ColumnStyles[3].Width=0;}
        if(capacityStyle)
        {
            table.Padding=Padding.Empty;
            if(definitions.Any(f=>f.Key=="diametro"))
            {
                table.ColumnStyles.Clear();
                table.ColumnStyles.Add(new(SizeType.Percent,46));table.ColumnStyles.Add(new(SizeType.Absolute,62));
                table.ColumnStyles.Add(new(SizeType.Percent,28));table.ColumnStyles.Add(new(SizeType.Percent,26));
                foreach(var key in new[]{"tipo_palo","sottotipo_palo_battuto"})if(Editors.TryGetValue(key,out var choice)){table.SetColumn(choice,1);table.SetColumnSpan(choice,3);}
            }
            if(definitions.Any(f=>f.Key=="verticali_indagate")){table.ColumnStyles[1].Width=62;table.ColumnStyles[2].Width=70;}
            if(Editors.TryGetValue("metodo",out var method))
            {
                table.RowStyles.Insert(0,new(SizeType.Absolute,18));
                foreach(Control control in table.Controls)table.SetRow(control,table.GetRow(control)+1);
                var methodLabel=new Label{Text="Metodo",Dock=DockStyle.Fill,Font=new Font("Segoe UI",8),Margin=Padding.Empty};table.Controls.Add(methodLabel,0,0);table.SetColumnSpan(methodLabel,4);
            }
        }
        Controls.Add(table);
        if(capacityStyle)
        {
            void Uniform(Control parent){foreach(Control child in parent.Controls){child.Font=new Font("Segoe UI",15,child.Font.Style,GraphicsUnit.Pixel);Uniform(child);}}
            Uniform(this);
            if(definitions.Any(f=>f.Key is "diametro" or "verticali_indagate"))
            {
                int labelWidth=table.Controls.OfType<Label>().Where(c=>table.GetColumn(c)==0).Select(c=>TextRenderer.MeasureText(c.Text,c.Font,Size.Empty,TextFormatFlags.SingleLine).Width+12).DefaultIfEmpty(200).Max();
                bool general=definitions.Any(f=>f.Key=="diametro");
                table.ColumnStyles.Clear();table.ColumnStyles.Add(new(SizeType.Absolute,labelWidth));table.ColumnStyles.Add(new(SizeType.Absolute,68));table.ColumnStyles.Add(new(SizeType.Absolute,general?110:76));table.ColumnStyles.Add(new(SizeType.Absolute,general?66:0));
                MinimumContentWidth=labelWidth+68+(general?176:76);table.MinimumSize=new Size(MinimumContentWidth,0);
            }
        }
    }
    public void Set(string key,string value){if(Editors.TryGetValue(key,out var e))e.Text=value;}
    public void SetDisplay(string key,string value){displayOnly=true;try{Set(key,value);}finally{displayOnly=false;}}
    public void CompactRows(){}
    protected override void Dispose(bool disposing){if(disposing)fieldTips.Dispose();base.Dispose(disposing);}
    public void Enable(string key,bool enabled){if(Editors.TryGetValue(key,out var e))e.Enabled=enabled;if(labels.TryGetValue(key,out var l))l.Enabled=enabled;}
    public void ShowField(string key,bool shown)
    {
        if(!Editors.TryGetValue(key,out var editor)||editor.Parent is not TableLayoutPanel table)return;
        int row=table.GetRow(editor);table.RowStyles[row].Height=shown?(capacityStyle?36:sectionStyle?26:34):0;
        foreach(Control c in table.Controls)if(table.GetRow(c)==row)c.Visible=shown;
        if(headingRows.TryGetValue(key,out int headingRow)){table.RowStyles[headingRow].Height=shown?26:0;foreach(Control c in table.Controls)if(table.GetRow(c)==headingRow)c.Visible=shown;}
    }
}
