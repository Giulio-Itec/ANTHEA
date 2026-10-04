"""Structural integrity gate, shared by the index builder and negative regression tests."""
import re
from pathlib import Path
from urllib.parse import urlsplit
import xml.etree.ElementTree as ET

def validate(articles, chapters, references, glossary, bodies, root, aliases=None):
    aliases=aliases or {}
    errors=[]
    def require(ok,message):
        if not ok:errors.append(message)
    def unique(rows,field,label):
        values=[r.get(field,'') for r in rows]
        require(all(values),f'{label}: empty {field}')
        require(len(values)==len(set(values)),f'{label}: duplicate {field}')
    for field in ['id','key']:unique(articles,field,'article')
    unique(chapters,'id','chapter');unique(references,'id','reference')
    by_id={a['id']:a for a in articles};by_key={a['key']:a for a in articles}
    chapter_ids={c['id'] for c in chapters};ref_ids={r['id'] for r in references}
    def canonical(uri):
        uri=uri.removeprefix('wiki:')
        if uri in aliases:return aliases[uri]
        path,sep,anchor=uri.partition('#')
        return aliases.get(path,path)+(sep+anchor if sep else '')
    def resolve(uri):
        uri=canonical(uri).split('#')[0]
        return by_id.get(uri) or by_key.get(uri)
    def link(uri,owner):
        path,_,anchor=canonical(uri).partition('#');a=resolve(path)
        require(a is not None,f'{owner}: broken internal link {uri}')
        if a and anchor:require(anchor in a['sections'],f'{owner}: unknown anchor {uri}')
    for r in references:
        uri=urlsplit(r.get('url',''));require(uri.scheme=='https' and bool(uri.netloc) and bool(r.get('title')) and bool(r.get('kind')),f'invalid reference {r["id"]}')
    for old,new in aliases.items():
        require(new not in aliases,'alias chain or cycle: '+old)
        require(old!=new,'self alias: '+old)
        link(new,'alias '+old)
    for a in articles:
        owner=a['id'];body=bodies[owner]
        require(bool(a.get('title','').strip()),owner+': article without title')
        require(bool(a.get('summary','').strip()),owner+': article without description')
        require(a.get('chapterId') in chapter_ids,owner+': unknown chapterId')
        require(bool(a.get('area')),owner+': unknown categoryId')
        require(len(a['sections'])==len(set(a['sections'])),owner+': duplicate section slug')
        require(a.get('level') in ['introductory','intermediate','advanced'],owner+': invalid level')
        for field in ['related','prerequisites']:
            for uri in a.get(field,[]):
                link(uri,owner)
                require(resolve(uri.split('#')[0])!=a,owner+': self reference')
        for r in a.get('references',[]):require(r in ref_ids,owner+': unknown reference '+r)
        for uri in re.findall(r'\]\(((?:/wiki/|wiki:)[^)]+)\)',body):link(uri,owner)
        for alt,path in re.findall(r'^!\[([^\]]*)\]\(([^)]+)\)',body,re.M):
            require(bool(alt.strip()),owner+': missing alt text')
            asset=(root/'supporto/docs'/path).resolve();require(asset.is_file(),owner+': missing asset '+path)
            if asset.is_file() and asset.suffix=='.svg':
                svg=ET.parse(asset).getroot();require(bool(svg.get('viewBox')),owner+': missing viewBox')
                require(any(x.tag.endswith('title') and x.text for x in svg),owner+': missing SVG title')
                require(all(x.tag.split('}')[-1] in ['svg','title','line','polyline','polygon','circle','rect','text'] for x in svg.iter()),owner+': unsupported SVG primitive')
                require(asset.with_suffix('.png').is_file(),owner+': missing document figure rendition')
        require(not re.search(r'Lorem ipsum|TODO: aggiungere|Inserire grafico qui',body,re.I),owner+': placeholder')
    for term,data in glossary.items():
        require(len(data)>=2 and bool(data[0]),'invalid glossary '+term)
        if len(data)>=2:link(data[1],'glossary '+term)
    for c in chapters:require(any(a['chapterId']==c['id'] and a['status']!='historical' for a in articles),'orphan chapter '+c['id'])
    if errors:raise ValueError('\n'.join(errors))
    return dict(articles=len(articles),chapters=len(chapters),references=len(references),glossary=len(glossary))
