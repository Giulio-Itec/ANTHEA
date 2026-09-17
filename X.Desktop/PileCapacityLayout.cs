namespace X.Desktop;

public sealed partial class FoglioEditor
{
    private bool PileCapacity=>Module=="geo_palo_verticale";

    private static void StylePileLayers(DataGridView grid)
    {
        grid.BackgroundColor=Color.FromArgb(248,250,252);
        grid.DefaultCellStyle.BackColor=grid.BackgroundColor;
        grid.DefaultCellStyle.Font=new Font("Segoe UI",15,FontStyle.Regular,GraphicsUnit.Pixel);
        grid.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
        grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor=Color.FromArgb(228,234,241);
        grid.SelectionMode=DataGridViewSelectionMode.CellSelect;
        grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",14,FontStyle.Bold,GraphicsUnit.Pixel);
        grid.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
        grid.ColumnHeadersHeight=76;
        grid.Columns["laterale_attiva"].HeaderText="Laterale\nattiva";
        foreach(var (key,title) in new[]{("__strato","Strato"),("__k","Coeff. k\n[-]"),("__mu","Coeff. μ\n[-]"),("__alfa","Coeff. α\n[-]"),("__nq","Fattore Nq\n[-]")})
            grid.Columns.Add(new DataGridViewTextBoxColumn{Name=key,HeaderText=title,ReadOnly=true,DefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.FromArgb(234,242,250)}});
        grid.CellFormatting+=(_,e)=>{if(e.RowIndex>=0&&grid.Columns[e.ColumnIndex].Name=="__strato"){e.Value=((char)('A'+e.RowIndex%26)).ToString();e.FormattingApplied=true;}};
        grid.Columns["spessore"].HeaderText="Spessore\nS [m]";
        grid.Columns["peso_specifico"].HeaderText="Peso specifico\nγ [kN/m³]";
        grid.Columns["peso_specifico_saturo"].HeaderText="Peso saturo\nγsat [kN/m³]";
        grid.Columns["angolo_attrito"].HeaderText="Attrito\nφ′ [°]";
        grid.Columns["coesione_efficace"].HeaderText="Coesione efficace\nc′ [kPa]";
        grid.Columns["coesione_non_drenata"].HeaderText="Coesione non drenata\nCu [kPa]";
        grid.Columns["nc"].HeaderText="Fattore Nc\n[-]";
        string[] order=["__strato","laterale_attiva","tipologia","addensamento","spessore","peso_specifico","peso_specifico_saturo","angolo_attrito","coesione_efficace","coesione_non_drenata","__k","__mu","__alfa","__nq","nc"];
        for(int i=0;i<order.Length;i++)grid.Columns[order[i]].DisplayIndex=i;
        foreach(DataGridViewRow row in grid.Rows)row.Height=38;
        grid.RowTemplate.Height=38;
        foreach(DataGridViewColumn column in grid.Columns){column.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;column.Width=156;column.SortMode=DataGridViewColumnSortMode.NotSortable;column.HeaderCell.Style.Alignment=DataGridViewContentAlignment.MiddleCenter;}
        grid.Columns["__strato"].Width=76;grid.Columns["laterale_attiva"].Width=90;
        grid.Columns["coesione_non_drenata"].Width=182;
        grid.ClearSelection();
    }

    private static void ArrangePileLayerActions(Panel panel,DataGridView grid)
    {
        var bar=panel.Controls.OfType<FlowLayoutPanel>().Single();
        bar.Dock=DockStyle.None;bar.BackColor=grid.BackgroundColor;bar.Height=34;bar.AutoScroll=false;
        var buttons=bar.Controls.OfType<Button>().ToArray();buttons[0].Text="Aggiungi strato";
        foreach(var button in buttons){button.FlatAppearance.BorderSize=0;button.BackColor=grid.BackgroundColor;button.Font=new Font("Segoe UI",14,FontStyle.Bold,GraphicsUnit.Pixel);button.AutoSize=false;button.Height=32;button.Margin=Padding.Empty;}
        panel.BackColor=grid.BackgroundColor;grid.Dock=DockStyle.None;
        void Arrange()
        {
            int height=Math.Min(Math.Max(70,panel.ClientSize.Height-36),grid.ColumnHeadersHeight+grid.Rows.GetRowsHeight(DataGridViewElementStates.Visible)+SystemInformation.HorizontalScrollBarHeight+3);
            grid.SetBounds(0,0,panel.ClientSize.Width,height);bar.SetBounds(0,height,panel.ClientSize.Width,34);
            foreach(var button in buttons)button.Width=Math.Max(70,(panel.ClientSize.Width-4)/2);
        }
        panel.Resize+=(_,_)=>Arrange();grid.RowsAdded+=(_,_)=>Arrange();grid.RowsRemoved+=(_,_)=>Arrange();Arrange();
    }

    private void ConfigurePileCards()
    {
        // Reuse the existing controls and bindings; only their presentation changes.
        var normative=originalCards[2];var body=normative.Controls.OfType<Panel>().First(p=>p is not TableLayoutPanel);
        var resetBar=body.Controls.OfType<FlowLayoutPanel>().Single();var reset=resetBar.Controls.OfType<Button>().Single();
        resetBar.Controls.Remove(reset);body.Controls.Remove(resetBar);resetBar.Dispose();
        var header=normative.Controls.OfType<TableLayoutPanel>().Single();header.ColumnStyles[1].Width=58;
        reset.Dock=DockStyle.Fill;reset.AutoSize=false;reset.Font=new Font("Segoe UI",8);reset.Padding=Padding.Empty;reset.Margin=new Padding(2);header.Controls.Add(reset,1,0);

        effLabel.BackColor=Color.FromArgb(234,242,250);effLabel.ForeColor=Ui.Navy;effLabel.Height=24;effLabel.Font=new Font("Segoe UI",8,FontStyle.Bold);
        verification.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        verification.ColumnHeadersHeight=44;verification.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",8,FontStyle.Bold);
        verification.Columns[0].HeaderText="Condizione";verification.Columns[1].HeaderText="NEd [kN]";verification.Columns[2].HeaderText="Rd [kN]";
        foreach(DataGridViewColumn column in verification.Columns){column.HeaderCell.Style.BackColor=Ui.Navy;column.HeaderCell.Style.ForeColor=Color.White;column.HeaderCell.Style.SelectionBackColor=Ui.Navy;column.HeaderCell.Style.SelectionForeColor=Color.White;column.SortMode=DataGridViewColumnSortMode.NotSortable;}
        verification.ColumnHeadersDefaultCellStyle.SelectionBackColor=Ui.Navy;verification.ColumnHeadersDefaultCellStyle.SelectionForeColor=Color.White;
        verification.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
        verification.CellBorderStyle=DataGridViewCellBorderStyle.None;verification.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
        verification.DefaultCellStyle.BackColor=Color.FromArgb(248,250,252);verification.DefaultCellStyle.SelectionBackColor=verification.DefaultCellStyle.BackColor;
        verification.DefaultCellStyle.SelectionForeColor=Ui.Navy;verification.AlternatingRowsDefaultCellStyle.BackColor=verification.DefaultCellStyle.BackColor;
        verification.BackgroundColor=verification.DefaultCellStyle.BackColor;
        foreach(DataGridViewRow row in verification.Rows)row.Height=48;
        verification.Columns[0].FillWeight=140;verification.Columns[0].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;verification.ClearSelection();

        var layersBody=originalCards[4].Controls.OfType<Panel>().First(p=>p is not TableLayoutPanel);
        var layerBar=layersBody.Controls.OfType<FlowLayoutPanel>().Single();
        layersBody.Controls.Remove(layerBar);
        var layerHeader=originalCards[4].Controls.OfType<TableLayoutPanel>().Single();
        layerHeader.ColumnCount=3;layerHeader.ColumnStyles.Add(new(SizeType.Absolute,58));
        layerBar.Dock=DockStyle.Fill;layerBar.AutoScroll=false;layerBar.Margin=Padding.Empty;layerBar.BackColor=Color.White;
        int i=0;foreach(var button in layerBar.Controls.OfType<Button>()){button.Text=i++==0?"+":"−";button.AutoSize=false;button.Size=new Size(27,26);button.Padding=Padding.Empty;button.Margin=Padding.Empty;button.FlatAppearance.BorderSize=0;}
        layerHeader.Controls.Add(layerBar,2,0);
        sondages!.Appearance=TabAppearance.FlatButtons;sondages.SizeMode=TabSizeMode.Fixed;sondages.ItemSize=new Size(28,24);sondages.Font=new Font("Segoe UI",8,FontStyle.Bold);
        foreach(TabPage page in sondages.TabPages)page.Padding=Padding.Empty;
    }

    private void FitPileFonts()
    {
        // Readable physical sizes, never the previous DPI-divided tiny text.
        var fonts=GetAll(this).Select(control=>(control,font:control.Font)).ToArray();
        var cellFonts=GetAll(this).OfType<DataGridView>().SelectMany(grid=>new[]{grid.DefaultCellStyle,grid.ColumnHeadersDefaultCellStyle}).Where(style=>style.Font is not null).Select(style=>(style,font:style.Font!)).ToArray();
        foreach(var (control,font) in fonts)
        {
            if(control.HasChildren||font.Unit==GraphicsUnit.Pixel)continue;
            control.Font=new Font(font.FontFamily,Math.Max(14,font.Size*1.7f),font.Style,GraphicsUnit.Pixel);
        }
        foreach(var (style,font) in cellFonts)if(font.Unit!=GraphicsUnit.Pixel)style.Font=new Font(font.FontFamily,14,font.Style,GraphicsUnit.Pixel);
    }
}
