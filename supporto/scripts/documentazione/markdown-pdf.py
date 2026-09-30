"""Export a text/table Markdown guide to a searchable PDF using bundled Python.

Usage: python markdown-pdf.py source.md [output.pdf]
Preserves source text; supports headings, paragraphs, lists, fenced code and tables.
"""
from pathlib import Path
import re
import sys
from html import escape
from reportlab.pdfgen.canvas import Canvas
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, LongTable, TableStyle, Image, KeepTogether, PageBreak
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

FONTS = Path('C:/Windows/Fonts')
for name, file in [('Guide','arial.ttf'),('GuideBold','arialbd.ttf'),('GuideItalic','ariali.ttf'),('GuideMono','consola.ttf')]:
    pdfmetrics.registerFont(TTFont(name, str(FONTS / file)))
pdfmetrics.registerFontFamily('Guide',normal='Guide',bold='GuideBold',italic='GuideItalic',boldItalic='GuideBold')

styles = getSampleStyleSheet()
styles.add(ParagraphStyle(name='BodyGuide',fontName='Guide',fontSize=10.3,leading=14.4,spaceAfter=7))
styles.add(ParagraphStyle(name='TitleGuide',fontName='GuideBold',fontSize=20,leading=25,spaceAfter=17,keepWithNext=True))
styles.add(ParagraphStyle(name='H2Guide',fontName='GuideBold',fontSize=14,leading=18,spaceBefore=14,spaceAfter=7,keepWithNext=True))
styles.add(ParagraphStyle(name='H3Guide',fontName='GuideBold',fontSize=11.5,leading=15.5,spaceBefore=9,spaceAfter=6,keepWithNext=True))
styles.add(ParagraphStyle(name='CellGuide',fontName='Guide',fontSize=9,leading=12))
styles.add(ParagraphStyle(name='CodeGuide',fontName='GuideMono',fontSize=8.5,leading=11,spaceAfter=5,wordWrap='CJK'))

def inline(text):
    text = text.replace('\u2011','-').replace('\u2013','-').replace('\u2014','-')
    text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', lambda m: m.group(1)+' ('+m.group(2)+')', text)
    text = escape(text)
    # Arial does not contain all Unicode subscript digits: use its ordinary glyphs.
    for digit, subscript in enumerate('₀₁₂₃₄₅₆₇₈₉'):
        text = text.replace(subscript, f'<sub>{digit}</sub>')
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'`([^`]+)`',r'<font name="GuideMono">\1</font>',text)
    return text

class NumberedCanvas(Canvas):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs); self.saved=[]
    def showPage(self):
        self.saved.append(dict(self.__dict__)); self._startPage()
    def save(self):
        total=len(self.saved)
        for state in self.saved:
            self.__dict__.update(state)
            self.setFont('Guide',8); self.setFillColor(colors.HexColor('#526174'))
            self.drawRightString(A4[0]-43,25,f'{self._pageNumber} / {total}')
            super().showPage()
        super().save()

def build(source, output=None):
    source=Path(source).resolve(); output=Path(output).resolve() if output else source.with_suffix('.pdf')
    lines=source.read_text(encoding='utf-8-sig').splitlines(); story=[]; i=0
    width=A4[0]-86
    while i<len(lines):
        line=lines[i].strip(); i+=1
        if not line: continue
        if line == '<!-- pagebreak -->':
            story.append(PageBreak()); continue
        picture = re.fullmatch(r'!\[([^\]]*)\]\(([^)]+)\)', line)
        if picture:
            path = (source.parent / picture[2]).resolve()
            graphic = Image(str(path)); graphic._restrictSize(width, 305)
            story.append(KeepTogether([graphic, Spacer(1,5), Paragraph(inline(picture[1]),styles['CellGuide']), Spacer(1,8)])); continue
        if line.startswith('```'):
            while i<len(lines) and not lines[i].strip().startswith('```'):
                story.append(Paragraph(escape(lines[i]).replace(' ','&#160;'),styles['CodeGuide'])); i+=1
            i+=1; continue
        if line.startswith('|'):
            block=[line]
            while i<len(lines) and lines[i].strip().startswith('|'):
                block.append(lines[i].strip()); i+=1
            rows=[[x.strip() for x in row.strip('|').split('|')] for row in block if not re.fullmatch(r'[\s|:\-]+',row)]
            cols=len(rows[0]); measures=[]
            for col in range(cols):
                lengths=sorted(len(re.sub(r'[*`]', '', row[col])) for row in rows)
                measures.append(max(10,min(60,lengths[-1]*.6+len(rows[0][col])*.4)))
            widths=[width*x/sum(measures) for x in measures]
            data=[[Paragraph(('<b>'+inline(cell)+'</b>') if r==0 else inline(cell),styles['CellGuide']) for cell in row] for r,row in enumerate(rows)]
            table=LongTable(data,colWidths=widths,repeatRows=1,hAlign='LEFT')
            table.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#dce7ee')),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#f5f7f8')]),('GRID',(0,0),(-1,-1),.4,colors.HexColor('#d9d9d9')),('VALIGN',(0,0),(-1,-1),'MIDDLE'),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7)]))
            story.extend([table,Spacer(1,9)]); continue
        heading=re.match(r'^(#{1,6})\s+(.*)',line)
        if heading:
            level=len(heading[1]); story.append(Paragraph(inline(heading[2]),styles['TitleGuide' if level==1 else 'H2Guide' if level==2 else 'H3Guide'])); continue
        if line.startswith('- '):
            story.append(Paragraph(inline(line[2:]),styles['BodyGuide'],bulletText='\u2022')); continue
        paragraph=[line]
        while i<len(lines) and lines[i].strip() and not re.match(r'^(#|\||- |```|!\[|<!--)',lines[i].strip()):
            paragraph.append(lines[i].strip()); i+=1
        story.append(Paragraph(inline(' '.join(paragraph)),styles['BodyGuide']))
    output.parent.mkdir(parents=True,exist_ok=True)
    SimpleDocTemplate(str(output),pagesize=A4,rightMargin=43,leftMargin=43,topMargin=42,bottomMargin=43,title=next((x.lstrip('# ') for x in lines if x.startswith('# ')),source.stem),author='ANTHEA').build(story,canvasmaker=NumberedCanvas)
    print(output)
    return output

if __name__=='__main__': build(*sys.argv[1:])
