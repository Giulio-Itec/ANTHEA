"""Prepare attributed article adaptations and reading paths from the cached public sources.

No application files are written here. Publication into the two canonical guides is separate.
Third-party images and image-only equations stay linked to their original source.
"""
import sys as _sys
# W0.5: the external corpus (adapted articles, reading lists, links) was removed on 6/10/2026.
_sys.exit('Script di migrazione superato, non rieseguire: rigenererebbe contenuti esterni eliminati il 6/10/2026.')
from pathlib import Path
from urllib.parse import urljoin, urlsplit, quote
from lxml import html
import json, re, unicodedata, sys

sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[3]
ART=ROOT/'supporto/artefatti/wiki-tecnica-fonti'
OUT=ART/'prepared'; OUT.mkdir(exist_ok=True)

def slug(s):
    s=''.join(c for c in unicodedata.normalize('NFD',s.lower()) if unicodedata.category(c)!='Mn')
    return re.sub('[^a-z0-9]+','-',s).strip('-')

def classify(title,cats=''):
    t=(title+' '+cats).lower()
    if re.search(r'bim|ifc|lod|loin|ids |bep|cad|autocad',t): return ('bim','BIM',[],['bim'])
    if re.search(r'muratur|meccanism[i|o] local|cerchiatur',t): return ('materiali','Fondamenti di ingegneria strutturale',[],['load-path'])
    if re.search(r'legno|ligneo|lignei|x-lam',t): return ('materiali','Fondamenti di ingegneria strutturale',[],['load-path'])
    if re.search(r'fondaz|terren|geotec|palo|pali|micropal|winkler|drenat|liquefaz|masw|hvsr|penetrom|granulometr|edometr|atterberg|pendi|muro|muri|parati|plinto|falda|sifon|piezometr|inclinometr|nailing|rocci|gabbion|rilevat|permeabil|geofis|trince|tirant.*ancor|burland|priebe|cam.clay|geognost|fran|berezantzev|darcy',t):
        mods=[]
        if re.search(r'muro|muri',t): mods=['geo_muri_sostegno']
        elif 'micropal' in t: mods=['geo_micropalo_verticale','geo_micropalo_orizzontale']
        elif re.search(r'palo|pali',t): mods=['geo_palo_verticale','geo_palo_orizzontale','geo_efficienza_orizzontale']
        elif 'winkler' in t: mods=['geo_palo_orizzontale']
        return ('geotecnica','Fondazioni e geotecnica',mods,['bearing-capacity'])
    if re.search(r'dinamic|sism|oscillat|modale|spettr|risonan|steady|time.history|smorz|duttil|gerarchia|p.?delta|massa|vibrant',t): return ('sismica','Dinamica e sisma',[],['dinamica-e-sisma-del-modello'])
    if re.search(r'ponte|ponti|collaborante|compost',t): return ('ponti','Ponti',['str_mista_ponte'],['bridge'])
    if not re.search(r'calcestruzz|cemento|armatur',t) and re.search(r'acciaio|profil[i|at]|bullon|saldat|flesso.torsion|warping|classe 4|carropont|flang',t): return ('acciaio','Acciaio',[],['euler'])
    if re.search(r'calcestruzz|cemento|armatur|coprif|fessur|punzon|taglio|torsion|sezion|aderenza|confin|presso.flession|dominio|mensol',t): return ('calcestruzzo','Calcestruzzo armato',['str_palo'],['sezione-in-calcestruzzo-armato'])
    if re.search(r'acciaio|acciai|bullon|saldat|giunt|instabil|warping|classe 4|carropont|flang',t): return ('acciaio','Acciaio',[],['euler'])
    if re.search(r'bim|ifc|lod|loin|ids |bep|cad|autocad',t): return ('bim','BIM',[],['bim'])
    if re.search(r'fem|matric|mesh|vincol|iperstatic|spostamenti|rigidezz|elementi finit|gusci|shell',t): return ('fem','FEM e modellazione',[],['beam'])
    if re.search(r'excel|python|algorit|comput',t): return ('computational','Computational Design',[],['parametric'])
    return ('fondamenti','Fondamenti di ingegneria strutturale',[],['load-path'])

# Promotional blocks are removed, not silently presented as ANTHEA instructions.
PROMO=re.compile(r'ver\.?\s?sez|easysteel|easypil|easybeam|easydom|easysolve|design.?tools|focus normativ|quickdownload|iscriv|newsletter|scaric|acquista|codice sconto|risorsa consigliata|condividi|condivisione social|lascia un commento|buona lettura|seguimi|mio (?:ebook|e-book|libro|corso)|miei (?:software|app|corsi)|riceverai|email|e-mail|contattami|puoi supportar|caffè virtuale|al prossimo post|^Marco$|earthquake.*guida|ca\.?tel\.?2d|\bwally\b',re.I)
DROP_HEADING=re.compile(r'scaric|risorse|software|applicazion|conclusion|considerazion[i|e] final|risorsa consigliata|iscriv|video|podcast|supporta|comment|link utili|approfondimenti|contenuti correlati|articoli correlati|ti è piaciuto|questi li hai',re.I)

