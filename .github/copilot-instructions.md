# Copilot Instructions

## Build, test, and run

```bash
dotnet build TtsWebService.csproj
dotnet run --project TtsWebService.csproj
dotnet test
dotnet test --filter FullyQualifiedName~<TestName>
dotnet build client/TtsClient.csproj
dotnet run --project client/TtsClient.csproj -- --url http://localhost:5000 "ciao mondo"
```

## High-level architecture

- **ASP.NET Core minimal API** exposes a single GET endpoint: `/say?text=...`.
- **SpeechQueue** stores incoming strings in memory so requests return immediately.
- **SpeechWorker** runs in the background, drains the queue, and feeds each string to a long-lived Piper process.
- **Piper** is invoked as an external executable (resolved from `piper/piper.exe`) and runs persistently with `--output_raw`, so the voice model is loaded once and audio streams to stdout.
- **NAudio** plays Piper's raw 16-bit mono PCM through a `BufferedWaveProvider` + `WaveOutEvent`; the sample rate is read from the voice config (`*.onnx.json`, default 22050).
- Piper runs with its working directory set to the executable folder so it can load its DLLs and runtime.
- The default voice model is selected by priority: `it_IT-paola-high.onnx`, then `it_IT-paola-medium.onnx`, then `it_IT-riccardo-x_low.onnx` (only when each has its companion `.json`).
- Each Piper voice needs its companion config file next to the model (e.g. `it_IT-paola-medium.onnx.json`); without it Piper crashes with `0xC0000409`.
- **CLI client** sends a GET request to `/say` and is meant to stay tiny and disposable.

## Key conventions

- The GET endpoint must stay non-blocking; it returns `204 No Content` after enqueueing text.
- The `text` query parameter is required and should be validated before enqueueing.
- Speech execution happens off the request thread; do not call TTS directly from the endpoint.
- Keep the queue and worker in-process and lightweight; this service is intentionally small.
- Configure Piper through `Piper:ExecutablePath`, `Piper:ModelPath`, and optional `Piper:Speaker`.
- Keep Italian model files together with matching `.json` and rely on the built-in priority order for automatic fallback.
- Keep speech execution off the request thread; the endpoint must stay non-blocking and return `204 No Content`.
- Keep Piper as a single persistent process and stream its raw audio; do not spawn a new process per utterance (avoids reloading the model).
- Keep the CLI client stateless; it should only validate input, call the service, and print the HTTP status code.

## Copilot CLI command for response forwarding

Use the repository command below to control whether Copilot should send its own response text to the webservice:

```bash
.\copilot-ws.cmd on
.\copilot-ws.cmd off
.\copilot-ws.cmd status
```

- State file: `.copilot/webservice-forwarding.state`
- If the file value is `ON`, Copilot should send each assistant response text through:
  `dotnet run --project client/TtsClient.csproj -- "<assistant response text>"`
- If the value is `OFF` (or file missing), Copilot should not forward response text.

## Custom instructions source

- Use `CUSTOM_INSTRUCTIONS.md` as the canonical copy/paste block for Copilot CLI custom instructions.
