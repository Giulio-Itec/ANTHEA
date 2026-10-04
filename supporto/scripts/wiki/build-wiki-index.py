"""Rebuild the derived WPF Wiki metadata and search postings from the two global manuals.
Run with bundled Python after editorial changes. --check verifies that the committed index is current.
Bodies remain in the canonical guides; byte offsets permit lazy chapter loading.
"""
from pathlib import Path
import re, json, unicodedata, sys, hashlib, shutil
from validate_wiki import validate
ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'X.Desktop/Wiki'
def normalize(s):
    s=s.lower()
    for symbol,name in {'σ':'sigma','τ':'tau','π':'pi','λ':'lambda','γ':'gamma','κ':'kappa','θ':'theta','δ':'delta','ρ':'rho'}.items(): s=s.replace(symbol,name)
    return ''.join(c for c in unicodedata.normalize('NFD', s.lower()) if unicodedata.category(c) != 'Mn')
def slug(s): return re.sub('[^a-z0-9]+', '-', normalize(s)).strip('-')
AREAS = ['Fondamenti di ingegneria strutturale','Azioni e combinazioni','Calcestruzzo armato','Acciaio','Ponti','FEM e modellazione','Fondazioni e geotecnica','Dinamica e sisma','Metodi costruttivi','Normativa e riferimenti','BIM','Computational Design']
GUIDES = ['Introduzione','Primi passi','Progetti','Moduli singoli','Interpretazione risultati','Esportazione','Workflow','Tutorial','FAQ e troubleshooting']
articles=[]; postings={}; assets={}; hashes={}; bodies={}
editorial=json.loads((OUT/'editorial.json').read_text(encoding='utf-8'))
chapters=json.loads((OUT/'chapters.json').read_text(encoding='utf-8'))
references=json.loads((OUT/'references.json').read_text(encoding='utf-8'))
redirects=json.loads((OUT/'aliases.json').read_text(encoding='utf-8'))
glossary=json.loads((OUT/'glossary.json').read_text(encoding='utf-8'))
for kind, source in [('guide','pratica'),('theory','teorica')]:
    data=(ROOT/f'supporto/docs/guida-{source}-anthea.md').read_bytes()
    hashes[source]=hashlib.sha256(data).hexdigest().upper()
    for path in re.findall(r'^!\[[^\]]*\]\(([^)]+)\)',data.decode('utf-8'),re.M):
        original=(ROOT/'supporto/docs'/path).resolve()
        assert original.is_file(),f'Figura mancante: {original}'
        name=original.name if original.parent==ROOT/'X.Desktop/Assets/Wiki' else slug(original.stem)+'-'+hashlib.sha256(path.encode()).hexdigest()[:8]+original.suffix
        assets[path]=name
        if '--check' not in sys.argv and original!=ROOT/'X.Desktop/Assets/Wiki'/name: shutil.copy2(original,ROOT/'X.Desktop/Assets/Wiki'/name)
    matches=list(re.finditer(rb'^## ([^\r\n]+)',data,re.M))
    for i, match in enumerate(matches):
        title=match[1].decode('utf-8'); start=match.start(); end=matches[i+1].start() if i+1<len(matches) else len(data)
        body=data[start:end].decode('utf-8')
        key=kind+':'+title
        assert key in editorial, f'Capitolo senza revisione editoriale: {key}'
        meta=editorial[key]
        area=meta['area']
        clean=re.sub(r'^\d+\s+|^(?:PRATICA|TEORICA|TEORIA) A\d+\s*[—-]\s*','',title)
        route=f'/wiki/{"guide" if kind=="guide" else "manuale"}/{slug(area)}/{slug(clean)}'
        route=meta.get('id',route)
        assert not any(a['id']==route for a in articles),f'Duplicate article slug: {route}'
        bodies[route]=body
        prose=[l.strip() for l in body.splitlines()[1:] if l.strip() and not l.startswith(('#','|','>','!','```','<!--','- '))]
        summary=re.sub(r'\*\*|`','',prose[0]) if prose else f'{clean}: metodi, procedure e limiti del modello.'
        if len(summary)>220: summary=summary[:220].rsplit(' ',1)[0]+'…'
        summary=meta.get('summary',summary)
        words=set(re.findall('[a-z0-9]+',normalize(body+' '+area)))
        aliases=[]
        for key, values in [('instabilita',['buckling','euler','snellezza']),('beam',['trave','finite','element','FEM']),('calcestruzzo',['concrete','rc']),('taglio',['shear']),('progett',['project']),('fessur',['cracking'])]:
            if key in normalize(body): aliases+=values
        aliases+=meta.get('keywords',[])
        words.update(re.findall('[a-z0-9]+',normalize(' '.join(aliases))))
        for word in words:
            if len(word)>1: postings.setdefault(word,[]).append(route)
        sections=[]
        for s in re.findall(r'^#{3,4} (.+)',body,re.M):
            anchor=slug(s); unique=anchor; n=2
            while unique in sections:unique=anchor+'-'+str(n);n+=1
            sections.append(unique)
        entry=dict(id=route,type=kind,area=area,title=clean,summary=summary,source=source+'.md',offset=start,length=end-start,order=len(articles),readingTime=max(1,round(len(body.split())/180)),keywords=aliases,related=[],modules=meta["modules"],example=None,sections=sections)
        entry.update(meta); articles.append(entry)
