# Decisioni

Una riga per decisione: data, scelta, motivo.

- 2026-11-12: test con xUnit (pacchetti `xunit`, `xunit.runner.visualstudio`, non Microsoft): eccezione alla regola "solo pacchetti Microsoft" perché CLAUDE.md chiede esplicitamente xUnit; usati solo nel progetto di test.
- 2026-11-12: App WPF senza file XAML (UI costruita in C#), con `Program.Main` come avvio: meno caricamenti all'avvio e niente logica nel code-behind.
- 2026-11-12: `TreatWarningsAsErrors` attivo in tutta la solution per garantire "zero warning".
- 2026-11-12: oltre a `.lnk` lo stack accetta anche `.url` (collegamenti Steam e simili): stesso avvio via shell.
- 2026-11-12: prefisso d'ordine riconosciuto solo con 1-3 cifre, separatore (`-`, `.`, `)`) e spazio: così "7-Zip" e "3D Builder" restano intatti.
- 2026-11-12: AppUserModelID = `FanStack.Stack.<NomeSoloAscii>.<hash8 del percorso>`, calcolato dal percorso della cartella sia nel `.lnk` sia nel processo: due cartelle con lo stesso nome restano distinte.
- 2026-11-12: con taskbar a sinistra/destra il ventaglio è una colonna ad arco centrata sul cursore, con etichette verso l'interno (le etichette in orizzontale si sovrapporrebbero).
- 2026-11-12: vicino al bordo sinistro dello schermo il ventaglio si specchia (etichette a destra) invece di spostarsi lontano dall'icona.
- 2026-11-12: avvio dell'app aprendo il `.lnk` stesso con la shell (`UseShellExecute`), dopo aver letto il bersaglio per segnalare i collegamenti rotti: funziona anche con app dello Store e collegamenti "pubblicizzati".
- 2026-11-12: finestra grande quanto il ventaglio (non tutto lo schermo): con finestre trasparenti WPF il costo del rendering cresce con l'area.
- 2026-11-12: rendering WPF in modalità software per il ventaglio: −120 ms all'avvio e fluidità uguale (misurata) all'hardware su finestre trasparenti.
- 2026-11-12: secondo clic sull'icona = chiusura: un mutex per stack; il nuovo processo segnala a quello aperto di chiudersi ed esce. Dopo una chiusura per clic fuori il processo resta vivo 450 ms per riconoscere il clic sulla taskbar.
- 2026-11-12: oltre a "Deactivated", un controllo ogni 40 ms dei pulsanti del mouse chiude il ventaglio se si clicca fuori: serve se Windows non concede il primo piano alla finestra.
- 2026-11-12: le bitmap della shell arrivano a volte con alfa premoltiplicato e a volte no: rilevamento automatico (un canale colore maggiore dell'alfa = non premoltiplicato).
- 2026-11-12: icona dello stack sovrascritta nello stesso percorso e `SHChangeNotify(SHCNE_ASSOCCHANGED)` per aggiornare Explorer, invece di toccare il collegamento fissato in `User Pinned` (fuori dalle cartelle consentite).
- 2026-11-12: l'icona dello stack si rigenera da sola alla chiusura del ventaglio se le prime 4 app sono cambiate (firma in un file `.sig`).
- 2026-11-12: variabile `FANSTACK_SIMULATE_EDGE` solo per le prove: Windows 11 non permette di spostare la taskbar.
- 2026-11-12: `--quiet` per `--create`/`--refresh-icons`, utile per script e prove automatiche.
- 2026-11-12: se sul Desktop esiste già un file con il nome dello stack che non è nostro, il collegamento si chiama "Nome (FanStack).lnk": nessun file dell'utente viene sovrascritto.
