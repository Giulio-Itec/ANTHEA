"""Consolidate existing documentation without dropping source content."""
from pathlib import Path
import re, shutil, json, hashlib, importlib.util, os

ROOT = Path(__file__).resolve().parents[3]
S = ROOT / 'supporto'
if 'revisione documentale 08' in (S/'docs/guida-pratica-anthea.md').read_text(encoding='utf-8-sig'):
    raise SystemExit('Unificazione Rev08 già eseguita. Usare Build-AntheaGuides-Itec.py per rigenerare i Word.')
ARCHIVE = S / 'SUPERATI/guide-unificate-rev08-20261002'
ART = S / 'artefatti/guide_anthea_itec_rev08'
ART.mkdir(parents=True, exist_ok=True)
registry = []

def archive(p, replacement, move=False):
    dest = ARCHIVE / p.relative_to(S)
    assert p.resolve().is_relative_to(S.resolve()) and dest.resolve().is_relative_to(ARCHIVE.resolve())
    dest.parent.mkdir(parents=True, exist_ok=True)
    if not dest.exists():
        shutil.copy2(p, dest)
        registry.append(dict(origine=str(p.relative_to(ROOT)), archivio=str(dest.relative_to(ROOT)),
            motivo='Contenuti incorporati nelle guide globali Rev08', sostituzione=replacement,
            sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    if move:
        p.unlink()

practical = {'calcestruzzo-interfaccia', 'gerarchia-progetti', 'progetti-revisioni',
    'stabilita-globale-guida-rapida', 'micropalo-orizzontale'}
sources = {'pratica': [], 'teorica': []}
for p in sorted((S / 'docs').glob('*.md')):
    if p.stem.startswith('guida-'):
        continue
    kind = 'pratica' if p.stem in practical else 'teorica'
    sources[kind].append(p)
for kind, label in [('pratica','Pratica'), ('teorica','Teoria')]:
    sources[kind].append(S / f'documentazione/Bridge_Design/ANTHEA_Bridge_Design_{label}_ITEC_Rev01.md')

mapping = {}
for kind in sources:
    target = S / f'docs/guida-{kind}-anthea.md'
    text = target.read_text(encoding='utf-8-sig')
    for p in [target, target.with_suffix('.pdf')]:
        if p.exists(): archive(p, str(target.relative_to(ROOT)))
    text = re.sub(r'Edizione 2 del .*?revisione documentale 07',
        'Edizione 3 del 2 ottobre 2026 — revisione documentale 08', text, count=1)
    notice = ('\n\nQuesta edizione unifica la documentazione di ANTHEA in due volumi globali. '
        'Il volume pratico comprende uso, interfaccia e procedure; quello teorico comprende '
        'modelli, formule, ipotesi, limiti e approfondimenti di tutti i moduli. '
        'I capitoli di approfondimento conservano integralmente i contenuti delle precedenti schede. '
        'Audit, migrazioni e studi conservano la loro data e il loro ambito storico: '
        'non descrivono automaticamente lo stato attuale del programma.\n')
    pos = text.index('\n', text.index('Edizione 3'))
    text = text[:pos] + notice + text[pos:]
    text += '\n\n## Approfondimenti integrati\n\n'
    for n, p in enumerate(sources[kind], 1):
        body = p.read_text(encoding='utf-8-sig')
        title = next((line[2:] for line in body.splitlines() if line.startswith('# ')), p.stem)
        text += f'- {kind.upper()} A{n:02}: {title}\n'
    for n, p in enumerate(sources[kind], 1):
        body = p.read_text(encoding='utf-8-sig')
        body = re.sub(r'!\[([^\]]*)\]\(([^)]+)\)', lambda m: '!['+m[1]+']('+os.path.relpath((p.parent/m[2]).resolve(),target.parent).replace('\\','/')+')' if '://' not in m[2] else m[0], body)
        body = re.sub(r'^(#{1,6}) ', lambda m: '#' * min(6,len(m[1])+1)+' ', body, flags=re.M)
        title_match = re.search(r'^## (.*)', body, re.M)
        if title_match:
            body = body[:title_match.start()] + re.sub(r'^## ', f'## {kind.upper()} A{n:02} — ', body[title_match.start():],count=1)
        text += '\n\n' + body
        mapping[p.resolve()] = target
    target.write_text(text, encoding='utf-8')

# Retarget existing Markdown links, including the links inside merged chapters.
for p in ROOT.rglob('*.md'):
    if any(part in {'SUPERATI','tmp','bin','obj','.git','node_modules','artefatti'} for part in p.parts): continue
    text = p.read_text(encoding='utf-8-sig')
    def link(m):
        if m[2].startswith(('http:', 'https:', '#')): return m[0]
        raw = m[2].split('#')[0]
        old = (p.parent / raw).resolve()
        lookup = old.with_suffix('.md') if old.suffix in {'.pdf','.docx'} else old
        if lookup not in mapping: return m[0]
        new = mapping[lookup]
        if old.suffix == '.pdf': new = new.with_suffix('.pdf')
        if old.suffix == '.docx':
            kind = 'pratica' if 'pratica' in new.name else 'teorica'
            new = S / f'documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev08.docx'
        return '['+m[1]+']('+os.path.relpath(new,p.parent).replace('\\','/')+')'
    changed = re.sub(r'(?<!!)\[([^\]]+)\]\(([^)]+)\)', link, text)
    if changed != text: p.write_text(changed,encoding='utf-8')

for kind in sources:
    replacement = f'supporto/docs/guida-{kind}-anthea.md'
    for p in sources[kind]:
        for ext in ['.md','.pdf','.docx']:
            sibling = p.with_suffix(ext)
            if sibling.exists(): archive(sibling,replacement,move=True)
for p in (S/'documentazione/Guide_ANTHEA').glob('*Rev07.*'):
    archive(p, str(p.relative_to(ROOT)).replace('Rev07','Rev08'), move=True)

builder = S/'scripts/Build-AntheaGuides-Itec.py'
text = builder.read_text(encoding='utf-8-sig').replace("REVISION = '07'", "REVISION = '08'").replace('30/09/2026','02/10/2026').replace('30 settembre 2026','2 ottobre 2026').replace('2026-09-30','2026-10-02').replace('MURI SISMA SLE E ARMATURE','GUIDE GLOBALI UNIFICATE')
text = text.replace('}[count]', '}.get(count, [17 / count] * count)')
builder.write_text(text,encoding='utf-8')
(ART/'artifact.md').write_text('Guide globali Rev08: consolidamento integrale dei contenuti, modello ITEC esistente.\n',encoding='utf-8')
(ARCHIVE/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
(ARCHIVE/'README.md').write_text('# Guide unificate — revisione 08\n\nOrigine: supporto/docs e supporto/documentazione.\n\nLe guide autonome e le revisioni 07 sono conservate nella struttura relativa originale. Tutti i contenuti sono incorporati nelle due guide globali Rev08 del 2 ottobre 2026. Il registro JSON documenta origine, motivo, sostituzione e SHA-256 di ogni file.\n',encoding='utf-8')
spec = importlib.util.spec_from_file_location('builder',builder)
B = importlib.util.module_from_spec(spec); spec.loader.exec_module(B)
for kind in B.GUIDES: B.build(kind)
print(f'Integrati {sum(map(len,sources.values()))} sorgenti nelle due guide globali.')
