# Pal Calc

![Pal Calc Screenshot](./docres/palcalc-screenshot.jpg)

_v1.4.3 screenshot_

**Fixed for Sakurajima update with latest pals and traits!**

Pal Calc is a Windows program for calculating the optimal steps to breed a specific pal with a specific set of traits:

- Can detect and read from your local game save files, based on [palworld-save-tools by cheahjs](https://github.com/cheahjs/palworld-save-tools)
- Supports local Steam saves and Xbox saves
  - Xbox saves are synced to your PC by downloading the game through the ['Xbox' app on Windows](https://apps.microsoft.com/detail/9mv0b5hzvk9z) and running the game at least once. Save files are synced when the game is run.
- Provides time estimates on each step, based on probabilities and mechanics [derived by /u/mgxts in this Reddit post](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/)
  - Gender probabilities
  - Probability of directly- and randomly-inserted traits
  - For directly-inherited traits, probability of getting the desired traits
- Offers the optimal path
  - Determines "path efficiency" based on calculated probabilities, not just the total number of steps
  - Handles single-root paths, where you successively breed children with another pal you own (one "starting point")
  - Handles multi-root paths, where two children are bred (multiple "starting points")
- Flexible search process
  - Allows wild pals
  - Set a max number of undesired traits if you're ok with imperfect pals
  - Set limits on the number of breeding steps
  - Choose which pals you want to include by filtering by guilds and players
- Efficient
  - Low memory usage and fast load times
  - Relatively fast path-solving process, searches take under a minute
  - Distributes path-solving work across all available CPU cores
- Save Inspector
  - Lists all pal containers (palbox, viewing cages, etc.)
  - Inspect pals to see IVs and traits
  - Search for specific pals and/or pals with specific traits
- Multiple languages
  - Supports all languages in Palworld, pal and trait names imported from game files
  - Translations for in-app text [can be added](./PalCalc.UI/Localization/README.md)

_See [here](./PalCalc.Solver/) for an overview of the full solver process._

# FAQs

### Why won't the app start?

Pal Calc requires [.NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.7-windows-x64-installer). You shouldn't need to install this manually as the app should prompt you to install this when you run it if needed.

Pal Calc also creates and manages the folder `data`, `cache`, and `logs`. If Pal Calc is in a folder which already has these files from another program, it may cause startup issues. You should place Pal Calc in a folder which does not have these, or (preferably) in its own dedicated folder.

### Where are my Pals?

Pal Calc should auto-detect all pals across all of your pal containers (palbox, viewing cages, etc.). If you're not sure whether Pal Calc is finding a specific pal, you can use the Save Inspector (select your save game and click the "Inspect" button) to see all of the detected pal containers and their contents. You can open an [issue](https://github.com/tylercamp/palcalc/issues) if your pal isn't showing up. (Make sure your save file is up-to-date first.)

### How do I import server / co-op saves?

You'll need a copy of the save files on your computer for Pal Calc to read them. You'll be looking for a folder containing:

- `Level.sav` (required)
- `LevelMeta.sav` (optional but recommended)
- `WorldOption.sav`
- `LocalData.sav`
- A `Players` folder (optional but recommended)

For simple co-op saves, the person hosting the game can run Pal Calc and use the "Export Save" button to make a ZIP with all of the needed files. Extract the ZIP locally.

For server saves, the location of the save data can vary. Check with the documentation for your server on how to back up or restore save data - this should mention where these files are stored.

Once you have the save files:

1. Open Pal Calc
2. Expand the "Location" dropdown on the top-left
3. Select "Manually Added"
4. In the "Game" dropdown, select "Add a new save..."
5. Select the `Level.sav` file in your save folder

These save files will need to be manually updated whenever there changes. Pal Calc will auto-detect changes when it starts and read any new data.

### Why aren't I getting any results?

There are a few reasons for an empty list / result:

- You're trying to get a pal which requires specific parents that you don't have (e.g. Jetragon _requires_ two Jetragons, Chikipi _requires_ two Chikipis).
- Some of the selected traits can't be found on any of your pals (use the Save Inspector to search by trait).
- A requested trait can be found in wild pals (e.g. Bellanoir Libero is guaranteed Siren of the Void) but "Max Wild Pals" is set to zero or the pal is excluded from "Allowed Wild Pals".
- You have a pal with the requested trait, but that pal has been excluded due to filters.
  - This is often caused by the "Max Input Irrelevant Traits" settings, which should typically be set to the max value (3). This setting is mainly to reduce the starting number of pals and reduce the solver's runtime.
- You have a pal with the requested trait and the solver _is_ using that pal, but "Max Breeding Steps" is too low for it to find a full path.
  - This setting is mainly to prevent the solver from taking too long. In practice, it will detect when further breeding is redundant and stop early.

# Community Help

Pal Calc currently has some outstanding pieces that need more information to resolve. Some of these need some level of reverse engineering, but some can be figured out through experimentation and statistics. An [issue](https://github.com/tylercamp/palcalc/issues) has been created for each item, where more information can be found.

1. Is there a formula for how long breeding takes? Or is it a constant five minutes? [Issue](https://github.com/tylercamp/palcalc/issues/2)
2. What's the probability of wild pals having exactly N traits? [Issue](https://github.com/tylercamp/palcalc/issues/4)
3. Has the trait inheritance calculation changed since /u/mgxts reverse engineered it? Was their reverse engineering accurate? [Issue](https://github.com/tylercamp/palcalc/issues/7)
4. Assuming the trait inheritance calculation is correct, is Pal Calc's implementation of those probabilities correct? [Issue](https://github.com/tylercamp/palcalc/issues/8)
5. How can we derive the map coordinates for a base from its world coordinates? [Issue](https://github.com/tylercamp/palcalc/issues/9)
6. What's a good way to estimate time needed to capture a wild pal of a certain type? e.g. Chikipi would be much faster to find + catch than Paladius. [Issue](https://github.com/tylercamp/palcalc/issues/10)

# Development

Visual Studio Community 2022 is required. The `.CLI` projects act as test programs which can be ran without involving the whole Pal Calc UI.

## Palworld Database

The list of pals, traits, and stats are stored in a `db.json` file embedded in `PalCalc.Model`. This file is generated by the [`PalCalc.GenDB`](./PalCalc.GenDB/) project. Running this project will update the `db.json` file in `PalCalc.Model` which will be used by the rest of the projects. The input files for this, stored under `PalCalc.GenDB/ref`, are generated by the [`ScrapeData/fetch.js`](./ScrapeData/) utility, which also fetches the latest pal icons.

The `db.json` file should _not_ be modified manually. It should be modified by changing the contents of `PalCalc.GenDB` and re-running the program to regenerate it. See the README in the `PalCalc.GenDB` project for more information.

(`FetchIcons` is old and should not be used.)

## Save File Support

Save file parsing is in `PalCalc.SaveReader`, which is a partial C# port of [palworld-save-tools](https://github.com/cheahjs/palworld-save-tools). See the project's [README](./PalCalc.SaveReader/) for more information.

## Data and Solver Model

Data collected from Palworld or a save file are represented by types in `PalCalc.Model`. Instances of an owned pal within the game are represented by `PalInstance`.

The solver logic in `PalCalc.Solver` wraps this type with `IPalReference` types, which can represent owned, wild, and bred pals. The `IPalReference` types are returned by `PalCalc.Solver.BreedingSolver`, but you can use `OwnedPalReference` results to fetch underlying owned instances.

The overall solver process is described in the project's [README](./PalCalc.Solver/).

## Pal Calc UI

The general structure of the `PalCalc.UI` project is somewhat messy. The application uses WPF and (weak) MVVM for handling its UI. MVVM was largely followed for convenience and it was not strictly adhered to. There are various hackfixes since many features were unplanned and added through the path of least resistance. Refactoring is planned and gladly accepted.

Entries in the `Resource` folder are set to the `Resource` build action and embedded in the final program.

The Community Toolkit library is used for the viewmodels, which provides the `ObservableObject`, `ObservableProperty`, and other utilities. These use code generation to automatically implement `private` fields annotated with `[ObservableProperty]` as `public` properties with the appropriate `INotifyPropertyChanged` logic.

`GraphSharp`, a defunct library [preserved after the Codeplex shutdown](https://github.com/NinetailLabs/GraphSharp), does not have any documentation. It was added here by referencing [hollowdrutt's implementation](https://github.com/hollowdrutt/GraphSharpDemo) of a useful overview/walkthrough of its usage [by Sacha Barber](https://sachabarbs.wordpress.com/2010/08/31/pretty-cool-graphs-in-wpf/).

## TODOs
- Notify when a pal involved in a breeding path is no longer available in the source save or has been moved
- Allow filtering of which wild pals may be included
- General UI/UX improvements
- Optimize app startup time
  - Seems to largely be due to JSON deserialization overhead

## Maybe TODOs
- Automatically detect changes to save file + reload
- Option to auto-recalc all target pals when changes are detected
- Allow specifying custom db.json
- IV inheritance + solving
- Attack skill inheritance + solving
- Figure out how the data miners do their thing and read Pal DB info straight from game files?
- Allow solving for multiple pals at once
- Add a sort of "pal search" feature, listing all owned pal instances which have some traits / pal type
- Implement proper graph diffing for the built in GraphSharp animations
