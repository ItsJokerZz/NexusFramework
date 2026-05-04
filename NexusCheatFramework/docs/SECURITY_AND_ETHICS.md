# Safety, Legal, and Ethics

## Scope (read this)

NexusCheatFramework is built for:

- **Homebrew** development on **your own** jailbroken consoles.
- **Reverse engineering** and research on **games you own**.
- **Single-player / offline** modding and accessibility (e.g. assistive
  cheats for players who need them, freezing a value to make a game
  beatable, restoring cut content).
- **Educational use** — the code is annotated and tested so people can
  learn how this kind of system fits together.

It is **not** for:

- Cheating in online matchmaking, ranked play, or any competitive
  multiplayer context.
- Manipulating leaderboards, accounts, or telemetry.
- Defeating anti-cheat or anti-tamper systems for online services.
- Piracy, license-bypass, DRM removal, or content theft.
- Stealing credentials, tokens, or anyone else's saves.
- Any deployment that affects users other than yourself.

The configuration file accepts an `allowOnlineUse` flag that is **always
false by definition**. It exists only to make the policy unambiguous and
visible in audits; setting it to true does not enable any feature.

## Risk acknowledgement

Modifying console memory can crash games, corrupt saves, or in pathological
cases damage the system in ways that require service-mode or factory
reset. **You assume all risk.** Make backups. Don't run cheats on saves
you can't afford to lose. Don't run with online services connected.

## License compliance

This project is GPL-3.0-or-later. If you redistribute it, redistribute
modifications under the same license, preserve `ATTRIBUTION.md`, and
include the LICENSE file. See `ATTRIBUTION.md` for upstream credit.

## Reporting

If someone is using a fork of this project to attack online services,
hijack accounts, or harm other players, that's outside the supported
scope. Open an issue on the upstream repository with the details and we
will document the abuse in `LIMITATIONS.md`.
