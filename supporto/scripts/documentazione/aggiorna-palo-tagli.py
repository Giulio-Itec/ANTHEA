"""Rev25: quote dei tratti come estremi di taglio, giunto entrante unico."""
from pathlib import Path
import hashlib, json, shutil, subprocess, sys

root=Path(__file__).resolve().parents[3]
support=root/'supporto'
archive=support/'SUPERATI/palo-tagli-rev25-20261005'
paths=[support/f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica','teorica') for ext in ('md','pdf')]
paths+=list((support/'documentazione/Guide_ANTHEA').glob('*Rev24.*'))
paths+=[support/name for name in ('README.md','README.pdf','installer/Indice-guide.md','installer/Indice-guide.pdf')]
paths+=list((support/'esempi/palo-orizzontale-armature').glob('*'))
records=[]
for path in paths:
    if not path.is_file(): continue
    destination=archive/path.relative_to(support)
    destination.parent.mkdir(parents=True,exist_ok=True)
    if not destination.exists(): shutil.copy2(path,destination)
    records.append(dict(origine=str(path.relative_to(root)),archivio=str(destination.relative_to(root)),
        motivo='Sostituita su richiesta utente la continuità automatica con gruppi delimitati dai tratti e giunto entrante',
        sostituzione=str(path.relative_to(root)).replace('Rev24','Rev25'),sha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
(archive/'registro.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

practical=support/'docs/guida-pratica-anthea.md'
text=practical.read_text(encoding='utf-8-sig').replace('revisione documentale 24','revisione documentale 25')
text=text.replace('Per i giunti commerciali si adotta il massimo fra iniziale e richiesta; al cambio di diametro si riserva anche lo sviluppo necessario dei due lati, indicandolo separatamente.', 'Per ogni giunto si adotta il massimo fra lunghezze iniziali e richieste delle armature collegate. La barra entrante risale di questa lunghezza; la barra superiore termina alla fine del suo tratto.')
text=text.replace('La distinta di ciascun tratto indica il gruppo continuo, quantità, diametro, quote fisiche, lunghezza di taglio, barra commerciale e sovrapposizione. Un gruppo che attraversa più tratti è unico: le righe mostrate nei singoli tratti sono richiami, non quantità da sommare nuovamente.', 'La distinta di ciascun tratto indica il proprio gruppo, quantità, diametro, quote fisiche, lunghezza di taglio, barra commerciale e sovrapposizione. Ogni pezzo è conteggiato una sola volta; i giunti richiamano i pezzi collegati senza aggiungere altre quantità.')
text=text.replace('Le quote sono misurate dalla testa; una fine vuota indica la punta.', 'Le quote sono misurate dalla testa; una fine vuota indica la punta. Fine tratto / barre è la quota fisica finale del gruppo: il primo inizia a zero, ogni successivo risale dal proprio inizio della sovrapposizione adottata.')
text=text.replace('gli sviluppi esterni al modello restano visibili.', 'le quote fisiche dei gruppi sono leggibili accanto alle barre.')
text=text.replace('I gruppi longitudinali continui sono riportati una sola volta.', 'I gruppi dei tratti e i relativi tagli sono riportati una sola volta.')
start=text.index('Al cambio di armatura la distinta segue le singole barre.')
end=text.index('Salvataggio, JSON, CSV e report',start)
text=text[:start]+'''La fine del tratto è la fine fisica delle barre. Con un palo di 20 m, tratti 0–12, 12–18 e 18–20 m e sovrapposizione adottata di 1,20 m, i gruppi risultano 0–12, 10,80–18 e 16,80–20 m: lunghezze 12,00, 7,20 e 3,20 m. Il primo gruppo parte dalla testa; ogni gruppo seguente risale di l0 rispetto al proprio inizio. Anche due tratti con identica armatura rimangono gruppi separati. Il calcolo non sposta le estremità per soddisfare ancoraggi o sviluppo: segnala gli eventuali controlli da completare.

Un gruppo è spezzato internamente soltanto se non entra nelle lunghezze commerciali ammesse; i tagli conservano il primo inizio e l'ultima fine, con sovrapposizioni intermedie comprese nelle quantità. Un tratto netto di 12 m dopo il primo richiede più di 12 m di barra per il giunto entrante: con limite commerciale 12 m viene quindi spezzato. Le barre commerciali più lunghe vengono tagliate alla lunghezza richiesta, senza allungare il gruppo.

I giunti J mostrano coppie effettivamente allineate rispetto a quelle previste, quote, lunghezze iniziale, richiesta, adottata ed effettiva. Passando da 20 barre equidistanti a 12, non tutte le direzioni coincidono: il programma conserva quantità e tagli ma segnala la disposizione trasversale da definire. Finestra troppo corta, giunti interferenti e distanza non ammessa impediscono di considerare il dettaglio soddisfatto. Le sezioni sono nominali; accostamento, confinamento, sfalsamento e chiusure delle staffe restano da completare. I vecchi valori archiviati di sviluppo esterno sono conservati ma non modificano i tagli di questa modalità.

La vista finale deriva dai risultati e rimane preliminare quando mancano dettagli. Non somma l'armatura sovrapposta alla resistenza nominale del tratto. La distinta separa barre, giunti e staffe, con le relative unità.

L’esempio [Palo 12 m con quattro tratti](../esempi/palo-orizzontale-armature/palo-12m-quattro-tratti.programma) mostra quattro gruppi distinti; il [riepilogo](../esempi/palo-orizzontale-armature/palo-12m-quattro-tratti.pdf) descrive input e limiti. L’esempio [Palo 20 m, tagli 12–18–20](../esempi/palo-orizzontale-armature/palo-20m-tagli-12-18-20.programma) usa 20Ø20 nel primo tratto e 12Ø16 negli altri: le sovrapposizioni adottate sono rispettivamente 1,20 e 1,00 m. Le barre risultano 0–12, 10,80–18 e 17–20 m. Il [riepilogo degli input](../esempi/palo-orizzontale-armature/palo-20m-tagli-12-18-20.pdf) esplicita anche le coppie da sistemare al cambio 20→12.

'''+text[end:]
practical.write_text(text,encoding='utf-8')

theory=support/'docs/guida-teorica-anthea.md'
text=theory.read_text(encoding='utf-8-sig').replace('revisione documentale 24','revisione documentale 25')
start=text.index('### Continuità delle barre e proposta costruttiva')
end=text.index('Per non accreditare capacità a barre insufficientemente sviluppate,',start)
text=text[:start]+'''### Tagli dei tratti e sovrapposizione entrante

Le lunghezze di ancoraggio e sovrapposizione richiamano il motore Checker già validato, con barre ad aderenza migliorata, σsd=fyd e nessuna riduzione favorevole di forma o confinamento (α1…α5=1). La resistenza a trazione usata per l'aderenza è limitata a C60/75. Buona aderenza è una scelta esplicita. I risultati conservano lbd, l0, percentuale assegnata e controllo della distanza libera. La lunghezza iniziale 60φ, arrotondata per eccesso a 0,10 m, è una preferenza dell'utente, distinta dalla lunghezza richiesta dal verificatore.

La partizione determina i tagli fisici. Per un tratto [a_i,b_i], il primo gruppo occupa [0,b_1]; ogni gruppo successivo occupa [max(0,a_i−l0_i),b_i]. La lunghezza adottata l0_i è il massimo delle lunghezze iniziali e richieste delle due armature collegate. La quota b_i rimane invariata. Questa è una convenzione geometrica richiesta dall'utente, non una prescrizione normativa: non dimostra da sola il pieno sviluppo dell'armatura alla quota di cambio. Non si sommano lbd o traslazioni alla lunghezza fisica, non si fondono gruppi identici e non si estendono automaticamente le barre oltre testa, punta o fine tratto. Un giunto privo dello spazio disponibile viene segnalato.

Lo sviluppo richiesto per usare la resistenza conserva lbd+a_l, con a_l=z cotθ/2 per staffe ortogonali (EN1992-1-1 §9.2.1.3, presentazione JRC Arrieta 2011, diapositiva 33). Se il taglio non è confermato si usa il limite superiore cotθ=2,5 a fini preliminari. Questo controllo non modifica le quote scelte: può lasciare tratti non verificati. Le formule del motore di aderenza sono invariate; la nuova regola riguarda esclusivamente i tagli e la costruzione della distinta.

La finestra del giunto è l'intersezione fisica dei due gruppi e termina al confine dei tratti. Si conservano l0 iniziale, richiesta, adottata ed effettiva. Il calcolo assume il 100% delle giunzioni alla stessa quota; una percentuale richiesta inferiore rimane da realizzare mediante sfalsamento esplicito. Si associano soltanto barre sulla stessa direzione radiale nominale e si indicano coppie allineate e coppie previste. Per 20Ø20 e 12Ø16 su corone regolari con lo stesso orientamento coincidono quattro direzioni su dodici richieste: le altre non vengono considerate automaticamente giuntate. La distanza trasversale, le interferenze tra finestre e la lunghezza disponibile partecipano allo stato del dettaglio. Il disegno non certifica piegature, accostamento o confinamento.

Il motore riutilizza i tagli commerciali soltanto se il gruppo eccede una barra disponibile. Conserva gli estremi assegnati e introduce giunti interni; ogni lunghezza reale è conteggiata una sola volta. Con tratti 0–12, 12–18 e 18–20 m, l0=1,20 m e venti barre per gruppo si hanno 20×(12+7,20+3,20)=448 m. La lunghezza commerciale scelta può superare quella di taglio; la differenza è uno sfrido, non una modifica del gruppo. La proposta a passo 0,5 m riserva una sola sovrapposizione entrante dopo il primo tratto, mantenendo lunghezza minima 3 m e catalogo commerciale assegnato.

Le regressioni indipendenti controllano quote esatte, quantità, mancata fusione di armature identiche, cambio di diametro e quantità, tagli interni, spazio insufficiente, interferenza dei giunti e conservazione delle quote rispetto ai vecchi sviluppi esterni archiviati. L'esempio 20 m riproduce la configurazione 20Ø20 / 12Ø16 / 12Ø16 e distingue giunti geometricamente definiti dai dettagli ancora da verificare.

'''+text[end:]
text=text.replace('La lunghezza teorica e lo sviluppo max(lbd,l0)+a_l riservato conservativamente a entrambe le estremità devono entrare', 'La lunghezza del primo tratto, e quella dei successivi aumentata della sola sovrapposizione entrante, devono entrare')
text=text.replace("La distinta mantiene continue le barre con identica disposizione su tratti adiacenti. I pezzi preferiscono lunghezze commerciali 6/8/10/12 m; l'ultimo può essere tagliato alla quota fisica richiesta.", 'La distinta mantiene distinti i gruppi dei tratti anche con identica disposizione. I pezzi preferiscono lunghezze commerciali 6/8/10/12 m e vengono tagliati alle quote richieste; un gruppo eccedente il massimo viene spezzato internamente conservandone gli estremi.')
theory.write_text(text,encoding='utf-8')

for name in ('README.md','installer/Indice-guide.md'):
    path=support/name
    text=path.read_text(encoding='utf-8-sig').replace('Rev24','Rev25').replace('Revisione 24','Revisione 25')
    if 'palo-20m-tagli-12-18-20' not in text:
        prefix='' if name=='README.md' else '../'
        text+=f'\n- Esempio aggiornato: [palo 20 m, tratti 0–12 / 12–18 / 18–20]({prefix}esempi/palo-orizzontale-armature/palo-20m-tagli-12-18-20.programma), con [dati e tagli in PDF]({prefix}esempi/palo-orizzontale-armature/palo-20m-tagli-12-18-20.pdf).\n'
    path.write_text(text,encoding='utf-8')
path=support/'scripts/Build-AntheaGuides-Itec.py'
path.write_text(path.read_text(encoding='utf-8').replace("REVISION = '24'","REVISION = '25'").replace("REVISION_NOTE = 'CONTINUITÀ DELLE BARRE E GIUNTI DEL PALO'","REVISION_NOTE = 'TAGLI DEI TRATTI E SOVRAPPOSIZIONI DEL PALO'"),encoding='utf-8')
path=support/'scripts/Render-AntheaGuides-Itec.ps1'
path.write_text(path.read_text(encoding='utf-8-sig').replace("$Revision = '24'","$Revision = '25'"),encoding='utf-8-sig')
path=root/'lib/Checker/manifest.json'
manifest=json.loads(path.read_text(encoding='utf-8-sig'))
manifest['source']+='; Concrete da working tree con tagli fissati dai tratti, sovrapposizione entrante unica e spezzatura commerciale interna, 5 ottobre 2026'
for entry in manifest['assemblies']:
    if entry['file']=='GPCChecker.Concrete.dll': entry['sha256']=hashlib.sha256((root/'lib/Checker'/entry['file']).read_bytes()).hexdigest().upper()
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
subprocess.run([sys.executable,str(support/'scripts/wiki/build-wiki-index.py')],check=True)
art=support/'artefatti/guide_anthea_itec_rev25'
art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Aggiornare le due guide globali preservando il modello ITEC: quote dei tratti come estremi fisici delle barre, una sovrapposizione entrante e tagli commerciali interni. Distinguere la scelta geometrica dai controlli di sviluppo e disposizione.',encoding='utf-8')
subprocess.run([sys.executable,str(support/'scripts/Build-AntheaGuides-Itec.py')],check=True)
for name in ('README.md','installer/Indice-guide.md'):
    subprocess.run([sys.executable,str(support/'scripts/documentazione/markdown-pdf.py'),str(support/name)],check=True)
