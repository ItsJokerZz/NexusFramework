# Cheat Manager Shortcut

## Overview

The cheat manager shortcut provides an etaHEN-style in-game cheat management
experience. When configured, you can press a controller button combo (or a
keyboard shortcut) to open an overlay that lets you browse and toggle cheats
for the currently running game — all without leaving the game process.

## How It Works

```
┌──────────┐     ┌──────────────────┐     ┌──────────────────┐
│  User    │     │  NCF WebUI       │     │  PS4/PS5 Console │
│  (PC)    │     │  (Host PC)       │     │  (Payload)       │
└────┬─────┘     └────────┬─────────┘     └────────┬─────────┘
     │                    │                        │
     │  ── Three open paths ──                     │
     │                    │                        │
     │ 1. Ctrl+Shift+C    │                        │
     │ ──────────────────>│                        │
     │                    │  POST /api/cheat-      │
     │                    │  manager/open          │
     │                    │                        │
     │ 2. POST /api/      │                        │
     │    cheat-manager/  │                        │
     │    open (HTTP)     │                        │
     │ ──────────────────>│                        │
     │                    │                        │
     │ 3. ?autoOpen=1     │                        │
     │    (browser URL)   │                        │
     │ ──────────────────>│                        │
     │                    │                        │
     │ 4. Controller      │                        │
     │    shortcut        │                        │
     │    (future: when   │                        │
     │     /pad_state     │                        │
     │     is available)  │                        │
     │                    │  GET /api/process      │
     │                    │ ──────────────────────>│
     │                    │ <──────────────────────│
     │                    │  { titleId, name, ... }│
     │                    │                        │
     │                    │  GET /api/cheats       │
     │                    │ ──────────────────────>│
     │                    │ <──────────────────────│
     │                    │  [ cheat list ]        │
     │                    │                        │
     │  ── Overlay shown ──                        │
     │                    │                        │
     │  Toggle cheat      │  POST /api/enable      │
     │ ──────────────────>│ ──────────────────────>│
     │                    │ <──────────────────────│
     │                    │  { success: true }     │
     │                    │                        │
     │  Close (Esc)       │  POST /api/cheat-      │
     │ ──────────────────>│  manager/close         │
     │                    │                        │
```

## Trigger Options

| Trigger | Enum Value | Description |
|---------|-----------|-------------|
| Off | `Off` | No controller trigger. Use WebUI hotkey or HTTP only. |
| L1+R1+Square | `HoldL1R1Square` | Hold all three buttons simultaneously (default). |
| L1+R1+Triangle | `HoldL1R1Triangle` | Hold all three buttons simultaneously. |
| R3+L3 | `HoldR3L3` | Hold both thumbsticks. |
| L2+Triangle | `HoldL2Triangle` | Hold L2 and Triangle. |
| Long Hold Options | `LongHoldOptions` | Hold Options button for 2 seconds. |
| Long Hold Share | `LongHoldShare` | Hold Share button for 2 seconds. |
| Custom | `Custom` | User-defined chord (see "Recording a Custom Chord"). |

## Three Fallback Open Paths

Because the payload's `/pad_state` endpoint returns `null` on stock payloads,
controller-only opening will not work out of the box. Three reliable fallbacks
are provided:

### 1. WebUI Keyboard Shortcut (Default: Ctrl+Shift+C)

In the browser-based WebUI, press **Ctrl+Shift+C** to open the cheat manager
overlay. The hotkey is configurable in the overlay's "Keyboard shortcut" field
and persisted in `localStorage`.

### 2. HTTP Trigger

Send a POST request to the WebUI:

```bash
curl -X POST http://<host-pc-ip>:9080/api/cheat-manager/open
curl -X POST http://<host-pc-ip>:9080/api/cheat-manager/close
```

This can be bound to a Stream Deck, AutoHotkey script, phone shortcut, or any
other macro tool.

### 3. etaHEN Toolbox XML Entry

Place `menu/etaHEN_xml/cheat_manager.xml` on the console at
`/data/etaHEN/toolbox/cheat_manager.xml`. Edit the file to replace
`<host-pc-ip>` and `<port>` with your PC's actual IP and port.

When you click the entry in etaHEN's Toolbox, it opens the PS4/PS5 browser to
`http://<host-pc-ip>:<port>/?autoOpen=1`, which immediately shows the cheat
manager overlay.

## Recording a Custom Chord

If your payload supports `/pad_state`:

### Via CLI

```bash
ncf shortcut record --ip <console-ip> --port 9080
```

This polls the controller for 5 seconds. Hold the desired button combo during
that window. The chord is saved and set as the open trigger.

### Via WebUI

Click the "Record shortcut" button in the WebUI shortcut config panel (future
feature — currently available via the API).

### Via API

```bash
curl -X POST http://<host-pc-ip>:9080/api/shortcut-config/record \
  -H "Content-Type: application/json" \
  -d '{"windowMs": 5000}'
```

## Configuration

The shortcut config is persisted as JSON at `<cheat-database-root>/shortcut-config.json`.

### Default Configuration

```json
{
  "schemaVersion": 1,
  "enabled": true,
  "mode": "Off",
  "holdDurationMs": 750,
  "debounceMs": 1000,
  "cheatManagerTrigger": "HoldL1R1Square",
  "closeTrigger": "HoldL1R1Square",
  "customOpenChord": [],
  "customChordHoldMs": 200
}
```

### Via CLI

```bash
# Show current config
ncf shortcut show --ip <console-ip> --port 9080

# Set trigger
ncf shortcut set --trigger HoldL1R1Square --ip <console-ip> --port 9080
ncf shortcut set --trigger Off --ip <console-ip> --port 9080
ncf shortcut set --trigger Custom --ip <console-ip> --port 9080
```

### Via API

```bash
curl http://<host-pc-ip>:9080/api/shortcut-config

curl -X POST http://<host-pc-ip>:9080/api/shortcut-config \
  -H "Content-Type: application/json" \
  -d '{"cheatManagerTrigger": "HoldL1R1Square"}'
```

## CLI Commands

```bash
# Manager session
ncf manager open   --ip <console-ip> [--port 9080]
ncf manager close  --ip <console-ip> [--port 9080]
ncf manager status --ip <console-ip> [--port 9080]

# Shortcut config
ncf shortcut show  --ip <console-ip> [--port 9080]
ncf shortcut set   --trigger <Mode> --ip <console-ip> [--port 9080]
ncf shortcut record [--ip <console-ip>] [--port 9080] [--window-ms 5000]
```

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/cheat-manager/open` | Open the cheat manager session |
| POST | `/api/cheat-manager/close` | Close the cheat manager session |
| GET | `/api/cheat-manager/state` | Get current session state |
| GET | `/api/shortcut-config` | Get current shortcut configuration |
| POST | `/api/shortcut-config` | Update shortcut configuration |
| POST | `/api/shortcut-config/record` | Record a custom chord |

## Safety

- Open/close is idempotent: calling open twice is a no-op.
- Toggling a cheat while the manager is closed is allowed (parity with existing behavior).
- If the payload disconnects while the manager is open, the UI shows a
  "Reconnecting…" banner and retries every 2 seconds. Toggle attempts are
  queued (max 16) and replayed on reconnect.
- Hard cap of 64 cheats per session without explicit confirmation.

## Attribution

The cheat-manager-shortcut UX is behaviorally inspired by etaHEN's
`Cheats_shortcut_opt` toolbox flow. No etaHEN source code was used; only the
user-facing behavior was referenced.
