# Studio della condivisione dati tra i fogli ANTHEA
Data: 23 settembre 2026. Analisi del codice presente nel workspace; non modifica ai calcoli o alla sincronizzazione.
Ambito: sei moduli disponibili. Il micropalo strutturale in preparazione è escluso. Le equivalenze riportate sono un progetto software basato sui significati e sui consumatori del codice; non costituiscono una nuova validazione normativa dei motori.

## Conclusione
La condivisione attuale è parziale e troppo legata ai nomi JSON e alle categorie Materiali/Geometria/Armatura. Occorre un registro di proprietà fisiche con adattatori per modulo, unità, condizioni di applicabilità e dipendenze. Non è corretto estendere indiscriminatamente la copia di campi.
Prima priorità: esposizione Materiali–SLE, diametro palo verticale–orizzontale–CA, identità dell'elemento, staffe complete, falda e terreno. Servono inoltre controlli di capacità del modulo: un dato presente nel JSON non è necessariamente editabile o utilizzato dal calcolo.

## Moduli e sorgenti
| Sigla | Modulo | Identificativo | Dati principali |
|---|---|---|---|
| PV | Palo verticale | geo_palo_verticale | generali, efficienza, stratigrafie |
| PO | Palo orizzontale | geo_palo_orizzontale | generali, sezione, verifica, stratigrafie |
| MV | Micropalo verticale | geo_micropalo_verticale | generali, efficienza, stratigrafie Bustamante–Doix |
| MO | Micropalo orizzontale | geo_micropalo_orizzontale | generali, sezione CHS, verifica, stratigrafie |
| CA | Verifica sezione in c.a. | str_palo | input, combinazioni, workspace_ca |
| MAT | Materiali–Calcestruzzo | mat_calcestruzzo | classe, numeri, scelte, opzioni, esposizioni |

Fonti esaminate:
- X.Core/ProjectSharedData.cs; X.Desktop/Wpf/ProjectSharing.cs: confronto, ereditarietà e propagazione attuali.
- X.Core/Archivio.cs; X.Desktop/Wpf/GeoEditor.cs; X.Core/Calcolo.cs; X.Core/Micropali.cs: moduli verticali, dati visibili, conversione asse/profondità e peso.
- X.Core/PaloOrizzontale.cs; X.Core/MicropaloOrizzontale.cs; X.Desktop/Wpf/HorizontalWorkspace.cs; HorizontalMaterials.cs: modelli orizzontali, sezione efficace, campi UI.
- X.Core/SezioneCA.cs; SectionWorkspace.cs; CheckerSection.cs; ConcreteMaterials.cs; ConcreteMaterialCatalog.cs; ConcreteStandards.cs; Ntc2018Checks.cs.
- X.Desktop/Wpf/ConcreteWorkspace.cs; ConcreteParameters.cs; ConcreteShear.cs; ConcreteStress.cs; ConcreteRefinements.cs; ConcreteTendons.cs.
- X.Materiali/MaterialState.cs; MaterialDetails.cs; Bond.cs; ExposureSelector.cs; MinimumConcrete.cs; MixAutomation.cs.

## Matrice delle proprietà: geometria ed elemento
Stato: “parziale” significa che esiste un collegamento ma non copre tutti i casi o la semantica necessaria.
Tutte le condivisioni presuppongono lo stesso elemento fisico, non soltanto la stessa cartella.

