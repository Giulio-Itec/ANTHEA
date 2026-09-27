from pathlib import Path
import json
import pypdfium2 as pdfium

A=Path('supporto/artefatti/validazione_illustrata_2026_09_26')
d=pdfium.PdfDocument(A/'final.pdf')
issues=[]
for pi,page in enumerate(d):
    t=page.get_textpage();text=t.get_text_range();bad=[]
    for i in range(t.count_chars()):
        ch=t.get_text_range(i,1)
        if not ch.strip():continue
        x0,y0,x1,y1=t.get_charbox(i)
        if 75<y0<page.get_height()-80 and (x1>page.get_width()-50 or x0<40):
            bad.append(dict(char=ch,box=[round(v,1) for v in (x0,y0,x1,y1)],context=t.get_text_range(max(0,i-60),min(130,t.count_chars()-max(0,i-60)))))
    if bad:issues.append(dict(page=pi+1,overflow=bad))
    if any(e in text for e in ['Errore. Il segnalibro','Error! Reference','Error! Bookmark','Errore. L’origine']):raise ValueError((pi+1,'broken field'))
    t.close();page.close()
(A/'overflow.json').write_text(json.dumps(issues,ensure_ascii=False,indent=2),encoding='utf8')
print('Pages',len(d),'overflow candidates',[(x['page'],len(x['overflow'])) for x in issues])
