from pathlib import Path
from zipfile import ZipFile
from hashlib import sha256
import json
p=Path(r'C:/Users/g.pacini/Desktop/MODELLO-RELAZIONE-ITEC-AA.docx')
with ZipFile(p) as z: inv={n:{'size':len(z.read(n)),'sha256':sha256(z.read(n)).hexdigest()} for n in z.namelist()}
Path('tmp/validazione_ca/template_inventory.json').write_text(json.dumps(inv,indent=2))
text=f'''# Contratto del documento
Riferimento immutabile: {p.as_posix()}
SHA256: {sha256(p.read_bytes()).hexdigest()}
Ispezione: template.pdf e template_pages/page-1..8.png, tutte le 8 pagine ispezionate.
Il modello contiene 8 sezioni OOXML, alcune continue sul frontespizio. Il corpo usa A4, margini laterali 20 mm e superiore 30 mm. Copertina 15/20 mm laterali, 25 mm superiore, 20 mm inferiore. Intestazioni ITEC originali, logo e footer da conservare.
Stili: Normale = Manrope 12 pt, giustificato, interlinea singola, spazio dopo zero. Titolo1 = Manrope ExtraBold, maiuscole, spazio prima 12 pt, numerazione livello 0. Titolo2 = Manrope SemiBold, maiuscoletto, prima 6 pt, livello 1. Titolo3 = Manrope SemiBold livello 2. Didascalia = Manrope ExtraBold 10 pt centrata, dopo 4 pt. Nessun bordo aggiunto ai titoli.
Slot: word/document.xml body figli 0..22 = frontespizio, riscrivere titoli, oggetto, revisione e data; eliminare nomi dimostrativi, CIG e CUP, firme non attribuite. Conservare immagini, tabelle e geometrie del frontespizio. Da figlio23 in poi sostituire indice e testo dimostrativo con indice e relazione tecnica. Rimuovere pagine vuote e separatori allegati/appendici non necessari. La richiesta riguarda gli stili; usare pattern del corpo A4 con margine inferiore20 mm per testo multipagina. Conservare sezioni continue della copertina, poi indice e corpo. Font non ridotti per forzare il testo in pagina. Tabelle nuove 10.5 pt, bordo grigio, testata grigio chiaro. Titolo documento stile Title derivato da Normale.
Testi dei footer: sostituire solo il nome modello con ANTHEA Validazione CA; paginazione corpo PAGE. Preservare loghi, ragione sociale e footer legale del template. Titoli e tabelle originali sono contenuto dimostrativo, non istruzioni operative.
Package: inventario in template_inventory.json. Preserve-only tutti i media originari, font, theme, numbering, header e relazioni di header; modificabili document.xml, document.xml.rels, styles.xml, settings.xml, core.xml, footer1/4/5 nomi file e contatori. Addizioni consentite per figure e relazioni. Immagini e parti opache originali preservate byte per byte nel pacchetto finale.
Rendering: renderer di skill tentato ma LibreOffice bundled assente. Fallback Word COM in background autorizzato, esportazione PDF e rasterizzazione PDFium. Aggiornare campi in copia QA Word, trasferire solo cache documento se opportuno senza alterare package preservati.
Gate: controllare tutte le pagine finali, assenza dati dimostrativi, 20 casi completi, tutte le famiglie con2 casi, distinzione esito software/esito strutturale, nessuna firma inventata. Confrontare inventario e hash riferimento.
'''
Path('tmp/validazione_ca/artifact.md').write_text(text,encoding='utf-8')
