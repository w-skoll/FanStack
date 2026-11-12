# PROGRESS

Ultimo aggiornamento: 2026-11-12.

## Stato milestone

- [x] M0, Scheletro
- [x] M1, Lettura dello stack (Core) + test
- [x] M2, Geometria del ventaglio (Core) + test (N da 1 a 30, 4 bordi, angoli, secondo monitor con coordinate negative)
- [x] M3, Icone (IShellItemImageFactory, cache PNG con invalidazione, icona di riserva) e avvio con gestione errori
- [x] M4, Finestra del ventaglio (trasparente, topmost, fuori da Alt+Tab, PerMonitorV2, animazione, pillole, hover, tastiera, chiusura)
- [x] M5, `--create`, icona `.ico` con anteprima, collegamento con AppUserModelID, finestra di aiuto
- [x] M6, DPI/cambio monitor, tema chiaro/scuro, log con rotazione, `--refresh-icons`, README, pubblicazione in `dist/`

## Definition of done

- [x] `dotnet build` e `dotnet test` verdi, zero warning (171 test).
- [x] `--create "Ufficio"` crea cartella, icona e collegamento funzionanti (verificato a runtime).
- [x] Il collegamento fissato sulla taskbar apre il ventaglio con animazione (confermato dall'utente).
- [x] Clic su app la avvia; clic fuori o Esc chiudono (verificato a runtime).
- [x] Due stack fissati restano icone separate (AppUserModelID verificati nei `.lnk`, confermato dall'utente sulla taskbar).
- [~] Lato giusto per i 4 bordi: bordo basso reale verificato; alto/sinistra/destra verificati con taskbar simulata; geometria testata per tutti i bordi e secondo monitor. **Taskbar vera su altri bordi e secondo monitor: da provare a mano**.
- [x] README in italiano con elenco verificato / da provare.

## Misure

- Tempo di comparsa (dall'avvio del processo al primo frame), build Release ReadyToRun: **circa 450-650 ms** su questa macchina. Obiettivo 300 ms **non raggiunto**: circa 110 ms sono l'avvio del runtime .NET, circa 300 ms l'inizializzazione di WPF (finestra + primo rendering). Già fatto: ReadyToRun, rendering software (−120 ms), icone caricate in background dopo la comparsa. Per scendere sotto i 300 ms servirebbe un processo sempre attivo, vietato dalle regole.
- Animazione di apertura: circa 50-55 frame al secondo.

## Da provare a mano su Windows

1. Tasto destro su `Ufficio` sul Desktop > Mostra altre opzioni > Aggiungi alla barra delle applicazioni. Stessa cosa per `Giochi`.
2. Controllare che sulla taskbar compaiano **due icone distinte** con l'anteprima delle app.
3. Clic su ciascuna: si apre il ventaglio giusto sopra l'icona. Secondo clic sulla stessa icona: si chiude.
4. Aggiungere/togliere un collegamento in `%UserProfile%\FanStacks\Ufficio`, aprire e chiudere il ventaglio: l'icona sulla taskbar si aggiorna (se no: `FanStack.exe --refresh-icons`).
5. Se disponibile: secondo monitor (anche con scala diversa), taskbar nascosta automaticamente, tema chiaro.

## Prova dell'utente (2026-11-12)

- Stack fissati sulla taskbar: funzionano, icone separate. Confermato dall'utente.

## Pubblicazione

- Repository pubblico su GitHub, licenza MIT, release con `FanStack.exe`.
- Screenshot e GIF in `docs/screens/` presi su uno sfondo neutro generato, con stack demo di app di sistema.
- `CLAUDE.md` resta solo in locale (escluso dal repository).

## Come provare la build

```
dotnet build
dotnet test
dotnet publish src/FanStack.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true -o dist
dist\FanStack.exe
```

Per provare gli altri bordi senza spostare la taskbar: `set FANSTACK_SIMULATE_EDGE=Left` (o Top, Right) e poi `dist\FanStack.exe --stack "%UserProfile%\FanStacks\Ufficio"` con il mouse vicino a quel bordo.

## Blocchi

- Fissare sulla taskbar da codice non è possibile (limite di Windows): passo manuale, documentato.
- Spostare la taskbar su Windows 11 non è possibile: bordi alto/sinistra/destra provati con simulazione.
