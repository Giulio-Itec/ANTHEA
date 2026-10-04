"""Print selected source articles without shell quoting of their contents."""
from pathlib import Path
import json,sys,re
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[3]
articles=json.loads((ROOT/'X.Desktop/Wiki/index.json').read_text(encoding='utf-8'))
for a in articles:
    selected=[x for x in sys.argv[1:] if x.split('#')[0] in (a['key'],a['id'])]
    if selected:
        data=(ROOT/'supporto/docs'/('guida-'+a['source'].replace('.md','')+'-anthea.md')).read_bytes()
        body=data[a['offset']:a['offset']+a['length']].decode('utf-8').replace('\r','')
        filters=[x.split('#',1)[1] for x in selected if '#' in x]
        if filters:
            blocks=re.split(r'(?=^### )',body,flags=re.M)
            body='\n'.join(b for b in blocks if any(f.casefold() in b.split('\n')[0].casefold() for f in filters))
        print(body)