def clean(t): return re.sub(r'\s+',' ',t.replace('\xa0',' ')).strip()
def urlsafe(u): return quote(u,safe=':/?#=&%+;,@~!$*\'-._').replace('(','%28').replace(')','%29')

sources=json.loads((ART/'mdp-selection.json').read_text(encoding='utf-8'))
selected=[x for x in sources if x['selected'] and 'error' not in x and x['index']!=55]
routes={x['url'].rstrip('/'):'mdp-'+str(x['index']) for x in selected}
articles=[]; audit=[]
for item in selected:
    tree=html.fromstring((ROOT/item['cache']).read_bytes())
    main=tree.xpath('//*[contains(concat(" ",normalize-space(@class)," ")," entry-content ")]')[0]
    for e in main.xpath('.//script|.//style|.//form|.//iframe|.//nav|.//noscript'):
        e.drop_tree()
    for e in main.xpath('.//*[contains(@class,"mailpoet") or contains(@class,"extra-hatom")]'):
        if e.getparent() is not None:e.drop_tree()
    removed=[]; figures=[]; lines=[]; skipped=False
    def inline(e):
        out=e.text or ''
        for c in e:
            tag=c.tag.lower() if isinstance(c.tag,str) else ''
            txt=inline(c)
            if tag=='a' and c.get('href') and txt.strip():
                href=urljoin(item['url'],c.get('href')); dest=routes.get(href.split('#')[0].rstrip('/'))
                txt='['+clean(txt).replace('[','(').replace(']',')')+']('+('wiki:'+dest if dest else urlsafe(href))+')'
            elif tag=='br':txt=' '
            elif tag=='sub':txt='_'+txt
            elif tag=='sup':txt={'2':'²','3':'³','-1':'⁻¹'}.get(txt,'^('+txt+')')
            elif tag=='img':txt=''
            out+=txt+(c.tail or '')
        return out
    def visit(e):
        global skipped
        if not isinstance(e.tag,str):return
        tag=e.tag.lower() if isinstance(e.tag,str) else ''
        raw=clean(e.text_content())
        if tag in ('h1','h2','h3','h4','h5','h6'):
            skipped=bool(DROP_HEADING.search(raw) or PROMO.search(raw))
            if skipped:removed.append(raw)
            elif raw:lines.append('### '+raw.replace('#',''))
            return
        if skipped:return
        if tag in ('figure','img'):
            images=[e] if tag=='img' else e.xpath('.//img')
            for img in images:
                src=img.get('data-src') or img.get('src',''); alt=clean(img.get('alt',''))
                if not src or PROMO.search(src+' '+alt) or re.search(r'(?:/|_)\d*[_-]?cop|copertina|logo|banner|avatar',src,re.I):continue
                src=urlsafe(urljoin(item['url'],src));figures.append({'label':alt or 'Figura tecnica della fonte','url':src})
                lines.append('[Figura o formula originale · '+(alt or 'riferimento illustrato').replace('[','(').replace(']',')')+']('+src+')')
            return
        if tag in ('p','li'):
            if not raw:return
            if PROMO.search(raw) or any(re.search(r'marcodepisapia-store|quickdownload|/download|mailpoet|iscriv',a.get('href',''),re.I) for a in e.xpath('.//a[@href]')):removed.append(raw);return
            value=clean(inline(e))
            if value:lines.append(('- ' if tag=='li' else '')+value)
            for img in e.xpath('.//img'):visit(img)
            return
        if tag=='table':
            for row in e.xpath('.//tr'):
                cells=[clean(inline(c)) for c in row.xpath('./th|./td')]
                if cells:lines.append(' · '.join(cells))
            return
        if tag=='blockquote':
            # Quoted third-party passages have independent rights; retain a source pointer.
            if raw:lines.append('[Passaggio citato da consultare nella fonte]('+item['url']+')')
            return
        for c in e:visit(c)
    visit(main)
    body='\n\n'.join(lines).strip()
    # Empty headings after removal are not useful navigation destinations.
    body=re.sub(r'### [^\n]+\n\n(?=### |$)','',body)
    chapter,area,mods,related=classify(item['title'])
    key='mdp-'+str(item['index']);title='Approfondimento · '+item['title']
    if len(body.split())<180:
        body+='\n\n### Risorsa audiovisiva e materiale originale\n\nQuesta voce contiene soprattutto un video o materiale illustrato esterno. La parte testuale disponibile non è una trascrizione del video. Per il procedimento completo consultare la [risorsa originale]('+item['url']+'); utilizzare i collegamenti tematici di questa pagina per la teoria interna alla Wiki.'
    preamble=f'Adattamento testuale da Marco De Pisapia, [{item["title"]}]({item["url"]}). [Licenza CC BY-NC 3.0 Italia](https://creativecommons.org/licenses/by-nc/3.0/it/). Consultazione del 6 ottobre 2026. La riproduzione di questo contributo è consentita alle condizioni della licenza, incluso il vincolo di uso non commerciale; il vincolo riguarda il contributo attribuito.\n\nSono stati rimossi promozioni, inviti al download e blocchi dedicati ai prodotti dell’autore; titoli e collegamenti sono stati riorganizzati. La voce e gli esempi del testo restano dell’autore. Figure, formule disponibili soltanto come immagini e citazioni di terzi rimandano alla fonte: questa è una versione testuale adattata, non una riproduzione integrale illustrata. I riferimenti normativi del testo conservano l’edizione citata e non attestano un controllo automatico in ANTHEA.\n\n'
    credit=f'Marco De Pisapia · [Articolo originale]({item["url"]}) · [CC BY-NC 3.0 Italia](https://creativecommons.org/licenses/by-nc/3.0/it/). Adattamento testuale per uso non commerciale. Figure e formule pubblicate come immagini rimandano alla fonte.\n\n'
    note='### Fonte e condizioni dell’adattamento\n\n'+preamble
    entry=dict(key=key,title=title,chapterId=chapter,area=area,modules=mods,related=related,sourceUrl=item['url'],license='CC BY-NC 3.0 IT',status='qualified',body='## '+title+'\n\n'+credit+body+'\n\n'+note+'\n')
    articles.append(entry)
    audit.append(dict(index=item['index'],url=item['url'],sourceWords=item['words'],adaptedWords=len(body.split()),removedBlocks=len(removed),linkedFigures=len(figures),removed=removed))
