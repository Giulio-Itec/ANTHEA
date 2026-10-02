# Aggiornamento librerie · 24 settembre 2026

Importate le DLL dai bin/Release dei singoli progetti indicati. Percorsi, versioni assembly e SHA-256 sono registrati in lib/Checker/manifest.json. Le versioni assembly sono rimaste invariate; sono cambiati i binari di Model, ModelData, Geometry, DelaunayMesh, Utilities e Checker.Concrete. GMsh.Net e UnsafeEx sono invariati.

## Verifiche

- Compilazione e pubblicazione desktop completate; hash delle DLL pubblicate coincidenti con il manifest.
- 294 controlli Checker/Excel/estensioni CA e 52 controlli del modulo ampliato superati.
- 71 controlli di interfaccia superati.
- Regressione storica: 414 casi e 2550 controlli software superati; 50 casi falliti. Ripetendo lo stesso eseguibile con le DLL precedenti si ottengono gli stessi 50 fallimenti, con messaggi e valori identici. La suite storica non è quindi interamente superata, ma non emergono nuovi fallimenti dovuti alle DLL.

## Prestazioni

Stessi casi e parametri, circolari a 32 lati. Mediana di tre esecuzioni dopo il primo utilizzo. Misure locali indicative, senza prove ANTHEA concorrenti; attività esterne al processo e variabilità della macchina possono influenzare i tempi. Nessuna differenza nei checksum diagnostici dei 96 campioni; questo confronto non sostituisce la validazione numerica.

| Sezione | Operazione | Prima, ms | Dopo, ms | Prima/dopo |
|---|---|---:|---:|---:|
| rettangolare | preparazione | 48.74 | 8.13 | 6 |
| rettangolare | dominio_3d_32 | 32.62 | 38.68 | 0.84 |
| rettangolare | 24_verifiche | 18.11 | 27.05 | 0.67 |
| rettangolare | 24_tensioni_non_lineari | 39.79 | 51.84 | 0.77 |
| rettangolare | 24_tensioni_lineari_fessure | 54.56 | 32.46 | 1.68 |
| rettangolare | curva_30_passi | 79.79 | 114.19 | 0.7 |
| rettangolare_cava | preparazione | 50.43 | 2.82 | 17.89 |
| rettangolare_cava | dominio_3d_32 | 31.83 | 70.62 | 0.45 |
| rettangolare_cava | 24_verifiche | 16.05 | 27.85 | 0.58 |
| rettangolare_cava | 24_tensioni_non_lineari | 39.27 | 45.59 | 0.86 |
| rettangolare_cava | 24_tensioni_lineari_fessure | 55.38 | 27.11 | 2.04 |
| rettangolare_cava | curva_30_passi | 84.83 | 103.03 | 0.82 |
| circolare | preparazione | 61.68 | 3.35 | 18.4 |
| circolare | dominio_3d_32 | 25.92 | 55.62 | 0.47 |
| circolare | 24_verifiche | 24.58 | 44.69 | 0.55 |
| circolare | 24_tensioni_non_lineari | 55.29 | 55.39 | 1 |
| circolare | 24_tensioni_lineari_fessure | 65.94 | 30.5 | 2.16 |
| circolare | curva_30_passi | 112.98 | 143.36 | 0.79 |
| circolare_cava | preparazione | 151.29 | 3.23 | 46.81 |
| circolare_cava | dominio_3d_32 | 78.89 | 33.78 | 2.34 |
| circolare_cava | 24_verifiche | 65.72 | 33.65 | 1.95 |
| circolare_cava | 24_tensioni_non_lineari | 96.2 | 39.77 | 2.42 |
| circolare_cava | 24_tensioni_lineari_fessure | 134.89 | 37.89 | 3.56 |
| circolare_cava | curva_30_passi | 228.15 | 123.23 | 1.85 |

La preparazione risulta più veloce in tutti i casi; alcune operazioni su sezioni rettangolari e circolari piene risultano più lente. Non si conclude un miglioramento uniforme.

## Artefatti locali

- supporto/tmp/ca_extensions/dll_update_before/benchmark.json
- supporto/tmp/ca_extensions/dll_update_after_isolated/benchmark.json
- supporto/tmp/ca_extensions/dll_update_comparison.json
- supporto/tmp/ca_extensions/dll_previous_regression.log
- supporto/tmp/ca_extensions/dll_update_regression.log
- supporto/tmp/ca_extensions/dll_update_ui/esito.txt
- supporto/tmp/ca_extensions/dll_previous: copia delle DLL precedenti e relativo manifest.

## Secondo aggiornamento · build delle 16:10

Importate le nuove versioni dai medesimi bin/Release: Model 1.1.0.3, Checker.Concrete 0.0.12.2, Geometry 2.0.1.10, Utilities 2.0.0.6 e DelaunayMesh 2.0.0.4. Aggiornata anche ModelData, ricompilata con versione invariata 0.0.1.10. Manifest e applicazione pubblicata aggiornati; hash verificati.

Superati 346 controlli CA e 71 controlli di interfaccia. La regressione completa restituisce gli stessi 50 fallimenti storici, con valori identici al precedente aggiornamento. Report: `supporto/tmp/ca_extensions/dll_1610_regression.json`; prova UI: `supporto/tmp/ca_extensions/dll_1610_ui/esito.txt`. I benchmark sopra si riferiscono al primo aggiornamento, non a questa build.
