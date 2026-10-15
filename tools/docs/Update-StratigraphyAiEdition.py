"""Update the unpublished Rev46 global guides; retain the preceding draft with hashes."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
ARCH = ROOT / 'supporto/SUPERATI/stratigrafia-rev46-prima-ai-20261015'
paths = [ROOT / f'supporto/docs/guida-{kind}-anthea.{ext}' for kind in ('pratica', 'teorica') for ext in ('md', 'pdf')]
paths += list((ROOT / 'supporto/documentazione/Guide_ANTHEA').glob('*Rev46.*'))
entries = []
for source in paths:
    target = ARCH / source.relative_to(ROOT)
    if not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    entries.append({'origine': source.relative_to(ROOT).as_posix(), 'archivio': target.relative_to(ROOT).as_posix(),
                    'sha256': hashlib.sha256(target.read_bytes()).hexdigest()})
(ARCH / 'registro.json').write_text(json.dumps({'motivo': 'Bozza Rev46 sostituita dalla Rev46 con medi/minimi e importatore AI, prima del rilascio', 'files': entries}, ensure_ascii=False, indent=2), encoding='utf-8')
practical = '''### Valori medi e minimi

I valori medi costituiscono l’insieme principale. Attivare Gestisci anche i valori minimi nello strato per inserire i minimi eventualmente forniti dalla relazione geologica; questi campi sono opzionali e non vengono riempiti copiando i medi. Le due colonne hanno fonte e qualificazione distinte. La rimozione dei minimi richiede conferma.

La scelta Parametri da usare nelle verifiche ammette Medi, Minimi oppure Entrambi. La scelta è salvata insieme alla geometria comune e riportata nella scheda dati. Non avvia calcoli: il collegamento ai moduli di verifica resta successivo. I medi pertinenti saranno comunque richiesti dal modulo di verifica; scegliendo Minimi o Entrambi saranno richiesti anche i corrispondenti minimi. Non occorre compilare cu per una verifica che richiede soltanto parametri efficaci. Le bozze incomplete possono essere salvate, con avviso. Un minimo superiore al medio richiede controllo della fonte e del significato del dato.

### Importazione assistita della relazione

Aprire Importa relazione PDF con AI, selezionare un PDF fino a 20 MiB e inserire il nome di un modello OpenAI compatibile con PDF e output strutturato e la propria chiave API. I campi possono essere predisposti tramite le variabili di ambiente ANTHEA_AI_MODEL e OPENAI_API_KEY. La chiave non viene salvata nei documenti ANTHEA.

Il comando Invia PDF a OpenAI e prepara bozze trasmette l’intero documento al servizio remoto e può comportare costi sul proprio account API. Non effettua elaborazione AI locale. Interrompi annulla l’attesa; nessun dato viene applicato automaticamente alla scheda aperta. Errori del servizio o risposte incomplete richiedono una nuova richiesta, eventualmente con un estratto più breve della relazione.

Selezionare il sondaggio o profilo dall’elenco, controllare gli avvisi e correggere la bozza nell’editor. Profili differenti restano separati e se ne applica uno alla volta. Le fonti riportano per parametro le pagine PDF indicate dal modello: confrontarle con il documento originale. I valori non riconosciuti come medi o minimi restano nelle note da classificare. Dati assenti, geometrie ambigue e unità non determinabili non devono essere completati per supposizione; l’estrazione AI può contenere errori.

Dopo il confronto con la relazione, selezionare Ho confrontato dati, unità e pagine della bozza con la relazione e applicare il profilo revisionato. Ogni modifica o cambio di profilo richiede di ripetere questa attestazione. Il comando sostituisce la stratigrafia della scheda aperta; chiudere la finestra conserva i dati precedenti. Geometrie e valori invalidi impediscono l’applicazione; i parametri mancanti restano una bozza salvabile. Il documento conserva nome e impronta del PDF, modello, data, fonti e avvisi; non incorpora il PDF originale. La scheda Word/PDF espone medi, minimi e provenienza dell’importazione.

'''
theory = '''### Insiemi di parametri

La stratigrafia conserva un insieme principale di valori medi e un insieme opzionale di valori minimi per strato, con identica geometria e falda. Fonte e qualificazione sono distinte. La scelta Medi, Minimi o Entrambi rappresenta lo scenario richiesto, senza attribuire automaticamente natura caratteristica o di progetto ai dati. I coefficienti parziali di sicurezza non appartengono a questi insiemi.

L’assenza dei minimi non equivale ai medi. Per l’impiego futuro, il modulo di verifica dichiara i parametri necessari: il contratto richiede sempre i medi pertinenti e, per gli scenari che li usano, anche i minimi corrispondenti. Le copie degli scenari sono indipendenti e la stratigrafia originale resta integra. Non è imposto un obbligo normativo generale di doppio calcolo: la scelta dipende dal problema, dalle ipotesi progettuali e dal metodo della verifica.

### Limiti dell’estrazione assistita

L’importatore produce una trascrizione proposta e non una caratterizzazione geotecnica autonoma. Non calcola statistiche, correlazioni o coefficienti; richiede la revisione del progettista. I valori minimi esplicitamente forniti dalla relazione possono essere acquisiti senza ricavarli dai medi. Valori caratteristici o di progetto non identificabili come medi o minimi devono essere classificati in sede di revisione.

Il termine di un sondaggio non dimostra l’estensione indefinita dell’ultimo strato. Una relazione con più profili non autorizza a fonderli in una sola colonna. La fonte associata a un valore è proposta dal modello e va verificata, incluse conversioni di unità e numerazione delle pagine PDF. L’impronta del documento importato consente di identificare il file sorgente, ma non dimostra la correttezza dell’interpretazione.

'''
for kind, addition, anchor in [('pratica', practical, '## Avvio e scelta del modulo'), ('teorica', theory, '## Architettura del calcolo e convenzioni')]:
    path = ROOT / f'supporto/docs/guida-{kind}-anthea.md'
    text = path.read_text(encoding='utf-8')
    if addition.splitlines()[0] not in text:
        text = text.replace(anchor, addition + anchor, 1)
    path.write_text(text, encoding='utf-8')
print('Guide Rev46 aggiornate; bozza precedente conservata con impronte.')
