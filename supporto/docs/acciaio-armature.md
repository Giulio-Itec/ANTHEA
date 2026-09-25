# Scheda acciaio per armature

Modulo `mat_acciaio_armatura`, integrato in Materiali e nei progetti. Le proprietà sono nel nodo `input`; `riferimento` rimane una nota locale della scheda. Gli input incompleti si possono salvare; i risultati e il diagramma vengono sospesi finché i valori non sono validi.

- B450C (predefinito) e B450A: stesso catalogo DLL impiegato dalla verifica in c.a. Un avviso ricorda le limitazioni di impiego del B450A. Riferimenti: [CSLP, acciai](https://cslp.mit.gov.it/acciai), NTC 2018 §§ 7.4.2.2 e 11.3.2.1.
- FeB22k, FeB32k, FeB38k, FeB44k: valori minimi nominali dei prospetti 1-I e 2-I del [D.M. 09/01/1996, parte I, sezione I](https://ordingegneri.it/wp-content/uploads/sites/109/2024/06/DM-090196.pdf), pagina PDF 12. Es iniziale assunto pari a 200000 MPa; per cambiarlo si passa a Personalizzato. I valori non sostituiscono quelli derivanti da prove per strutture esistenti.
- L'allungamento A5 è una descrizione del catalogo storico: non viene convertito in εu. Quest'ultimo va inserito prima di usare il materiale completo nelle verifiche che lo richiedono. Gli acciai storici sono trasferiti alla verifica come materiali personalizzati, mantenendo il nome storico.
- Personalizzato: nome, Es, fyk, fu, εu, diagramma editabili. Valori in MPa e deformazioni in per mille. `gamma_s` è un parametro di calcolo editabile (valore iniziale 1,15); fyd = fyk / gamma_s, εyd = fyd / Es.

I campi condivisi sono nome/classe, Es, fyk, fu, εu, diagramma e gamma_s. Geometria, barre, carichi e materiali CHS restano separati. La scelta «solo questo foglio» mantiene un conflitto visibile. I dati assenti nei vecchi fogli seguono i valori impliciti del motore preesistente senza modificare i file durante il confronto.

Il diagramma mostra il legame **caratteristico** dell'acciaio, non una verifica di duttilità o di ammissibilità normativa. Il calcolo del palo orizzontale conserva il proprio modello elastoplastico: con materiale incrudente viene esposto un avviso, perché l'incrudimento non è usato da quel motore.

Verifica: `ANTHEA.exe --smoke-steel <cartella>` esercita interfaccia, catalogo, unità, errori, persistenza e condivisione; `--smoke-materials`, `--smoke-sharing` e `--smoke-projects` coprono le integrazioni preesistenti.