(OUT/'mdp-articles.json').write_text(json.dumps(articles,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'mdp-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
print('Adattamenti',len(articles),'parole',sum(x['adaptedWords'] for x in audit),'figure collegate',sum(x['linkedFigures'] for x in audit))

# GeoStru general technical entries; product tutorials, news and marketing excluded.
GEO={5,8,19,20,23,25,26,31,33,35,36,40,41,44,45,47,49,50,51,53,55,56,57,58,61,63,64,67,68,70,71,75,76,79,80,81,82,83,84,85,91,92,93,97,98,99,100,101,102,103,107,108,109,110,111,112,115,116,119,120,122,123,124,126,128,131,132,133,134,139,140,141,144,145,146,151,152,156,157,159,160}
geo=json.loads((ART/'geostru-catalog.json').read_text(encoding='utf-8'))
caffe=json.loads((ART/'caffe-catalog.json').read_text(encoding='utf-8'))
resources=[]
for source,rows in [('GeoStru',[(i,x) for i,x in enumerate(geo) if i in GEO]),('Simone Caffè',list(enumerate(caffe)))]:
    for i,x in rows:
        # Software-specific demonstrations and out-of-domain environmental physics are excluded.
        if source=='Simone Caffè' and i in {22,38,47,77,86,87}:continue
        title=x['title'].replace('CapacitaÌ€','Capacità').replace('_',' ')
        chapter,area,mods,related=classify(title)
        if source=='Simone Caffè':
            if i<=3:chapter,area,mods,related='ponti','Ponti',['str_mista_ponte'],['sezione-composta-da-ponte']
            elif i<=22:chapter,area='geotecnica','Fondazioni e geotecnica'
            elif i<=35:chapter,area,mods,related='calcestruzzo','Calcestruzzo armato',['str_palo'],['sezione-in-calcestruzzo-armato']
            elif i==36:chapter,area,mods,related='materiali','Fondamenti di ingegneria strutturale',[],['load-path']
            elif i<=56:chapter,area,mods,related='sismica','Dinamica e sisma',[],['dinamica-e-sisma-del-modello']
            elif i<=82:chapter,area,mods,related='acciaio','Acciaio',[],['euler']
            elif i==83:chapter,area,mods,related='materiali','Fondamenti di ingegneria strutturale',[],['load-path']
            else:chapter,area,mods,related='meccanica','Fondamenti di ingegneria strutturale',[],['beam']
        resources.append(dict(source=source,index=i,title=title,url=urlsafe(x['url']),chapterId=chapter,area=area,modules=mods,related=related))
(OUT/'reading-resources.json').write_text(json.dumps(resources,ensure_ascii=False,indent=2),encoding='utf-8')
print('Letture esterne selezionate',len(resources))
