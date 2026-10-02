from pathlib import Path
import importlib.util, re, json, shutil, hashlib
ROOT=Path(__file__).resolve().parents[3]; S=ROOT/'supporto'
ARCH=S/'SUPERATI/guide-unificate-rev08-20261002'
registry=json.loads((ARCH/'registro.json').read_text(encoding='utf-8'))
def save_previous(p):
    if not p.exists(): return
    dest=ARCH/p.relative_to(S); dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():
        shutil.copy2(p,dest)
        registry.append(dict(origine=str(p.relative_to(ROOT)), archivio=str(dest.relative_to(ROOT)),motivo='Indice aggiornato alle due guide globali Rev08',sostituzione=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
for p in [S/'README.md',S/'README.pdf',S/'installer/README.md',S/'installer/README.pdf',S/'installer/Indice-guide.md',S/'installer/Indice-guide.pdf']:
    save_previous(p)
for kind in ('pratica','teorica'):
    p=S/f'docs/guida-{kind}-anthea.md'
    text=p.read_text(encoding='utf-8')
    text=re.sub(r'^#{4,6} ', '### ', text, flags=re.M)
    p.write_text(text,encoding='utf-8')
p=S/'README.md'; text=p.read_text(encoding='utf-8')
end=text.index('| Cartella |')
text='# Materiale di supporto ANTHEA\n\nLa documentazione utente è raccolta in due guide globali Rev08 del 2 ottobre 2026:\n\n- [Guida pratica e UI](docs/guida-pratica-anthea.md): procedure e interfaccia di tutti i moduli.\n- [Guida teorica](docs/guida-teorica-anthea.md): modelli, formule, ipotesi, limiti, approfondimenti e appendici tecniche di tutti i moduli.\n\nEdizioni Word e PDF in `documentazione/Guide_ANTHEA`, PDF omonimi accanto ai Markdown. Gli approfondimenti di Bridge Design, pali, sezioni, muri e stabilità globale sono inclusi nei due volumi. Le fonti precedenti sono conservate in `SUPERATI/guide-unificate-rev08-20261002`, con registro di origine e sostituzione.\n\nOgni nuovo argomento va integrato nelle due guide; per casi particolari chiedere all’utente prima di introdurre una diversa organizzazione. Modelli, esempi di calcolo ed evidenze restano nelle loro cartelle.\n\n'+text[end:]
p.write_text(text,encoding='utf-8')
p=S/'installer/README.md'; text=p.read_text(encoding='utf-8')
start=text.index('La cartella `Guide`'); end=text.index('`Documentazione.ps1`',start)
text=text[:start]+'''La cartella `Guide` accanto ad `ANTHEA.exe` contiene tre PDF:

- `Indice-guide.pdf`: indice dei due volumi globali;
- `Guida pratica ANTHEA.pdf`: uso e UI di tutti i moduli;
- `Guida teorica ANTHEA.pdf`: teoria, formule, ipotesi e limiti di tutti i moduli.

Tutti gli approfondimenti sono incorporati nelle due guide globali Rev08. Nel menu Start sono presenti Indice delle guide, Guida pratica, Guida teorica e Documentazione. La documentazione è obbligatoria anche nelle installazioni silenziose.

Per i nuovi argomenti aggiornare i due volumi, i PDF corrispondenti e l’indice. Per casi particolari chiedere all’utente prima di creare un documento autonomo. `Guide.json` contiene soltanto l’indice; i due manuali sono selezionati automaticamente alla revisione più recente.

'''+text[end:]
p.write_text(text,encoding='utf-8')
index='# ANTHEA — Indice della guida globale\n\nITEC Engineering · Revisione 08 · 2 ottobre 2026\n\nUn manuale globale articolato in due volumi, consultabili offline dal menu Start → ANTHEA.\n\n- **Guida pratica ANTHEA.pdf**: uso, UI, procedure, dati, risultati e relazioni di tutti i moduli.\n- **Guida teorica ANTHEA.pdf**: modelli, formule, ipotesi, limiti e approfondimenti di tutti i moduli.\n\nSono inclusi materiali e durabilità, progetti e revisioni, pali e micropali verticali e orizzontali, Broms e metodo stratificato, sezioni CA, sezioni composte, connessioni e appoggi, Bridge Design, muri di sostegno e stabilità globale. Le appendici tecniche conservano audit e studi datati, distinguendoli dalle istruzioni correnti.\n\n'
for kind in ('pratica','teorica'):
    lines=(S/f'docs/guida-{kind}-anthea.md').read_text(encoding='utf-8').splitlines()
    index+=f'## Volume {kind}: capitoli e approfondimenti\n\n'
    for line in lines:
        if line.startswith('## ') and line!='## Approfondimenti integrati': index+='- '+line[3:]+'\n'
    index+='\n'
(S/'installer/Indice-guide.md').write_text(index,encoding='utf-8')
p=ROOT/'README.md'; text=p.read_text(encoding='utf-8').replace('Le guide complete della versione del 26 settembre 2026','Le due guide globali complete Rev08 del 2 ottobre 2026');p.write_text(text,encoding='utf-8')
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
spec=importlib.util.spec_from_file_location('builder',S/'scripts/Build-AntheaGuides-Itec.py'); B=importlib.util.module_from_spec(spec);spec.loader.exec_module(B)
for kind in B.GUIDES: B.build(kind)
spec=importlib.util.spec_from_file_location('pdf',S/'scripts/documentazione/markdown-pdf.py'); P=importlib.util.module_from_spec(spec);spec.loader.exec_module(P)
for p in [S/'README.md',S/'installer/README.md',S/'installer/Indice-guide.md',ARCH/'README.md']: P.build(p)
