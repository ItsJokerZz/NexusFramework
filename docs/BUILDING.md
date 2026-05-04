# Building

## Prerequisites
- .NET SDK 8.0 or later (the Core lib targets `netstandard2.1`; the CLI
  and tests target `net8.0`).
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

## Building the optional Nexus adapter

The adapter project pulls in the upstream `NexusFramework.csproj`, which
embeds payload `.elf` binaries as resources (present in the bundled
`NexusFramework/source/libraries/C#/payloads/`). It also references
`System.Text.Json 10.0.7` upstream — adjust to a real version if your NuGet
mirror doesn't have that one before building.

```sh
dotnet build src/NexusCheatFramework.NexusAdapter/NexusCheatFramework.NexusAdapter.csproj -c Release
```

## CI

`.github/workflows/ci.yml` runs build + test on push / PR.

## Validation status

This v0.1.0-alpha drop was authored in an environment **without** a .NET
SDK; the source has not been fed through `dotnet build` end-to-end inside
that environment. CI is the canonical signal. If you hit a build error,
file an issue with the diagnostic — the project is small and reachable.
