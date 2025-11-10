Awesome—here’s a drop-in set of scripts + CI that will:

* publish **three flavors** of your app: **JIT** (framework-dependent), **R2R** (ReadyToRun), **NativeAOT** (self-contained),
* run **tests** (with coverage) and **benchmarks** (short CI-friendly run),
* upload artifacts from the build.

I’ll give you:

1. repo additions (folders + files)
2. local **PowerShell** and **bash** publish scripts
3. three **Publish Profiles** (`.pubxml`) you can use from Rider or CLI
4. a **GitHub Actions** workflow that builds/tests/benches and publishes all three variants

You can paste these verbatim and commit.

---

# 0) Repo layout additions

```
SwarmSim.sln
  /SwarmSim.Core
  /SwarmSim.Render
    /Properties
      /PublishProfiles
        Jit-win-x64.pubxml
        R2R-win-x64.pubxml
        AOT-win-x64.pubxml
  /SwarmSim.Tests
  /SwarmSim.Benchmarks
/scripts
  publish.ps1
  publish.sh
.github/workflows
  ci.yml
```

> Assumes your entrypoint app is **SwarmSim.Render**. If not, replace project paths accordingly.

---

# 1) Local publish scripts

## 1.1 PowerShell — `scripts/publish.ps1`

```powershell
Param(
  [string]$Project = "SwarmSim.Render",
  [string]$Rid = "win-x64",                  # e.g., win-x64 | linux-x64 | osx-arm64
  [string]$Configuration = "Release",
  [ValidateSet("jit","r2r","aot")]
  [string]$Mode = "jit",
  [switch]$SelfContained,                    # force self-contained (overrides defaults)
  [switch]$SingleFile                       # bundle into one file (R2R/AOT recommended)
)

$ErrorActionPreference = "Stop"

$projPath = Join-Path -Path $PSScriptRoot -ChildPath "..\$Project\$Project.csproj"
if (!(Test-Path $projPath)) { throw "Project not found: $projPath" }

# Output dir: artifacts/<rid>/<mode>
$outDir = Join-Path -Path $PSScriptRoot -ChildPath "..\artifacts\$Rid\$Mode"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Common flags
$props = @()
$props += "-p:StripSymbols=true"                # smaller binaries
$props += "-p:DebugType=None"
if ($SingleFile) { $props += "-p:PublishSingleFile=true" }

# Mode-specific
switch ($Mode) {
  "jit" {
    $props += "-p:PublishReadyToRun=false"
    if ($SelfContained) { $props += "--self-contained true" } else { $props += "--self-contained false" }
  }
  "r2r" {
    $props += "-p:PublishReadyToRun=true"
    $props += "--self-contained true"
  }
  "aot" {
    $props += "-p:PublishAot=true"
    $props += "-p:InvariantGlobalization=true"
    $props += "--self-contained true"
  }
}

# Cross-OS note: NativeAOT must be built on the target OS toolchain.
if ($Mode -eq "aot") {
  if ($Rid -like "linux-*") { Write-Host "NOTE: NativeAOT for Linux needs clang/lld/zlib dev packages on the build host." }
  if ($Rid -like "osx-*")   { Write-Host "NOTE: NativeAOT for macOS requires Xcode CLT toolchain." }
}

dotnet publish $projPath `
  -c $Configuration `
  -r $Rid `
  -o $outDir `
  @props

Write-Host "Published $Mode to $outDir"
```

Examples:

```powershell
# JIT (framework-dependent) for Windows
pwsh scripts/publish.ps1 -Mode jit -Rid win-x64

# R2R single-file, self-contained
pwsh scripts/publish.ps1 -Mode r2r -Rid win-x64 -SingleFile

# NativeAOT single-file (build on Windows for win-x64)
pwsh scripts/publish.ps1 -Mode aot -Rid win-x64 -SingleFile
```

## 1.2 Bash — `scripts/publish.sh`

