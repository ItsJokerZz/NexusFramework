# Menu Integration

## TL;DR

There are two complementary menu paths:

1. **etaHEN toolbox link** — adds a "★ Nexus Cheats" entry to etaHEN's
   toolbox so it's discoverable from the console. The page itself is
   informational; it explains how to reach the live menu.
2. **Companion WebUI / CLI** — the live cheat menu, hosted off-console
   because the Nexus payload doesn't render UI on its own.

True in-game overlay rendering is **not implemented**. See
`docs/LIMITATIONS.md` for why.

## Path 1 — etaHEN toolbox

1. Copy `menu/etaHEN_xml/nexus_cheats.xml` next to etaHEN's
   `etaHEN_toolbox.xml` (typically packaged with the shellui assets).
2. Open `etaHEN_toolbox.xml` and add the link below — see
   `menu/etaHEN_xml/toolbox_link.snippet.xml` for the exact text and
   placement context:

   ```xml
   <link id="id_nexus_cheats" title="★ Nexus Cheats" file="nexus_cheats.xml"/>
   ```

3. If etaHEN's assets are encrypted on your build, re-encrypt with
   `ETAHEN/shellui/assets/encryptxml.py` (this is etaHEN's own tool;
   we don't ship it).
4. Reboot the toolbox; the entry appears alongside "Cheats (WIP)".

The XML page does not call back into Nexus directly. It provides
instructions and a stable identifier so future Nexus payload endpoints
can be wired in (see "Future" below).

## Path 2 — companion WebUI / CLI

Today the live menu lives off-console. Two front-ends share the same
back-end (`CheatManager` + `CheatEngine`):

### CLI (shipped)
```sh
ncf cheats list    --ip <console-ip> --db ./cheats
ncf cheats enable  --ip <console-ip> --db ./cheats --cheat <id>
ncf cheats disable --ip <console-ip> --db ./cheats --cheat <id>
```

### WebUI (scaffolded)
`menu/webui/index.html` consumes a small JSON HTTP API. The HTML is
ready; the host server is up to you. The expected endpoints are:

| Method | Path | Body | Returns |
|---|---|---|---|
| GET | `/api/state` | – | `{ target: ProcessSnapshot, cheats: CheatRow[] }` |
| POST | `/api/enable` | `{ id }` | `CheatResult` |
| POST | `/api/disable` | `{ id }` | `CheatResult` |

A minimal host can be built with `Microsoft.AspNetCore.Mvc.Minimal` plus
your existing `CheatManager` instance. We deliberately don't ship the
host so you can choose your hosting stack.

## Future: payload-side menu endpoint

To get a one-press in-game overlay we'd need either:

- A Nexus payload endpoint that draws on-screen toasts (e.g. via the
  system's notification API), or
- A cooperating shellui hook (etaHEN territory; GPLv3) that calls
  `GoToURI("Nexus?Cheats")` like etaHEN's `GoToURI("etaHEN?Cheats")`.

Both are feasible but neither is in scope for v0.1.0-alpha. The detector
in `Input/ShortcutDetector.cs` is already factored so it can drive either
path.
