# Controller Shortcuts

## Supported modes

The `CheatsShortcutMode` enum mirrors etaHEN's `Cheats_Shortcut` so existing
toolbox users see familiar names:

| Value | Mode | Behavior |
|---|---|---|
| 0 | `Off` | Disabled |
| 1 | `HoldR3L3` | Press and hold L3 + R3 for `HoldDuration` |
| 2 | `HoldL2Triangle` | Press and hold L2 + △ for `HoldDuration` |
| 3 | `LongHoldOptions` | Long-hold the Options button |
| 4 | `LongHoldShare` | Long-hold the Share / Create button |
| 5 | `SingleTapShare` | Quick tap the Share / Create button |

## Config

```json
{
  "shortcut": {
    "enabled": true,
    "mode": "HoldR3L3",
    "holdMilliseconds": 750,
    "tapMaxDurationMilliseconds": 300,
    "debounceMilliseconds": 1000,
    "pollIntervalMilliseconds": 33
  }
}
```

- `holdMilliseconds` — minimum hold for the hold-style modes.
- `tapMaxDurationMilliseconds` — release before this elapses to count as a
  tap.
- `debounceMilliseconds` — minimum gap between successive triggers.
- `pollIntervalMilliseconds` — how often the *caller* should sample the pad
  and call `Update`. The detector is sample-driven, not time-driven; this
  setting just guides the host loop.

## How detection works

`ShortcutDetector` is a deterministic state machine. The host calls

```csharp
detector.Update(buttons, DateTime.UtcNow);
```

at any cadence. It tracks per-mode state (`_heldComboSince`, `_shareDownAt`)
and raises `OnTrigger` when the configured combo completes. Time is supplied
by the caller, so unit tests can advance the clock instantly (see
`ShortcutDetectorTests`).

Why a state machine and not a polling loop? It separates *what counts as
a shortcut* from *where pad samples come from*. The same detector can be
driven by:

- a host overlay app reading the pad over USB,
- a future Nexus payload endpoint that exposes `scePadReadState`,
- a recorded log replayed in tests.

## `PadStatePollingService`

`PadStatePollingService` is a host-side polling loop that drives
`ShortcutDetector`. It is configured with an interval and cancellation
token support.

**Status:** host-side only. The service calls `INexusClient.GetPadStateAsync()`,
which is a **contract-only** method — the native payload does not expose
`scePadReadState`. Until the payload implements `GET /pad_state`, the
service creates the polling infrastructure but receives no pad data.

When `GetPadStateAsync` returns real data, it feeds the buttons into
`ShortcutDetector.Update()`, and when a shortcut triggers:
- A log event is emitted (visible in WebUI)
- The `OnTrigger` callback is invoked

## Limitations

- **Console-side polling is not implemented.** NexusFramework does not
  currently expose `scePadOpen` / `scePadReadState`. Wiring the detector
  to the real pad on the console requires a payload-side change. The
  `INexusClient.GetPadStateAsync()` contract exists for when that endpoint
  is added.
- **The Share / Create button is special.** On retail consoles it is
  intercepted by the system before games see it; etaHEN handles this with
  shellui hooks. From outside shellui you generally cannot read the
  Share button via `scePadReadState` alone. Modes 4 and 5 therefore depend
  on shellui-side cooperation in production.
- **Pad disconnects** are handled by the caller; if no samples arrive,
  in-flight hold state never advances and never triggers spuriously.

## Recommended primary modes (v0.2)

Given the payload polling limitation, the recommended shortcut modes are:
- `HoldR3L3` — most reliable, no system interception
- `HoldL2Triangle` — also reliable, no system interception

`LongHoldShare` and `SingleTapShare` are documented but depend on
shellui-side hooks that are not part of this project.

## License note

The shortcut option *names* are inherited from etaHEN's user-facing UI for
familiarity. The detection logic is a clean reimplementation written from
the publicly observable behavior. See `ATTRIBUTION.md`.
