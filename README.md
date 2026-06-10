# TTS Web Service (Piper + .NET)

Piccolo webservice ASP.NET Core che riceve testo via HTTP e lo riproduce in voce usando Piper.

## Requisiti

- Windows
- .NET SDK 8+
- Cartella `piper/` nella root con almeno:
  - `piper.exe`
  - un modello voce + relativo `.json` (es. `it_IT-paola-medium.onnx` e `it_IT-paola-medium.onnx.json`)
  - DLL/runtime inclusi con Piper

## Avvio rapido

```bash
dotnet build TtsWebService.csproj
dotnet run --project TtsWebService.csproj
```

Invio testo:

```bash
dotnet run --project client/TtsClient.csproj -- --url http://localhost:5000 "ciao mondo"
```

Monitor appunti (invia automaticamente il testo copiato):

```bash
dotnet run --project client/clipboard-monitor/ClipboardMonitor.csproj -- --url http://localhost:5000 --interval-ms 500
```

## API

- `GET /say?text=...`
  - valida `text`
  - accoda il testo
  - risponde subito `204 No Content` (non bloccante)

## Architettura

- `SpeechQueue`: coda in memoria delle richieste
- `SpeechWorker`: worker in background che drena la coda
- Piper persistente (`--output_raw`): modello caricato una sola volta
- Playback streaming con NAudio (`BufferedWaveProvider` + `WaveOutEvent`)

## Configurazione Piper

Opzionale via config:

- `Piper:ExecutablePath`
- `Piper:ModelPath`
- `Piper:Speaker`

Default usati:

- eseguibile: `piper/piper.exe` (se presente)
- modello (ordine di preferenza): `it_IT-paola-high.onnx`, `it_IT-paola-medium.onnx`, `it_IT-riccardo-x_low.onnx` (solo se presente anche il rispettivo `.json`)

## Comandi utili

```bash
dotnet test
dotnet test --filter FullyQualifiedName~<TestName>
dotnet build client/TtsClient.csproj
dotnet build client/clipboard-monitor/ClipboardMonitor.csproj
```

## Comando Copilot CLI: toggle invio risposte al webservice

Usa questo comando dal root del repository:

```bash
.\copilot-ws.cmd on
.\copilot-ws.cmd off
.\copilot-ws.cmd status
```

- Stato persistente nel file: `.copilot/webservice-forwarding.state`
- `ON`: Copilot deve inoltrare il testo della propria risposta al webservice
- `OFF`: nessun inoltro automatico

### Invio progressivo a chunk

Per migliorare l'esperienza TTS nelle risposte lunghe:

```bash
.\copilot-ws-send.cmd "<testo risposta o chunk>"
```

- invia il testo ricevuto come singolo chunk
- rimuove la formattazione Markdown prima dell'invio
- il controllo `ON/OFF` va fatto a monte con `.\copilot-ws.cmd status`

## Custom instructions per Copilot CLI

- File pronto all'uso: `CUSTOM_INSTRUCTIONS.md`
- Contiene un blocco copy/paste per far rispettare il toggle ON/OFF durante le risposte.
