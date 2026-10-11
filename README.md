# RT Freedom — Mandatory Ray Tracing Bypass & Compatibility Utility

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform Windows](https://img.shields.io/badge/Platform-Windows_10_%2F_11-0078D4?logo=windows&logoColor=white)](https://microsoft.com/windows)
[![UI WPF](https://img.shields.io/badge/UI-WPF_MVVM-00E5FF)](src/RTFreedom.App)
[![Tests Passing](https://img.shields.io/badge/Tests-19%2F19_Passed-00E676)](tests/RTFreedom.Tests)
[![License MIT](https://img.shields.io/badge/License-MIT-gray.svg)](LICENSE)

**RT Freedom** is a Windows desktop utility inspired by tools like *DLSS Enabler*, engineered to analyze games with mandatory ray-tracing requirements and provide verified compatibility solutions, performance tuning patches, and fallback redirections where technically feasible.

---

## ⚡ Core Philosophy: A Genuine Compatibility Engine

> **Critical Rule:** Never assume that flipping a configuration flag is sufficient to remove actual ray-tracing dependencies.

Many modern games do not simply enable ray tracing as a post-processing toggle—they build their entire lighting and rendering architecture around hardware-accelerated BVH traversal (acceleration structures).

**RT Freedom distinguishes between:**
1. **Cosmetic Toggles:** Disabling a flag in an `.ini` file that results in a black screen, broken lightmaps, or an immediate crash.
2. **True Raster Fallbacks:** Games with existing rasterization passes (screen-space reflections, ambient occlusion, baked probes) that can be safely forced.
3. **Hardware-Mandatory Pipelines:** Engines fundamentally built on ray tracing or path tracing where no non-RT fallback assets exist. In these cases, **RT Freedom reports the technical limitation honestly** and provides workload optimization rather than making false promises.

---

## 🎯 Target Games Status & Feasibility

| Game | Engine | Graphics API | Ray Tracing Role | Non-RT Fallback | Compatibility Verdict |
|---|---|---|---|:---:|:---:|
| **Indiana Jones and the Great Circle** | Modified id Tech 7 | Vulkan (`VK_KHR_ray_tracing_pipeline`) | **Mandatory Global Illumination**; optional reflections & shadows | ❌ Engine Level<br>⚠️ Compute Fallback | ⚠️ **Experimental for GTX 10xx/16xx**<br>*(Bypass & Compute Emulation Patch)* |
| **Gears of War: E-Day** | Unreal Engine 5 | DirectX 12 (`DXR 1.1`) | **Lumen, MegaLights, RT Shadows** | ✅ Software Lumen (Distance Fields) | ✅ **Fully Compatible**<br>*(Software Lumen Fallback Patch)* |
| **DOOM: The Dark Ages** | id Tech 8 | Vulkan (`VK_KHR_ray_tracing_pipeline`) | **Mandatory Unified Path Tracing** | ❌ Engine Level<br>⚠️ Compute Fallback | ⚠️ **Experimental for GTX 10xx/16xx**<br>*(Compute RT Fallback Patch)* |

---

## 🚀 Key Features

* **Automated Multi-Store Game Detection:**
  * **Steam:** Discovers multiple Steam libraries from Windows Registry and parses `libraryfolders.vdf` and `appmanifest_*.acf` files.
  * **Epic Games Store:** Scans `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item`.
  * **Xbox / Microsoft Store:** Detects GDK `MicrosoftGame.config` and UWP app manifests across system drives.
  * **Manual Management:** Add custom executable paths with validation, saved in `%LocalAppData%\RTFreedom\manual_games.json`.
* **Hardware & Capability Analyzer:**
  * Hardware inspection via WMI and DXGI.
  * Classifies GPUs into capability tiers:
    * **Tier 0:** Non-RT (GTX 10/16-series, AMD RX 5000-series).
    * **Tier 1:** Basic DXR 1.0 / Vulkan RT (RTX 2060, RX 6600).
    * **Tier 2:** DXR 1.1 + Ray Queries (RTX 30-series, RX 7000, Intel Arc).
    * **Tier 3:** Shader Execution Reordering + Advanced Path Tracing (RTX 40/50-series).
* **Deep Binary & Shader Scanner:**
  * Inspects executables and dynamic libraries for D3D12 RT APIs (`CreateStateObject`, `BuildRaytracingAccelerationStructure`, `DispatchRays`) and Vulkan extensions (`VK_KHR_ray_tracing_pipeline`, `VK_KHR_ray_query`).
  * Scans compiled shaders (`.dxil`, `.spv`, `.cso`) for RT stages: `raygen`, `closesthit`, `anyhit`, `miss`, `intersection`.
* **Plugin Architecture for Game Adapters:**
  * Clean isolation: each title implements `IGameAdapter`. Modifying one title never destabilizes others.
* **Safety-First Backup & Rollback Engine:**
  * Cryptographic SHA-256 snapshotting before any file modification.
  * One-click instant restore with file integrity verification.
  * Zero tolerance for anti-cheat tampering (Easy Anti-Cheat, BattlEye, Denuvo are never modified).
* **Modern Windows Desktop UI:**
  * Dark interface with **Electric Cyan (`#00E5FF`)** and **Deep Violet (`#7C4DFF`)** accents.
  * Custom borderless window with draggable titlebar, search bar, card hover glows, and live hardware indicators.

---

## 🏗️ Technical Architecture

```
RTFreedom.sln
├── src/
│   ├── RTFreedom.Core/          # Shared domain entities, models, enums, interfaces
│   │   ├── Models/              # GameInfo, GameProfile, GpuCapabilities, RayTracingAnalysis
│   │   ├── Enums/               # CompatibilityLevel, PatchStatus, RenderingApi, RTFeature
│   │   └── Interfaces/          # IGameDetector, IGraphicsAnalyzer, IGameAdapter, IBackupManager
│   │
│   ├── RTFreedom.GameDetection/ # Store library parsers and manifest detectors
│   │   ├── SteamGameDetector.cs # Registry lookup + VDF library scanning
│   │   ├── EpicGameDetector.cs  # ProgramData JSON manifest parser
│   │   ├── XboxGameDetector.cs  # GDK & UWP package discovery
│   │   ├── ManualGameManager.cs # User-defined standalone games
│   │   └── VdfParser.cs         # Zero-dependency recursive-descent VDF parser
│   │
│   ├── RTFreedom.Graphics/      # Pipeline inspection and GPU detection
│   │   ├── GpuCapabilityDetector.cs  # WMI hardware probing & RT Tier assignment
│   │   ├── DxrDependencyScanner.cs   # Chunked PE binary & shader bytecode scanner
│   │   └── RenderingPathAnalyzer.cs  # Fallback vs pure RT evaluation
│   │
│   ├── RTFreedom.Adapters/      # Plugin engine & game compatibility adapters
│   │   ├── AdapterRegistry.cs        # Thread-safe adapter discovery
│   │   ├── IndianaJonesAdapter.cs    # id Tech 7 Vulkan RT GI analysis & tuning
│   │   ├── GearsOfWarAdapter.cs      # Gears of War: E-Day UE5 Software Lumen fallback patch
│   │   └── DoomDarkAgesAdapter.cs    # id Tech 8 path tracing analysis & tuning
│   │
│   ├── RTFreedom.Backup/        # Data integrity and rollback management
│   │   ├── FileIntegrityVerifier.cs  # SHA-256 stream hashing
│   │   ├── BackupManifestStore.cs    # Atomic JSON manifest persistence
│   │   └── BackupService.cs          # Snapshot management and atomic rollback
│   │
│   ├── RTFreedom.Diagnostics/   # Logging and reporting subsystem
│   │   ├── FileLogger.cs             # Thread-safe rolling logger (%LocalAppData%\RTFreedom\Logs)
│   │   ├── DiagnosticReportGenerator.cs # Structured compatibility audit reports
│   │   └── CrashDiagnostics.cs       # AppDomain & unobserved task crash traps
│   │
│   └── RTFreedom.App/           # WPF MVVM Desktop application
│       ├── Themes/DarkTheme.xaml     # NZXT/Razer style dark gaming palette
│       ├── ViewModels/               # Main, GameLibrary, GameCard, GameDetail, Settings
│       └── Views/                    # MainWindow, GameLibraryView, GameDetailView, SettingsView
│
├── tests/
│   └── RTFreedom.Tests/         # 17 automated xUnit unit & integration tests
└── docs/
    └── compatibility-report.md  # Detailed technical analysis & GPU compatibility matrix
```

---

## 🛡️ Safety & Anti-Cheat Guarantees

1. **Mandatory Backups:** No game file or configuration is ever written without computing an SHA-256 hash and saving an uncompressed snapshot.
2. **One-Click Rollback:** Any patch can be completely reverted at any time, restoring verified bit-for-bit original files.
3. **No Anti-Cheat Tampering:** Games with online multiplayer anti-cheat protections (EAC, BattlEye, Vanguard) are strictly guarded. RT Freedom does not bypass, hook, or tamper with anti-cheat components.
4. **No Untrusted DLL Injections:** RT Freedom never downloads, runs, or bundles untrusted third-party binaries.

---

## 💻 Hardware Compatibility Matrix

| GPU Family | RT Hardware | Tier | Indiana Jones | Gears of War: E-Day | DOOM: Dark Ages |
|---|:---:|:---:|:---:|:---:|:---:|
| **NVIDIA GeForce GTX 10xx (6GB+)** | ⚠️ Compute Emulation (Driver 425.31+) | Tier 0-E | ⚠️ Experimental (Bypass Patch) | ✅ Fully Compatible | ⚠️ Experimental (Fallback Patch) |
| **NVIDIA GeForce GTX 16xx (Turing non-RTX)** | ⚠️ Compute Emulation | Tier 0-E | ⚠️ Experimental (Bypass Patch) | ✅ Fully Compatible | ⚠️ Experimental (Fallback Patch) |
| **Legacy Non-RT (GTX 9xx / GTX 1050)** | ❌ None | Tier 0 | ❌ Incompatible | ✅ Fully Compatible | ❌ Incompatible |
| **NVIDIA GeForce RTX 20xx** | ✅ DXR 1.0 | Tier 1 | ⚠️ Low Settings | ✅ Fully Compatible | ⚠️ Low Settings |
| **NVIDIA GeForce RTX 30xx** | ✅ DXR 1.1 | Tier 2 | ✅ Medium/High | ✅ Fully Compatible | ✅ Medium |
| **NVIDIA GeForce RTX 40xx / 50xx** | ✅ DXR 1.1 + SER | Tier 3 | ✅ Ultra | ✅ Fully Compatible | ✅ High/Ultra |
| **AMD Radeon RX 5xxx** | ❌ None | Tier 0 | ❌ Incompatible | ✅ Fully Compatible | ❌ Incompatible |
| **AMD Radeon RX 6xxx** | ✅ DXR 1.0 | Tier 1 | ⚠️ Low Settings | ✅ Fully Compatible | ⚠️ Low Settings |
| **AMD Radeon RX 7xxx** | ✅ DXR 1.1 | Tier 2 | ✅ Medium | ✅ Fully Compatible | ✅ Medium |
| **Intel Arc A-Series / B-Series** | ✅ DXR 1.1 | Tier 2 | ⚠️ Variable | ✅ Fully Compatible | ⚠️ Variable |

---

## 🛠️ Building & Running

### Prerequisites
* **Windows 10 (version 1903+)** or **Windows 11** (64-bit)
* **[.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)** (installed automatically in `%LocalAppData%\dotnet`)

### Build Solution
```powershell
# Restore dependencies and build entire solution
dotnet build RTFreedom.sln -c Release
```

### Run Unit Tests
```powershell
# Execute the 17-test automated test suite
dotnet test tests\RTFreedom.Tests\RTFreedom.Tests.csproj -c Release
```

### Run Desktop Application
```powershell
dotnet run --project src\RTFreedom.App\RTFreedom.App.csproj -c Release
```

---

## 🔌 Adding a Custom Game Adapter

To support a new game, implement `IGameAdapter` or inherit from `BaseGameAdapter` in [`RTFreedom.Adapters`](src/RTFreedom.Adapters):

```csharp
public class MyGameAdapter : BaseGameAdapter
{
    public override string GameId => "my-game-id";
    public override string GameTitle => "My Game Title";
    public override string[] SupportedVersions => ["*"];

    public override bool SupportsGame(GameInfo game) =>
        game.Title.Contains("My Game", StringComparison.OrdinalIgnoreCase);

    public override async Task<RayTracingAnalysis> AnalyzeAsync(
        GameInfo game, GpuCapabilities gpu, CancellationToken ct = default)
    {
        // 1. Inspect pipeline
        // 2. Honestly check if non-RT fallback exists
        // 3. Return RayTracingAnalysis
    }

    public override async Task<IReadOnlyList<PatchAction>> GetAvailablePatchesAsync(
        GameInfo game, RayTracingAnalysis analysis)
    {
        // Return verified patch actions
    }
}
```
Register the adapter in [`AdapterLoader.cs`](src/RTFreedom.Adapters/AdapterLoader.cs) or place its compiled assembly into the `plugins/` directory.

---

## 📄 License & Disclaimer

* **License:** Distributed under the MIT License. See [LICENSE](LICENSE) for details.
* **Disclaimer:** RT Freedom is an independent open-source utility for compatibility research and optimization. It is not affiliated with Bethesda Softworks, id Software, MachineGames, The Coalition, Microsoft, NVIDIA, AMD, or Valve.