| Proprietà | Percorsi e moduli | Regola proposta | Stato attuale |
|---|---|---|---|
| Diametro esterno palo in CLS | PV/PO generali.diametro [m]; CA input.diameter_mm [mm] | Bidirezionale, Dmm=1000Dm; CA circolare; associare CA al palo | Presente con condizioni; non ancora completo |
| Forma della sezione resistente | CA input.shape; PO circolare imposto dal motore | PO può inizializzare CA circolare; CA rettangolare/T non convertibile in PO | Correzione recente; conflitto di forma ora rilevabile |
| Diametro perforazione micropalo Db | MV generali.diametro [m]; MO generali.diametro [m] etichettato geotecnico | Collegamento MV–MO solo dopo conferma della definizione del diametro resistente lateralmente | Oggi confluisce nella stessa chiave dei pali: da separare |
| Diametro bulbo iniettato Ds | MV: Db × alpha per strato | Derivato per terreno/iniezione, non una dimensione unica da copiare in MO | Non distinto nel registro |
| Tubolare CHS | MV generali.profilo_chs; MO sezione.profilo_chs / modo_chs / diametro_chs_mm / spessore_chs_mm | Catalogo↔catalogo bidirezionale; manuale MO→MV solo se profilo rappresentabile, altrimenti incompatibilità | Non collegato tra MV e MO |
| Lunghezza palo | PV/PO generali.lunghezza [m] | Stessa origine, quota testa/piano campagna e tratto infisso | Copiata senza metadati di riferimento |
| Lunghezza micropalo inclinato | MV lunghezza lungo asse e inclinazione; MO lunghezza infissa senza inclinazione equivalente | Non assumere uguaglianza; proiezione Lcosθ solo con riferimenti dichiarati, altrimenti blocco | La copia attuale non controlla inclinazione |
| Rettangolo/T | CA width_mm, height_mm, flange_width_mm, web_width_mm, flange_thickness_mm | Tra CA dello stesso elemento; campi attivi per forma | Presenti; confronto include anche parametri inattivi |
| Coordinate/assi sezione | CA input barre; workspace_ca.sle_comuni assi, origine_x/y, rotazione | Coordinate fisiche comuni, trasformazioni delle azioni locali | Nessuna distinzione generale tra sistema fisico e sistema di verifica |

Il problema del diametro PV non è semplicemente “campo assente”: è già mappato e la prova 1200 mm↔1,2 m è presente. Possibili condizioni bloccanti accertate nel codice: CA non circolare; fogli in gruppi diversi; categoria geometria in conflitto; aggiornamento non confermato o locale. Senza un file del caso segnalato non è possibile attribuire a una sola di queste cause il comportamento osservato.