```bash
#!/usr/bin/env bash
set -euo pipefail

PROJECT="${1:-SwarmSim.Render}"
RID="${2:-linux-x64}"          # linux-x64 | win-x64 | osx-arm64
CONFIG="${3:-Release}"
MODE="${4:-jit}"               # jit | r2r | aot
SINGLE_FILE="${5:-false}"      # true|false

PROJ_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)/$PROJECT/$PROJECT.csproj"
OUT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)/artifacts/$RID/$MODE"
mkdir -p "$OUT_DIR"

PROPS=(-p:StripSymbols=true -p:DebugType=None)
if [[ "$SINGLE_FILE" == "true" ]]; then PROPS+=(-p:PublishSingleFile=true); fi

case "$MODE" in
  jit) PROPS+=(-p:PublishReadyToRun=false --self-contained false) ;;
  r2r) PROPS+=(-p:PublishReadyToRun=true  --self-contained true)  ;;
  aot) PROPS+=(-p:PublishAot=true -p:InvariantGlobalization=true --self-contained true) ;;
  *)   echo "Mode must be jit|r2r|aot"; exit 1 ;;
esac

dotnet publish "$PROJ_PATH" -c "$CONFIG" -r "$RID" -o "$OUT_DIR" "${PROPS[@]}"
echo "Published $MODE → $OUT_DIR"
```

Examples:

```bash
bash scripts/publish.sh SwarmSim.Render linux-x64 Release jit
bash scripts/publish.sh SwarmSim.Render linux-x64 Release r2r true
bash scripts/publish.sh SwarmSim.Render linux-x64 Release aot true
```

---

# 2) Publish Profiles (`SwarmSim.Render/Properties/PublishProfiles`)

These make Rider’s **Publish…** UI (and CLI `-p:PublishProfile=…`) one-click.

## 2.1 `Jit-win-x64.pubxml`

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <Platform>Any CPU</Platform>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>            <!-- framework-dependent -->
    <PublishReadyToRun>false</PublishReadyToRun>
    <PublishSingleFile>false</PublishSingleFile>
    <DebugType>None</DebugType>
  </PropertyGroup>
</Project>
```

## 2.2 `R2R-win-x64.pubxml`

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishSingleFile>true</PublishSingleFile>
    <DebugType>None</DebugType>
    <StripSymbols>true</StripSymbols>
  </PropertyGroup>
</Project>
```

## 2.3 `AOT-win-x64.pubxml`

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishAot>true</PublishAot>
    <PublishSingleFile>true</PublishSingleFile>
    <InvariantGlobalization>true</InvariantGlobalization>
    <DebugType>None</DebugType>
    <StripSymbols>true</StripSymbols>
  </PropertyGroup>
</Project>
```

CLI usage:

```bash
dotnet publish SwarmSim.Render -p:PublishProfile=Jit-win-x64
dotnet publish SwarmSim.Render -p:PublishProfile=R2R-win-x64
dotnet publish SwarmSim.Render -p:PublishProfile=AOT-win-x64
```

> For Linux/macOS, duplicate and change `RuntimeIdentifier`. **NativeAOT must be built on its target OS** (don’t cross-AOT).

---

# 3) GitHub Actions workflow — `.github/workflows/ci.yml`

This runs on **Windows** and **Linux**; does restore/build/test (+ coverage), a short **BenchmarkDotNet** run, and publishes **JIT + R2R** on both OSes, plus **NativeAOT on Windows** (where toolchain is preinstalled). All outputs get uploaded as artifacts.

```yaml
name: ci

on:
  push:
  pull_request:
  workflow_dispatch:

