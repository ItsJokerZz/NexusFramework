# Payload Patches

This directory contains source patches for Nexus native payload endpoints.

These patches are **source-only**. They require the native PS4/PS5 payload build
toolchain (typically the Sony SDK or open source SDK like OpenOrbis) to compile
and link. No such toolchain is available in this development environment, so
**runtime testing has not been performed**.

## Patch Files

| File | Endpoint | Description | Status |
|------|----------|-------------|--------|
| `aob_scan_patch.txt` | `POST /aob_scan` | Pattern/AOB scanning directly on the console | Source patch provided, runtime unverified |
| `pad_state_patch.txt` | `GET /pad_state` | Controller state polling on the console | Source patch provided, runtime unverified |

## Applying Patches

1. Clone the NexusFramework payload source from
   https://github.com/ItsJokerZz/NexusFramework
2. Navigate to the native payload source directory (typically `source/console/`)
3. Apply the patch manually or use git patch
4. Build with the appropriate SDK toolchain
5. Deploy the patched payload to your console

## How the C# Side Handles Missing Endpoints

If a payload endpoint is unavailable (returns 404 or connection error):

- **`/aob_scan`**: The C# `AobScanner` class automatically falls back to
  client-side scanning via HTTP `read_memory` calls. This is slower but works
  without payload modifications.
- **`/pad_state`**: The `PadStatePollingService` will report the endpoint as
  unavailable. The `ShortcutDetector` state machine will still function, but
  without real controller samples it cannot detect shortcuts.

## Testing

To verify whether a patched payload endpoint is available:

```bash
curl http://<console-ip>/pad_state
# Expected: {"connected":true/false, ...}

curl -X POST http://<console-ip>/aob_scan \
  -H "Content-Type: application/json" \
  -d '{"pattern":"48 8B ?? ?? 89","max_results":8}'
# Expected: {"matches":[...], ...}
```

## Future Work

These patches should be verified and, if needed, corrected once a native payload
build toolchain is available. Integration tests would then be written to confirm
the endpoints produce correct results against known memory patterns.
