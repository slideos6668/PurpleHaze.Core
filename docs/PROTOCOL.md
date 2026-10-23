# Protocol Specification — Milestone 3

## Transport

- **Primary:** Windows named pipes / Unix domain sockets via `System.IO.Pipes`
- **Framing:** 4-byte big-endian length prefix + UTF-8 JSON body
- **Max message size:** 1 MiB (`ProtocolConstants.MaxMessageSizeBytes`)
- **Default pipe name:** `PurpleHaze.Bridge`

## Authentication

Local pairing token (hex string) shared out-of-band (config file / manager UI).

1. Client connects to the pipe.
2. Client sends `handshake` with `pairingToken` and supported protocol versions.
3. Server validates token (fixed-time compare) and version.
4. Server replies `handshake.ack` with `accepted`, `sessionId`, heartbeat interval.
5. Rejected handshakes close the session.

**The bridge never evaluates received strings as code, shell, or unrestricted game commands.**

## Envelope

```json
{
  "protocolVersion": 1,
  "messageType": "action.execute",
  "requestId": "a1b2c3d4e5f6...",
  "pluginId": "example.plugin",
  "timestamp": "2026-10-22T05:00:00Z",
  "payload": { }
}
```

| Field | Required | Notes |
|-------|----------|-------|
| `protocolVersion` | yes | Must be `1` |
| `messageType` | yes | Known type only (see below) |
| `requestId` | for requests/responses | UUID/hex; responses echo the same id |
| `pluginId` | when plugin-scoped | Lowercase dotted id |
| `timestamp` | yes | UTC |
| `payload` | depends | Strongly typed per message type |

## Message types

| Type | Direction | Correlation |
|------|-----------|-------------|
| `handshake` | Client → Server | request |
| `handshake.ack` | Server → Client | response |
| `heartbeat` | Either | notification |
| `capability.register` | Plugin/Host → Manager | notification |
| `capability.update` | Plugin/Host → Manager | notification |
| `capability.unregister` | Plugin/Host → Manager | notification |
| `action.execute` | Manager → Host | request |
| `action.result` | Host → Manager | response |
| `status.request` / `status.response` | Either | request/response |
| `log` | Host → Manager | notification |
| `event` | Host → Manager | notification |
| `plugin.state` | Host → Manager | notification |
| `error` | Either | response or notification |

Unknown `messageType` values are **rejected**.

## Example: handshake

**Request**

```json
{
  "protocolVersion": 1,
  "messageType": "handshake",
  "requestId": "req1",
  "payload": {
    "clientName": "PurpleHaze.Manager",
    "clientVersion": "0.1.0",
    "pairingToken": "a1b2c3...",
    "supportedProtocolVersions": [1]
  }
}
```

**Ack**

```json
{
  "protocolVersion": 1,
  "messageType": "handshake.ack",
  "requestId": "req1",
  "payload": {
    "accepted": true,
    "negotiatedProtocolVersion": 1,
    "serverName": "PurpleHaze.Bridge",
    "serverVersion": "0.1.0",
    "heartbeatIntervalMs": 5000,
    "sessionId": "..."
  }
}
```

## Example: action execute

**Request**

```json
{
  "protocolVersion": 1,
  "messageType": "action.execute",
  "requestId": "req2",
  "pluginId": "example.plugin",
  "payload": {
    "capabilityId": "example.toggle.feature",
    "argument": true
  }
}
```

**Result**

```json
{
  "protocolVersion": 1,
  "messageType": "action.result",
  "requestId": "req2",
  "pluginId": "example.plugin",
  "payload": {
    "success": true,
    "result": { "value": true }
  }
}
```

## Limits

| Limit | Value |
|-------|-------|
| Max message size | 1 MiB |
| Default request timeout | 10 s |
| Heartbeat interval | 5 s |
| Heartbeat timeout | 15 s |
| Max in-flight requests | 64 |
| Max messages/second | 200 |

## Security notes

- Pairing token required before any application traffic.
- Envelope and selected payloads are validated before dispatch.
- Rate limiting drops excess messages.
- Malformed frames cause a controlled disconnect, not a host crash.
- Plugins in-process can still access process memory; the bridge does not claim full isolation.