## Matrice: materiali, ambiente e durabilità
| Proprietà | Percorsi/moduli | Regola proposta | Stato attuale |
|---|---|---|---|
| Classe CLS/fck | MAT classe; CA input.classe_cls/fck_mpa; PO sezione.classe_cls/fck_mpa | Proprietà unica con nome e resistenza coerenti; custom non rappresentabile in MAT segnalato | Collegamento presente |
| Diagramma CLS | CA cls_diagramma; PO usa SezioneCA, non lo stesso motore costitutivo di CA | Identità del materiale comune; modello costitutivo specifico dichiarato | Copia basata sul campo, non sulla capacità del motore |
| Acciaio ordinario | CA e PO fyk_mpa, steel_modulus_mpa; CA anche classe_acciaio, fu, eps_u, diagramma | Condividere fy/Es; altri parametri solo se realmente consumati; aggiornare nomi e preset insieme | Parziale; campi CA aggiuntivi non garantiti nel destinatario |
| Acciaio CHS | MO fy_chs_mpa, gamma_m0 | Materiale tubolare distinto da barre B450; MV usa profilo per peso, non resistenza fy | Solo tra MO |
| Esposizioni | MAT esposizioni[] + esposizione_principale; CA workspace_ca.sle_comuni.esposizione e repliche sle.* | Lista canonica condivisa. CA attuale accetta una sola classe: valutare tutti i requisiti applicabili e mostrare quello governante senza perdere la lista | Collegamento MAT–CA assente |
| Esposizione scelta nel menu | MAT esposizione_principale | Scelta di visualizzazione, non distinta proprietà ambientale | Oggi confrontata come dato: possibile falso conflitto |
| Vita utile | MAT scelte.life | Dato comune dell'elemento, consumato dove implementato | Solo tra MAT |
| Classe minima richiesta | MAT minimumConcreteClass derivata dalle esposizioni | Requisito, non classe adottata: confrontare con CLS scelto, non sostituirlo silenziosamente | Derivata; nessun collegamento di requisito alle verifiche |
| Dmax | MAT numeri.aggregate | Proprietà materiale comune; influenza copriferro e prescrizione | Solo tra MAT, nessun consumatore esplicito equivalente in CA/PO |
| Copriferro adottato | CA input.cover_mm; PO sezione.cover_mm [mm] | Distanza netta al lato esterno della staffa, bidirezionale | Presente |
| Copriferro richiesto | MAT cnom calcolato, tolleranza, getto, abrasione, qualità | Vincolo c_adottato≥c_richiesto; proposta esplicita per adottarlo | Non collegato, non equiparare input e risultato |
| Diametro barra per durabilità/aderenza | MAT numeri.diameter; CA/PO più diametri barre e staffe | Richiede scelta della barra/superficie verificata; più famiglie non riducibili automaticamente a un singolo numero | Assente |
| Copriferro fessurazione | CA sle_comuni.copriferro_fessure | Nel codice default = cover_mm + Østaffa; valore diverso dal copriferro nominale. Conservare origine auto/manuale | Non incluso |
| Aderenza | MAT scelte.bondCondition buone/altre; CA sle_comuni.aderenza migliorata/liscia | Sono proprietà diverse: condizioni di getto vs superficie barra; NON tradurre l'una nell'altra | Nessun collegamento, correttamente da non unificare |
| αct, γc aderenza | MAT numeri.bondAlpha/bondGamma; CA coefficienti AlphaCT/GammaC | Solo con stessa norma/situazione di progetto; preservare modalità locale | Non collegati |
| αcc, γc, γs | CA input e workspace_ca.coefficienti; PO sezione | Parametri di verifica, non materiale intrinseco; sincronizzare copie e contesto normativo | Parte input condivisa; copie workspace sincronizzate all'apertura CA |
| Composizione/cemento | MAT scelte.cement, cementClass, cementEarly, consistency, numeri.cementName | Tra schede dello stesso materiale; a/c, cemento minimo, aria e cloruri derivati separatamente | Copia di interi oggetti MAT, granularità insufficiente |
| Peso specifico | PV/MV generali.peso_specifico_palo | Densità/peso indipendente da fck; MV usa miscela e CHS per peso lineare | Nessuna mappa dedicata |

La classificazione SLE oggi usa Ntc2018Checks.Exposures/CrackRequirement; Materiali gestisce tutte le esposizioni attive. Non bisogna prendere semplicemente la prima classe della lista o l'ultima nell'ordine alfabetico.

## Matrice: armature
| Proprietà | Percorsi/moduli | Regola proposta | Stato attuale |
|---|---|---|---|
| Corona circolare ordinaria | CA input / PO sezione: longitudinal_bar_count, longitudinal_bar_diameter_mm, cover_mm, transverse_bar_diameter_mm | Stessa distribuzione fisica; confrontare anche barre generate | Coperta per configurazione semplice |
| Barre rettangolo/T | CA top_bar_*, bottom_bar_*, side_bar_*, flange_bottom_* | Solo CA e forma appropriata; non importare in palo circolare | Coperta parzialmente per presenza dei campi |
| Secondi strati | CA second_*; SezioneCA supporta second_inner_* | CA↔CA; PO richiede esplicita capacità UI e verifica simmetria | Esclusi tra moduli, possibile differenza non evidenziata |
| Barre manuali | CA input.barre_manuali; SezioneCA legge barre_manuali | Intero assetto come oggetto atomico; verso PO ammissibilità/simmetria da verificare, non solo coincidenza di campi | CA↔CA; incompatibilità cross-modulo non mostrata come stato separato |
| Staffe: diametro/passo | CA input.transverse_*; PO espone diametro, non passo | Diametro fisico comune; passo usato nel taglio CA, non fingere utilizzo nel calcolo PO | Campi copiabili anche se UI/modello non li usa |
| Staffe: schema e bracci | CA workspace_ca.taglio: tipo_staffa, rami_x/y, rami_interni, schema_interno, rotazione_staffa | Comune tra CA; distinto dai parametri analitici di taglio | Mancanti nel confronto |
| Trefoli | CA workspace_ca.trefoli e relativi materiali | Geometria/materiali comuni fra CA; sigma0 richiede fase di precompressione comune | Array copiato integralmente, include sigma0 |
| Parametri manuali taglio | taglio.bw_*, d_*, asl_*, ancoraggio, alpha_*, cot_* | Separare proprietà fisiche da ipotesi e override di verifica; ricalcolo dei derivati | Non coperti, non copiare il blocco completo |