beam=next(a for a in articles if a['id']=='/wiki/manuale/fem/elementi-beam')
guide=next(a for a in articles if a['id']=='/wiki/guide/moduli/sezione-ca')
articles.remove(guide); articles.insert(0,guide) # preferred contextual guide, rather than an archived audit
by_id={a['id']:a for a in articles}
by_key={a['key']:a for a in articles}
for a in articles:
    assert len(a['related']) == len(set(a['related'])), f'Correlati duplicati: {a["title"]}'
    for uri in a['related']:
        assert (uri in by_id or uri in by_key) and uri not in [a['id'],a['key']], f'Correlato non valido: {uri}'
    assert a['area'] in AREAS+GUIDES, f'Categoria sconosciuta: {a["area"]}'
validate(articles,chapters,references,glossary,bodies,ROOT,redirects)
for term,entry in glossary.items():
    target=entry[1].split('#')[0];target=redirects.get(target,target).split('#')[0];a=by_id.get(target) or by_key.get(target)
    for word in set(re.findall('[a-z0-9]+',normalize(term+' '+entry[0]+' '+' '.join(entry[2:])))):
        if len(word)>1:postings.setdefault(word,[]).append(a['id'])
OUT.mkdir(exist_ok=True)
positions={a['id']:i for i,a in enumerate(articles)}
postings={word:sorted({positions[uri] for uri in ids}) for word,ids in postings.items()}
for name,obj in [('index.json',articles),('search.json',postings),('areas.json',dict(theory=AREAS,guide=GUIDES)),('assets.json',assets)]:
    text=json.dumps(obj,ensure_ascii=False,separators=(',',':'),sort_keys=True)+'\n'; path=OUT/name
    if '--check' in sys.argv: assert path.read_text(encoding='utf-8')==text,f'Indice obsoleto: {path}'
    else: path.write_text(text,encoding='utf-8')
tracked=['editorial.json','chapters.json','references.json','glossary.json','aliases.json','index.json','search.json','assets.json','areas.json']
tracked+=['../Assets/Wiki/'+name for name in sorted(set(assets.values()))]
props='<Project><ItemGroup>'+''.join(f'<WikiSource Include="$(MSBuildThisFileDirectory)../../supporto/docs/guida-{kind}-anthea.md"><ExpectedHash>{value}</ExpectedHash></WikiSource>' for kind,value in hashes.items())+''.join(f'<WikiSource Include="$(MSBuildThisFileDirectory){name}"><ExpectedHash>{hashlib.sha256((OUT/name).read_bytes()).hexdigest().upper()}</ExpectedHash></WikiSource>' for name in tracked)+'</ItemGroup></Project>\n'
if '--check' in sys.argv: assert (OUT/'sources.props').read_text(encoding='utf-8')==props,'Hash sorgenti obsoleti'
else: (OUT/'sources.props').write_text(props,encoding='utf-8')
print(f'{len(articles)} capitoli, {len(postings)} termini indicizzati')
