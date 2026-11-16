# Guide Rev72 della candidata completa 1.19.1

Compito 208, 16 novembre 2026. Base guide `84621b6f28bef76a24150a70f1083620975d70c5`; avviso P10 e correzione degli strati integrati dai commit 84c99d93, 48cd276f e 83e68e0a. Questa preparazione non sceglie la candidata da pubblicare e non aggiorna il numero 1.19.1.

## Contenuto verificato

- Pratica, Comprendere i dati comuni: selezione di tutte le schede Materiali del primo livello; avviso sui Materiali più vicini con minimo o esito diverso, senza cambiare l'esito principale. Integrato il testo pratico del coordinatore, eliminando il duplicato della selezione già inserito nel 207 e conservando il limite di RISPETTATO e il caso non verificabile.
- Teoria, Copriferro minimo e nominale: avviso condizionato a minimo/esito diverso, confronto del minimo a 0,01 mm, senza estendere l'esito ai requisiti locali.
- Pratica: chiarita la protezione della conferma quando cambia il numero di strati/sondaggi e delle successive modifiche verso fogli con diverso numero di strati. L'assenza di conflitti visibili non prova l'uguaglianza della suddivisione.
- Nessuna nuova formula, figura o prescrizione. I limiti dei riordini a numero invariato e dei comandi espliciti sono documentati nelle note di rilascio della candidata.

Fonti del comportamento: `ProjectValidation.CoverLevels/CoverChecks/Same`, `ProjectCoherenceCache`, `ProjectSharedData.ExceptChangedSoilSubdivisions/OtherSoilSubdivision` e `SharedDataCoordinator.ConfirmSharedChanges`; sorgenti e SHA nel resoconto esterno 208.

## Documenti e controlli

Pipeline invariata Build → aggiornamento campi Word → Finalize → VerifyFinal. Pratica: 140 pagine, 1409 frammenti, 9 formule; teorica: 214 pagine, 2557 frammenti, 1579 formule. Ciascuna guida conserva cinque sezioni e 26 parti del modello ITEC identiche. SHA del modello: f6f3f04b0bafeea09e4cca1b19fa74e95fc540c264aa518f3d93dee9eebbcd92.

Le 44 pagine cambiate sono state esaminate singolarmente a 96 dpi; le altre 310 immagini sono identiche al 207. Nessun taglio o sovrapposizione rilevato. Articolo Rev01 (quattro file), 16 originali Rev71 archiviati e 1801 file protetti invariati. Le sei immagini non versionate necessarie alla pipeline sono ripristinate dai corrispondenti asset Wiki, byte per byte.

Sette build e sei suite mirate PASS; anche la corsa UI mirata è conclusa. I dodici confronti F4 danno sei PASS e sei FAIL storici: i sei oggetti di confronto coincidono con il 191. Nessun atteso aggiornato.

F3: 1086 differenze non ammesse, tutte Wiki; 629 voci proposte e 457 precedenti superate. Proposta non applicata in `chatgpt-compiti/evidenze-208/wiki-rev72-per-ok.md`, con valori esatti prima/dopo nei JSON associati. Il registro congelato resta invariato e serve l'OK dell'utente.

Il resoconto conclusivo, gli SHA e la matrice delle prove della candidata sono in `chatgpt-compiti/candidata-1191-completa-208.md`. Nessun push, tag, pubblicazione o prova nativa.
