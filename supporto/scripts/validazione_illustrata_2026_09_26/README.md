# Revisione illustrata della validazione CA e ponti

`build.py` deriva la Rev03 dalla Rev02, conserva gli stili ITEC e i risultati
numerici, aggiunge formule native Word da LaTeX, didascalie e rimandi interni.
`equations.py` contiene le formule generali e la conversione MathML → OMML
attraverso il foglio XSLT di Microsoft Office.

Gli output della lavorazione sono in
`supporto/artefatti/validazione_illustrata_2026_09_26/`.
`prepare_captures.py` genera gli ingressi delle 114 viste.
Il progetto `supporto/test/ValidationIllustrations` compila i controlli WPF
di produzione in un eseguibile di supporto e acquisisce le viste mediante
il metodo Snapshot del prodotto. Riceve due argomenti: JSON degli ingressi
e cartella di destinazione. Nessun risultato numerico viene iniettato nelle
schermate. Le didascalie dichiarano i casi solo illustrativi e le differenze
fra funzione scalare e interfaccia.

Sequenza di generazione del documento:

1. Eseguire `build.py` con il Python delle dipendenze Codex e `latex2mathml`.
2. Eseguire `render_word.ps1` con Microsoft Word disponibile: aggiorna campi
   e indice, salva `word_updated.docx` ed esporta `final.pdf`.
3. Attendere la conclusione di Word, poi eseguire `qa_pdf.py` e
   `render_pages.py`; ispezionare pagine e schermate.
4. Eseguire `finalize.py` per trasferire nel DOCX le cache dei campi,
   mantenendo le parti opache del modello e i riferimenti alle immagini.
5. Eseguire `render_word.ps1 -VerifyFinal` e `verify_final.py`.

`equations.tex`, `equations.json`, `authoring-audit.json` e i manifest delle
acquisizioni rendono ripercorribile la revisione. I PDF e le immagini delle
pagine sono artefatti di controllo, non documenti aggiuntivi da consegnare.
I valori dei test numerici rimangono quelli delle campagne indicate nella Rev02;
le acquisizioni dell'interfaccia sono datate 26 settembre 2026.