## Matrice: geotecnica
| Proprietà | Percorsi/moduli | Regola proposta | Stato attuale |
|---|---|---|---|
| Falda | PV/PO/MO generali.presenza_falda, profondita_falda [m] | Stesso sondaggio/origine quote. MV non usa la falda nel motore attuale | Assente |
| Sondaggi e stratigrafie | PV/PO/MO stratigrafie[][] | Identità stabile di sondaggio/strato, origine e ordine quote; non corrispondenza per solo indice | Assente |
| Parametri geotecnici comuni | tipologia, spessore, peso_specifico, peso_specifico_saturo, angolo_attrito, coesione_non_drenata, coesione_efficace | PV↔PO↔MO per medesimo terreno; mantenere parametri extra locali | Assente |
| Campi solo PV | addensamento, nc, laterale_attiva, tipo_palo, sottotipo | Locali al metodo verticale; non eliminare copiando stratigrafie | Non mappati |
| Terreno MV | terreno, spessore, alpha, laterale_attiva | Spessori/geometria sondaggio condivisibili con riferimento verticale; categorie Bustamante non traducibili automaticamente in φ, Cu | Nessuna conversione, da mantenere esplicita |
| Indagini | PV/MV generali.verticali_indagate; PO/MO verifica.verticali_indagate | Numero comune se riferito allo stesso insieme di indagini; ξ derivati | Assente |
| Efficienza gruppo | PV/MV efficienza.*; PO/MO verifica.efficienza_*, interassi direzionali | Layout fisico comune, metodi/η verticali e orizzontali differenti | Non unificare i coefficienti |
| Iniezione micropalo | MV IGU/IRS, pressione, inizio_aderenza, percentuale_punta | Specifico del metodo; può alimentare metadati esecutivi ma non tradurre in parametri Broms | Locale |
| Peso lineare micropalo | MV derivato da CHS e perforazione | Ricalcolo dopo modifica profilo/Db; mai copia di output | Derivato |

## Dati da mantenere locali e risultati da ricalcolare
- Azioni N, H, M, V, combinazioni, eccentricità e vincoli: uno stesso elemento non implica lo stesso caso di carico.
- Compressione positiva in PO contro N negativo a compressione in CA; nessuna copia diretta senza un collegamento di combinazioni esplicito.
- Momento resistente PO: risultato della sezione per una specifica forza assiale/metodo, oppure valore manuale con provenienza. Non è un materiale né il momento sollecitante CA.
- Fattori parziali geotecnici, modello/risoluzione numerica, dominio, limiti e scelte analitiche: locali salvo un profilo di verifica esplicitamente condiviso.
- fcd, fyd, Ecm derivato, area, inerzia, peso lineare, Nq/Kp, capacità, limiti durabilità, esiti: ricalcolare dai dati aggiornati.
- Stato risultati: oggi il destinatario è ricalcolato alla riapertura. Occorre registrare subito “da ricalcolare” e impedire export di risultati obsoleti.
- Normativa, situazione persistente/accidentale e convenzioni devono accompagnare coefficienti e modelli; non basta uguagliare fck.

