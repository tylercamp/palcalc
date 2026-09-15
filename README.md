# PalCalc

## Automatic Palworld Breeding Tree Calculator

**The definitive breeding solver for Palworld.** Spend less time planning and more time playing!

Load your save, choose exactly what you want, and PalCalc automatically builds complete breeding trees from the Pals you already own. It compares paths using breeding and inheritance probabilities - not just the number of steps - while accurately accounting for passive skills, attack skills, and IVs.

PalCalc is free and open source for Windows. It runs locally and reads your save files directly.

- **[Download the standalone EXE (recommended)](https://github.com/tylercamp/palcalc/releases/latest/download/PalCalc.UI.exe)**
- **[Download the ZIP package](https://github.com/tylercamp/palcalc/releases/latest/download/PalCalc-NoBundle.zip)** - try this if antivirus software interferes with the standalone EXE

[Latest release notes and all downloads](https://github.com/tylercamp/palcalc/releases/latest) · [Wiki](https://github.com/tylercamp/palcalc/wiki)

## How it works

1. Load an auto-detected Steam or Xbox save, add a downloaded server save, or enter your Pals manually
2. Choose a target Pal, passive skills, attack skills, and IVs
3. Let PalCalc calculate and compare complete multi-generation breeding trees

Every breeding step shows where to find the required Pals and estimates how long the step will take.

Xbox saves must first sync to your PC by installing Palworld through the [Xbox app](https://apps.microsoft.com/detail/9mv0b5hzvk9z) and running the game at least once.

https://github.com/user-attachments/assets/2ededad9-4f0a-47b1-a460-b553be46bdd4

_v1.12.1 recording_

## Download and run

### Standalone EXE - recommended

Download `PalCalc.UI.exe`, place it in its own folder, and run it. When run for the first time, Windows may ask you to install [.NET 9 from Microsoft](https://dotnet.microsoft.com/download/dotnet/9.0).

### ZIP package - antivirus fallback

If antivirus software interferes with the standalone EXE, download `PalCalc-NoBundle.zip` instead. Extract the full contents into their own folder and run the included `PalCalc.UI.exe`. When updating, replace all files in that folder.

PalCalc does not require administrator access. If it unexpectedly asks for elevated permissions, do not continue and [report the problem](https://github.com/tylercamp/palcalc/issues).

## Features

- **Start with the Pals you actually have**
  - Load a local Steam or Xbox save, add a downloaded server save, or build a virtual collection by hand
  - Find routes from your owned Pals, with wild Pals included only when you allow them
  - Focus the search on a specific guild or player
- **Ask for exactly the Pal you want**
  - Choose the species, passive skills, attack skills, and IVs you care about
  - Combine those requirements in a single search instead of solving each one separately
  - Save presets for passive combinations you use often
- **Get a complete plan with realistic estimates**
  - PalCalc compares multi-generation trees by their chances of success, not just their number of steps
  - Time estimates account for gender odds and the chances of inheriting desired passives, attacks, and IVs
  - Gender Reversers, Surgery Table options, and Special Cakes are included when enabled
  - You control the limits for breeding steps, wild Pals, and unwanted passives
- **Easily find your Pals**
  - Pals used in the breeding tree are linked back to their Palbox, base, viewing cage, or other source
  - Use the minimap and coordinates to find Pals at bases and viewing cages
  - Search your collection by Pal, skill, or IV with the Save Inspector
- **Available in multiple languages**
  - Pal and skill names come directly from Palworld's localized game data
  - Community translations cover the rest of the interface, and the [translation guide](./PalCalc.UI/Localization/README.md) walks through adding or improving one

Curious how PalCalc reaches its answers? Read about the [full solver process](./PalCalc.Solver/README.md), the underlying [Palworld breeding mechanics and probabilities](./PalCalc.Solver/README-PALWORLD-MECHANICS.md), or check out the example [step-by-step passive inheritance calculation](./PalCalc.Solver/README-BREED-ESTIMATE.md).

PalCalc's save support is based on [palworld-save-tools by cheahjs](https://github.com/cheahjs/palworld-save-tools). Its breeding-effort calculations were first built on community research into [passive inheritance mechanics](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/).

## For contributors

PalCalc targets .NET 9 and Windows x64. Visual Studio Community 2022 or a compatible .NET SDK can build the solution.

```powershell
dotnet build PalCalc.sln
dotnet test PalCalc.Solver.Tests
dotnet run --project PalCalc.UI
```

The solution is divided into four main projects:

- [`PalCalc.Model`](./PalCalc.Model/) contains game data and shared domain models
- [`PalCalc.SaveReader`](./PalCalc.SaveReader/) reads Palworld save files
- [`PalCalc.Solver`](./PalCalc.Solver/) calculates and compares breeding paths
- [`PalCalc.UI`](./PalCalc.UI/) is the Windows WPF application

To update Palworld game data, use [`PalCalc.GenDB`](./PalCalc.GenDB/). It rebuilds `PalCalc.Model/db.json` from locally installed game files; do not edit that file manually. See the [database-generation guide](./PalCalc.GenDB/README.md) for setup instructions.

Bug reports, research contributions, and pull requests are welcome in [GitHub Issues](https://github.com/tylercamp/palcalc/issues).

## License

PalCalc is available under the [MIT License](./LICENSE.txt).
