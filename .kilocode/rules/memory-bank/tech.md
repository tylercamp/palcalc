# PalCalc – Technology Overview
*(Version 2025-07-02)*

---

## 1. Core Stack
| Layer | Technology | Details |
|-------|------------|---------|
| Language | **C# 13** | `LangVersion=latest` |
| Runtime | **.NET 9.0** | All projects use SDK-style csproj |
| UI | **WPF** | Windows-only desktop front-end |
| Tests | **MSTest** | `PalCalc.Solver.Tests` |

---

## 2. Development Environment
| Tool | Minimum Version | Purpose |
|------|-----------------|---------|
| Visual Studio 2022 | 17.10+ | Primary IDE, WPF designer |
| .NET SDK 9 Preview | 9.0.100+ | CLI build & publish |
| Git | 2.40+ | Source control |
| ResXResourceManager (VSIX) | 1.75+ | Localization workflow |

Projects are developed and built on **Windows 11**; UI targets Windows 10 17763+.

---

## 3. Solution Layout
All projects live in `PalCalc.sln`:

```
PalCalc.Model
PalCalc.SaveReader
PalCalc.GenDB
PalCalc.Solver
PalCalc.UI
PalCalc.SaveReader.CLI
PalCalc.Solver.CLI
PalCalc.Solver.Tests
```

---

## 4. Dependency Matrix
| Package / Library | Scope | Notes |
|-------------------|-------|-------|
| `GraphSharp`, `GraphSharp.Controls` | UI | Local fork (embedded) |
| `AdonisUI`, `AdonisUI.ClassicTheme` | UI | Embedded copy with custom tweaks |
| `DotNetKit.Wpf.AutoCompleteComboBox` | UI | Modified fork for UX |
| `CUE4Parse` | GenDB | Unreal asset extraction |
| `CommunityToolkit.Mvvm` | UI | MVVM helpers & code-gen |

---

## 5. Build Commands
```bash
# restore
dotnet restore

# debug build
dotnet build PalCalc.sln

# run UI
dotnet run -p PalCalc.UI

# regenerate db.json & icons
dotnet run -p PalCalc.GenDB

# publish single-file release
dotnet publish PalCalc.UI -c Release -r win-x64 \
  -p:PublishSingleFile=true --self-contained false
```

---

## 6. Typical Workflows
1. **Regenerate Game DB**
   ```bash
   dotnet run -p PalCalc.GenDB
   ```
2. **Run Tests**
   ```bash
   dotnet test PalCalc.Solver.Tests
   ```

---

## 7. Technical Constraints
* Windows-only desktop UI (WPF, GraphSharp)
* Read-only interaction with Palworld saves
* Forked/embedded libraries must be updated manually
* Solver memory budget ≈ 2 GiB; algorithms favour low-allocation patterns

---

*Generated during initial Memory Bank **initialisation**. Update as tech stack evolves.*