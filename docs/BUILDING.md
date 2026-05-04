# Building

## Prerequisites
- .NET SDK 8.0 or later (Core lib targets `netstandard2.1`; CLI, Web, and
  tests target `net8.0` / `net10.0`).
- Linux, macOS, or Windows.

The repo contains no native code paths that need to build by default.
Optional adapter code that wraps the upstream NexusFramework C# library
is in `src/NexusCheatFramework.NexusAdapter/` and is **not** part of the
default solution build.

## Build everything in the solution

```sh
./scripts/build.sh           # Linux / macOS
./scripts/build.ps1          # Windows
```

Both scripts run `dotnet restore`, `dotnet build`, and `dotnet test` on
`NexusCheatFramework.sln`.

## Build manually

```sh
dotnet restore NexusCheatFramework.sln
dotnet build   NexusCheatFramework.sln -c Release
dotnet test    NexusCheatFramework.sln -c Release
```

## Run the CLI from source

```sh
dotnet run --project src/NexusCheatFramework.Cli -- info --ip 192.168.1.50
```

To produce a single-file binary:

```sh
dotnet publish src/NexusCheatFramework.Cli \
    -c Release -r linux-x64 --self-contained true \
    /p:PublishSingleFile=true
```

## Run the WebUI from source

```sh
dotnet run --project src/NexusCheatFramework.Web -- --ip 192.168.1.50 --port 9080 --db ./cheats
```

Then open `http://localhost:9080/` in a browser.

## Build status

**v0.2 — ✅ Build-verified.** `dotnet build -c Release` completes with 0 errors, 0 warnings.
**v0.2 — ✅ Test-verified.** 50 tests pass (`dotnet test -c Release`, ~180ms).

## Building the optional Nexus adapter

The adapter project pulls in the upstream `NexusFramework.csproj`, which
embeds payload `.elf` binaries as resources.

```sh
dotnet build src/NexusCheatFramework.NexusAdapter/NexusCheatFramework.NexusAdapter.csproj -c Release
```

## CI

`.github/workflows/ci.yml` runs build + test on push/PR to all active
branches. See the CI badge in README.

### CI workflow triggers

- **Push** to: `main`, `develop`, `active`, `claude/*`
- **Pull request** to: `main`, `develop`, `active`

The CI runs on `ubuntu-latest` with .NET 8.0 SDK. Two jobs execute:
1. `build-and-test` — restore, build (Debug + Release), test (Debug + Release)
2. `lint-format` — `dotnet format --verify-no-changes`
