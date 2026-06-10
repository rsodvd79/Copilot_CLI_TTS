# Custom Instructions (Copilot CLI)

Incolla questo blocco nelle tue custom instructions di Copilot CLI:

```text
## Webservice response forwarding toggle

Repository command:
- .\copilot-ws.cmd on
- .\copilot-ws.cmd off
- .\copilot-ws.cmd status

Persistent state file:
- .copilot/webservice-forwarding.state

Behavior:
1. Before sending a user-facing response, read `.copilot/webservice-forwarding.state`.
2. If value is `ON`, send assistant output in progressive chunks (1-2 sentences each), as soon as each chunk is ready:
   .\copilot-ws-send.cmd "<chunk text>"
3. For very short replies, a single chunk is fine.
4. If value is `OFF` (or missing), do not send response text to the webservice.
5. Never send the user prompt; send only the assistant response text.
6. Send only the text content, without any markdown formatting, to the webservice.
7. Send all chunks in order, as they are generated, to allow for streaming TTS playback on the client side.
```