## Difetti e limiti del meccanismo attuale
1. Common esamina l'intersezione delle chiavi: un parametro assente/incompatibile può non apparire nelle differenze. “Dati comuni coerenti” non significa stessa sezione resistente completa.
2. Forma, armatura avanzata e CHS necessitano stati espliciti: coerente, diverso, non rappresentabile, dato mancante, non utilizzato dal modulo.
3. Tutti i geo condividono diameter_mm: confonde palo, perforazione micropalo e associazione con sezione CA; la famiglia dell'elemento deve diventare parte della chiave.
4. Fields confronta dimensioni/armature inattive presenti nei default, specialmente fra moduli uguali: possibile falso conflitto.
5. MAT confronta numeri/scelte/opzioni come blocchi interi. Una modifica del cemento può propagare anche scelte locali di durabilità.
6. ChangedGroups/Apply propaga l'intera categoria modificata: può sovrascrivere altre eccezioni della stessa categoria. Serve diff delle sole proprietà modificate più anteprima.
7. Mancano proprietà fisiche in workspace_ca (esposizione, dettagli staffe), non solo campi input.
8. Identità materiale acciaio e resistenza possono divergere se un foglio cambia fy manualmente dopo una precedente copia del nome: servono transazioni coerenti.
9. Ereditarietà dipende dai peers e dai conflitti di categoria; l'ordine dei fogli non deve diventare implicitamente un criterio di autorità.
10. Valori null, stringhe vuote e default non hanno uno stato distinto; valori incompleti non vanno propagati come geometrie valide.
11. Registro delle conversioni privo di metadati di origine, coordinata, superficie e unità persistite.
12. Lo studio non ha aperto un progetto reale fornito dall'utente: il caso specifico del diametro va riprodotto sul relativo archivio se persiste dopo le correzioni.

## Struttura proposta
Mantenere modifica nei fogli, senza introdurre una nuova scheda obbligatoria. Aggiungere:
- Identità di elemento (palo, micropalo, sezione generica), materiale, sondaggio e superficie di esposizione.
- Registro di proprietà canoniche: ID, tipo, unità, etichetta, dominio, gruppo atomico, applicabilità e dipendenze.
- Adattatore per ogni modulo: lettura, scrittura, capacità di rappresentazione, conversione, invalidazione e verifica.
- Distinzione tra dato fisico adottato, requisito minimo, ipotesi di calcolo e risultato.
- Collegamento predefinito ai fogli dello stesso gruppo compatibili, con eccezioni esplicite per singola proprietà.
- Provenienza/versione: foglio sorgente, revisione, ultima applicazione e override; non dipendere dal nome o dall'ordine dell'albero.
- Anteprima “questi campi cambiano in questi fogli”, tutti/solo questo; copia atomica dopo validazione, annullabile.
- Nello spostamento di gruppo: conservare i dati e chiedere separatamente se riallinearli.
- Gerarchia visiva e ordine manuale restano indipendenti dalle relazioni dati.

## Piano di intervento e prove di accettazione
1. Registro e adattatori; separare diametro palo/perforazione/CHS; distinguere mancanti, inattivi e incompatibili.
2. Copertura base: dimensioni PV–PO–CA, CHS MV–MO, classe/fy/Es, corona semplice e staffe CA complete.
3. Ambiente: lista esposizioni persistita anche in CA, criterio governante SLE tracciabile, verifica copriferro adottato/richiesto e gestione esplicita della barra di riferimento.
4. Geotecnica: sondaggio/strato con ID e quote, falda, parametri fisici, indagini; preservare dati specifici dei metodi.
5. Armature avanzate: doppie corone, manuali, trefoli e fasi; audit dei motori prima di abilitare collegamenti.
6. Anteprima proprietà, eccezioni, annullamento, migrazione conservativa, stato dei risultati.

Prove necessarie per ogni proprietà:
- A→B e B→A con valori non predefiniti; ricostruzione del dato effettivamente usato dal motore, non solo JSON.
- Unità, decimali con virgola, null/vuoto, input non valido, reset e rimozione.
- Modifica locale, condivisa, annullamento e nessuna richiesta ripetuta.
- Preservazione di carichi, parametri esclusivi e altri elementi.
- Aggiunta/rimozione/riordino/spostamento; indipendenza dall'ordine dei peers.
- Salvataggio/riapertura; vecchi archivi non modificati senza consenso.
- Circolare contro rettangolare/T; perforazione contro CHS; materiale custom; catalogo contro manuale.
- Esposizioni multiple e campi inattivi; copriferro richiesto sotto/sopra quello adottato.
- Confronto di contorno, coordinate/diametri barre, proprietà materiale e sondaggio dopo propagazione.
- Invalidazione immediata dei risultati, aggiornamento UI e blocco export di esiti obsoleti.