jobs:
  build-test-bench-publish:
    timeout-minutes: 60
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, ubuntu-latest]
        dotnet: [ '8.0.x' ]
        include:
          - os: windows-latest
            rid: win-x64
          - os: ubuntu-latest
            rid: linux-x64

    runs-on: ${{ matrix.os }}

    steps:
    - name: Checkout
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ matrix.dotnet }}
        cache: true

    - name: NuGet restore
      run: dotnet restore

    - name: Build (Release)
      run: dotnet build -c Release --no-restore

    - name: Test (with coverage)
      run: dotnet test -c Release --no-build --collect:"XPlat Code Coverage"

    - name: Upload Test Results
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: TestResults-${{ matrix.os }}
        path: |
          **/TestResults/*
          **/*.trx
        if-no-files-found: ignore

    # ---- Benchmarks (Short CI run) ----
    # Tip: Keep your benchmark project targeting net8.0 only for CI.
    - name: Run Benchmarks (Short)
      run: |
        dotnet run -c Release --project SwarmSim.Benchmarks -- \
          --runtimes net8.0 \
          --filter * \
          --artifacts ./BenchmarkDotNet.Artifacts \
          --warmupCount 1 --iterationCount 3 --invocationCount 1
      shell: bash

    - name: Upload Benchmark Artifacts
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: Benchmarks-${{ matrix.os }}
        path: BenchmarkDotNet.Artifacts/**

    # ---- Publish: JIT (framework-dependent) ----
    - name: Publish JIT
      run: |
        dotnet publish SwarmSim.Render -c Release -r ${{ matrix.rid }} \
          --self-contained false \
          -o artifacts/${{ matrix.rid }}/jit
      shell: bash

    # ---- Publish: ReadyToRun (self-contained, single-file) ----
    - name: Publish R2R
      run: |
        dotnet publish SwarmSim.Render -c Release -r ${{ matrix.rid }} \
          --self-contained true \
          -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:StripSymbols=true \
          -o artifacts/${{ matrix.rid }}/r2r
      shell: bash

    # ---- Publish: NativeAOT (Windows only in this pipeline) ----
    - name: Publish NativeAOT (Windows)
      if: matrix.os == 'windows-latest'
      run: |
        dotnet publish SwarmSim.Render -c Release -r ${{ matrix.rid }} \
          --self-contained true \
          -p:PublishAot=true -p:PublishSingleFile=true -p:InvariantGlobalization=true -p:StripSymbols=true \
          -o artifacts/${{ matrix.rid }}/aot
      shell: pwsh

    - name: Upload JIT artifact
      uses: actions/upload-artifact@v4
      with:
        name: app-${{ matrix.rid }}-jit
        path: artifacts/${{ matrix.rid }}/jit

    - name: Upload R2R artifact
      uses: actions/upload-artifact@v4
      with:
        name: app-${{ matrix.rid }}-r2r
        path: artifacts/${{ matrix.rid }}/r2r

    - name: Upload AOT artifact (Windows)
      if: matrix.os == 'windows-latest'
      uses: actions/upload-artifact@v4
      with:
        name: app-${{ matrix.rid }}-aot
        path: artifacts/${{ matrix.rid }}/aot
```

> Notes
> • **Benchmarks:** the flags keep CI fast. For deeper stats locally, just run without the counts.
> • **AOT on Linux/macOS:** add similar steps on those OS jobs once the toolchains (`clang/lld/zlib-dev`) are installed on runners (or add an apt-get step).
> • **Raylib native libs**: the `Raylib-cs` NuGet provides per-RID native assets; self-contained publishes will include them.

---

# 4) Optional: a tiny `publish-all` convenience

If you like a one-liner for local releases:

```powershell
# scripts/publish-all.ps1
pwsh ./scripts/publish.ps1 -Mode jit -Rid win-x64
pwsh ./scripts/publish.ps1 -Mode r2r -Rid win-x64 -SingleFile
pwsh ./scripts/publish.ps1 -Mode aot -Rid win-x64 -SingleFile
```

---

# 5) Sanity checklist

* [ ] `dotnet build -c Release` succeeds locally.
* [ ] `SwarmSim.Benchmarks` runs locally in Release.
* [ ] `pwsh scripts/publish.ps1 -Mode r2r -Rid win-x64 -SingleFile` produces `artifacts\win-x64\r2r\SwarmSim.Render.exe` (Windows).
* [ ] Push to GitHub → Actions tab shows Tests + Benchmarks + artifacts (JIT/R2R/Windows AOT).
* [ ] Grab the **R2R** artifact for the smallest “just works” single-file build; use **AOT** if you want the fastest startup and lowest RAM.

If you want, I can also add a second workflow that triggers only on **tags** and uploads zipped **release** assets (GitHub Releases) for each publish flavor.
