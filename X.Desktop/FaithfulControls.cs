using System.Drawing.Drawing2D;

namespace X.Desktop;

// Controlli di sola presentazione: colori e geometrie ripresi da widgets.py.
internal sealed class PaperPanel:Panel
{
    public PaperPanel(){DoubleBuffered=true;BackColor=Color.White;ResizeRedraw=true;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(Color.FromArgb(220,226,233));e.Graphics.DrawRectangle(pen,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));}
}

internal sealed class NumericField:UserControl
{
    private readonly TextBox input=new(){BorderStyle=BorderStyle.None,TextAlign=HorizontalAlignment.Center,Dock=DockStyle.None,Font=new Font("Segoe UI",9),BackColor=Color.FromArgb(248,250,252)};
    public bool Rounded{get;set;}=true;
    public NumericField(string value,bool readOnly)
    {
        AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.White;Height=28;input.ReadOnly=readOnly;input.Text=value;
        input.BackColor=readOnly?Color.FromArgb(234,242,250):Color.FromArgb(248,250,252);
        input.TextChanged+=(_,_)=>OnTextChanged(EventArgs.Empty);input.Enter+=(_,_)=>Invalidate();input.Leave+=(_,_)=>Invalidate();Controls.Add(input);EnabledChanged+=(_,_)=>Invalidate();
    }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text{get=>input?.Text??"";set{if(input is not null)input.Text=value??"";}}
    public void SectionStyle(){Rounded=false;input.Font=new Font("Segoe UI",8);input.TextAlign=HorizontalAlignment.Right;}
    protected override void OnResize(EventArgs e){base.OnResize(e);input.SetBounds(7,Math.Max(2,(Height-input.PreferredHeight)/2),Math.Max(1,Width-14),input.PreferredHeight);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float radius=Rounded?6:0;var rect=new RectangleF(.5f,.5f,Math.Max(1,Width-1),Math.Max(1,Height-1));using var path=new GraphicsPath();
        if(radius==0)path.AddRectangle(rect);else{float d=radius*2;path.AddArc(rect.Left,rect.Top,d,d,180,90);path.AddArc(rect.Right-d,rect.Top,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.Left,rect.Bottom-d,d,d,90,90);path.CloseFigure();}
        using var fill=new SolidBrush(input.BackColor);using var line=new Pen(input.Focused?Ui.Navy:Color.FromArgb(220,226,233),input.Focused?2:1);e.Graphics.FillPath(fill,path);if(!input.ReadOnly)e.Graphics.DrawPath(line,path);
    }
}

internal sealed class FileIconButton:Button
{
    public string ActionName{get;}
    public FileIconButton(string action,Action click,bool dark)
    {
        ActionName=action;AccessibleName=action;Size=new Size(38,36);Margin=new Padding(2);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=dark?Color.FromArgb(23,59,94):Color.White;ForeColor=dark?Color.White:Ui.Navy;Click+=(_,_)=>click();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using var pen=new Pen(ForeColor,1.3f);int x=(Width-20)/2,y=(Height-20)/2;
        if(ActionName=="Apri")g.DrawLines(pen,new Point[]{new(x,y+18),new(x,y+3),new(x+7,y+3),new(x+10,y+6),new(x+20,y+6),new(x+16,y+18),new(x,y+18),new(x+4,y+8),new(x+20,y+8)});
        else if(ActionName=="Report Word"){g.DrawRectangle(pen,x+2,y,16,20);using var font=new Font("Segoe UI",8,FontStyle.Bold);using var ink=new SolidBrush(ForeColor);g.DrawString("W",font,ink,x+3,y+6);}
        else{g.DrawRectangle(pen,x,y,19,20);g.DrawRectangle(pen,x+4,y,11,7);g.DrawRectangle(pen,x+4,y+12,11,8);if(ActionName=="Salva con nome")g.DrawLine(pen,x+22,y+14,x+22,y+20);}
    }
}
