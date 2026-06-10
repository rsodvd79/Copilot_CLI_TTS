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
2. If value is `ON`, send the exact assistant response text to the webservice with:
   dotnet run --project client/TtsClient.csproj -- "<assistant response text>"
3. If value is `OFF` (or missing), do not send response text to the webservice.
4. Never send the user prompt; send only the assistant response text.
```

