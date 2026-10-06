"""Read public articles into a provenance cache; no application documents are changed."""
import sys as _sys
# W0.5: the external corpus (adapted articles, reading lists, links) was removed on 6/10/2026.
_sys.exit('Script di migrazione superato, non rieseguire: rigenererebbe contenuti esterni eliminati il 6/10/2026.')
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.parse import urljoin, urlsplit
from concurrent.futures import ThreadPoolExecutor, as_completed
from lxml import html
import json, hashlib, sys, time

sys.stdout.reconfigure(encoding='utf-8')
ROOT = Path(__file__).resolve().parents[3]
ART = ROOT/'supporto/artefatti/wiki-tecnica-fonti'
ART.mkdir(parents=True, exist_ok=True)
def fetch(url, folder='pages'):
    target=ART/folder/(hashlib.sha256(url.encode()).hexdigest()+'.html')
    target.parent.mkdir(parents=True,exist_ok=True)
    if not target.exists():
        data=urlopen(Request(url,headers={'User-Agent':'Mozilla/5.0 (ANTHEA documentation research)'}),timeout=45).read()
        target.write_bytes(data)
        time.sleep(.2)
    return target,html.fromstring(target.read_bytes(),base_url=url)

catalog=json.loads((ART/'mdp-catalog.json').read_text(encoding='utf-8'))
# Reviewed against the public sitemap: exclude app landing pages, support, accounts,
# galleries of app models, promotional announcements and podcasts without a written lesson.
selected={5,7,8,9,12,15,16,17,18,19,20,23,24,25,26,30,32,34,35,36,37,38,39,40,41,42,44,
51,52,54,55,56,57,58,60,61,62,63,64,65,66,67,69,70,71,73,74,77,80,81,83,84,85,86,87,88,89,90,
95,96,97,98,100,101,104,105,106,107,108,109,113,115,116,118,119,122,124,125,127,128,130,131,132,133,
136,137,139,140,141,142,145,146,149,150,151,152,154,155,156,159,160,162,163,164,165,166,168,169,170,
171,172,176,177,180,181,182,183,186,187,188,189,192,193,194,195,196,198,199,200,201,202,203,204,205,
206,207,208,209,213,214,215,216,217,220,222,223,224,225,226,227,228,229,230,231,232,234,235,236,237,
239,241,242,243,244,245,246,247,248,255,260,261,264,265,266,267,268,269,270,272,275}
def collect(pair):
    i,item=pair
    item=dict(item,index=i,selected=i in selected)
    if i not in selected:
        item['reason']='Pagina di servizio, promozione, tutorial del software dell’autore, galleria o contenuto non tecnico scritto'
        return item
    try:
        path,tree=fetch(item['url'],'mdp')
        main=tree.xpath('//*[contains(concat(" ",normalize-space(@class)," ")," entry-content ")]')
        if not main:raise ValueError('Testo articolo assente')
        item['cache']=str(path.relative_to(ROOT))
        item['sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
        item['license']=next((a.get('href') for a in tree.xpath('//a[@href]') if 'creativecommons.org/licenses/by-nc/3.0/it' in a.get('href','')),None)
        item['categories']=[a.text_content().strip() for a in tree.xpath('//a[@rel="category tag"]')]
        item['words']=len(main[0].text_content().split())
        item['headings']=[e.text_content().strip() for e in main[0].xpath('.//h2|.//h3|.//h4')]
        item['image_count']=len(main[0].xpath('.//img'))
    except Exception as exc:item['error']=str(exc)
    return item
results=[]
with ThreadPoolExecutor(max_workers=3) as pool:
    for n,future in enumerate(as_completed([pool.submit(collect,p) for p in enumerate(catalog)]),1):
        results.append(future.result())
        if n%30==0:print('Raccolta',n,'/',len(catalog),flush=True)
results.sort(key=lambda x:x['index'])
(ART/'mdp-selection.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print('Selezionati',sum(x['selected'] for x in results),'errori',sum('error' in x for x in results))

# GeoStru: catalog only. No open reproduction license has been established.
geo=[];url='https://blog.geostru.eu/articoli/';seen=set()
for page in range(1,80):
    if url in seen:break
    seen.add(url)
    path,tree=fetch(url,'geostru-index')
    rows=tree.xpath('//article')
    for row in rows:
        links=row.xpath('.//h2/a[@href]|.//h3/a[@href]')
        if not links:continue
        a=links[0];geo.append(dict(title=a.text_content().strip(),url=urljoin(url,a.get('href')),categories=row.get('class',''),sourcePage=url))
    nexts=tree.xpath('//a[contains(concat(" ",normalize-space(@class)," ")," nav-next ") or contains(concat(" ",normalize-space(@class)," ")," next ")]/@href')
    if not nexts:break
    url=urljoin(url,nexts[-1])
    if urlsplit(url).hostname!='blog.geostru.eu':raise ValueError('Unexpected pagination host')
    if page%5==0:print('Indice GeoStru',page,flush=True)
unique={x['url']:x for x in geo}
(ART/'geostru-catalog.json').write_text(json.dumps(list(unique.values()),ensure_ascii=False,indent=2),encoding='utf-8')
print('Catalogo GeoStru',len(unique))

caffe=[]
for url in ['https://www.simonecaffe.it/index.php/ingegneria/documenti','https://www.simonecaffe.it/index.php/didattica/dispense']:
    path,tree=fetch(url,'caffe-index')
    for a in tree.xpath('//a[@href]'):
        href=urljoin(url,a.get('href'));title=a.text_content().strip()
        if title and any(k in href.lower() for k in ('.pdf','/download/','/documenti/')):
            caffe.append(dict(title=title,url=href,sourcePage=url))
(ART/'caffe-catalog.json').write_text(json.dumps(list({x['url']:x for x in caffe}.values()),ensure_ascii=False,indent=2),encoding='utf-8')
print('Catalogo Caffè',len(caffe))