## Decisioni da definire nell'implementazione
- Ambito stesso elemento dentro una sezione organizzativa con più elementi: introdurre un ID, senza costringere a duplicare la struttura dei progetti.
- Esposizioni per superficie o per elemento: predisporre la lista con ambito; non appiattire esposizioni diverse di parti diverse.
- Override permanente o singola modifica locale: mostrare l'effetto della prossima sincronizzazione.
- Propagazione dei requisiti di durabilità: proporre l'adozione del valore, senza imporre silenziosamente il copriferro.
- Collegamento terreni condiviso a livello superiore: per ora preservare il comportamento di sottosezioni indipendenti, salvo scelta esplicita.

Esito: fattibile, ma l'estensione va costruita sul modello semantico e verificata a livello dei motori. La sola aggiunta di chiavi all'attuale lista non rende affidabile la condivisione.


## Aggiornamento implementativo — 24 settembre 2026

Decisione dell'utente: una sola classe di esposizione. Materiali conserva la classe principale; negli archivi precedenti le esposizioni multiple sono archiviate in `esposizioni_precedenti` e segnalate. Il valore unico si collega a tutte le famiglie SLE della verifica CA e al palo orizzontale.

Implementati: propagazione dei soli campi modificati con anteprima; gruppo atomico per identità dell'acciaio e cambio forma; copriferro adottato CA↔PO; confronto con il nominale minimo del motore Materiali, riferito alla barra dichiarata nella scheda Materiali, con stato non verificabile in caso di esposizione/CLS incoerenti o input incompleti. Questo avviso non costituisce la verifica di ogni barra/superficie né una verifica al fuoco.

Separati diametro del palo CA e perforazione del micropalo. Collegati CHS da catalogo MV↔MO, falda PV/PO/MO, verticali indagate e dettagli delle staffe tra verifiche CA. Le lunghezze dei micropali inclinati non si propagano verso il modulo orizzontale. Forme incompatibili, armature avanzate e profili manuali non rappresentabili sono segnalati nel confronto.

Stratigrafie PV/PO/MO: collegamento esplicito da Confronto, con anteprima e conferma dello stesso sondaggio e origine delle quote. Su destinatari compilati sono richiesti numero di sondaggi, strati e spessori corrispondenti; vengono preservati i parametri esclusivi. ID persistenti per strato; aggiornamenti successivi per proprietà, mai per solo indice. Cambiare suddivisione/ordine sospende la condivisione fino a un nuovo collegamento. Le categorie Bustamante restano locali.

L'ambito resta il gruppo immediato del progetto: sottosezioni indipendenti. Il registro storico completo, l'annullamento delle sincronizzazioni e un collegamento tra gruppi diversi sono sviluppi ulteriori; non sono introdotti da questa revisione.


## Confronto indipendente dall'ordine dei fogli
Il confronto usa ora `ComparableFields`, con criteri simmetrici separati dalla compatibilità di trasferimento (`Common` / `Apply`). L'esposizione non compilata e il profilo CHS mancante vengono rilevati indipendentemente da quale foglio viene prima, senza rendere tali valori trasferibili a selettori che non li ammettono. Le coppie e gli avvisi sono ordinati per identità stabile; la posizione nell'albero resta una scelta di visualizzazione. Controllati tutti i 24 ordini di Materiali, CA, palo verticale e palo orizzontale, le coppie tra i sei moduli, la conservazione degli input e l'uniformazione in entrambi gli ordini. Verificati anche Test 1 e Test 3, in sola lettura sugli originali.
