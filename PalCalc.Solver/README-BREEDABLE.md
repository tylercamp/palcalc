# Find All Breedable

The solver answers "how do I breed this Pal with these passives?" The Find All Breedable tool answers the question before that: "what can I reach at all with the Pals I have, and how far away is it?"

## Overview

A **Tools** section sits under Target Pals. Selecting **Find All Breedable** replaces the center pane and shows every species as a card, grouped by how many generations of breeding it takes to reach:

- **Already Owned:** tier 0, showing the Pal with the highest IVs and where it is
- **Tier 1 to N:** the IV range available, and one parent pair with each parent's location
- **Not Reachable:** species which the selected Pals can't lead to

Passives, attacks, and effort are ignored. The calculation only looks at species, genders, and known IVs.

It's built on what already exists. It uses the existing Run command, Source Pals, `BreedingSolverSettings`, the Use Gender Reversers setting, and the gender rules in `BreedingResult.Matches`. Selecting a tool only changes what's visible, so target mode isn't affected, and nothing is saved to disk.

## How it works

`BreedableCalculator` starts with every owned species at tier 0, and then repeats one generation at a time:

1. Go through every pair in `PalBreedingDB.Breeding`, skipping same-species pairs since they only produce themselves.
2. Skip the pair if either parent isn't available yet, or if their genders can't work.
3. A child which is a new species joins the next tier.
4. A child which is already known, but is missing a gender, gets that gender.
5. Apply the new species and genders once the whole generation is done, and stop if there weren't any.

New discoveries are only applied at the end of each generation, otherwise a Pal found in one generation could be used in that same generation and get the wrong tier.

**Genders:** with gender reversers, any two different species can pair. Without them, an owned species only has the genders which are actually owned, until it can be bred for the other gender. Bred species have both genders, since every species can hatch as either gender.

This means a generation can add a gender without adding a species, so a tier can be empty. For example, with a male Anubis, a female Elgrove Cryst, and a male Dualith, the first generation only produces a female Anubis, and Astegon is reached in the second. Empty tiers aren't shown.

**Parents:** each owned parent is the Pal with the required gender and the highest IVs, comparing the same HP + attack + defense total as `InitialPalBuilder`. With gender reversers, if the required gender isn't owned, it falls back to the highest IVs of the other gender. Parents are chosen when the child is found, using the genders available at that point.

**IV ranges:** a child's range is `IV_Value.Merge` of its two parents, using the chosen owned Pal or a bred parent's own range.

## File breakdown

### Solver

| File | Purpose |
|---|---|
| `PalCalc.Solver/BreedableCalculator.cs` | The calculation. Its only input is `BreedingSolverSettings`. |
| `PalCalc.Solver/BreedableResult.cs` | `BreedableEntry` and `BreedableResult`. |
| `PalCalc.Solver.Tests/BreedableCalculatorTests.cs` | Tiers, genders, parent selection, and IV ranges. |
| `PalCalc.Solver.Tests/BreedableCalculatorEdgeCaseTests.cs` | Made-up breeding data which tries to hang it, and invariants across random collections. |
| `PalCalc.Solver.Tests/SolverTestScenario.cs` | Gained optional `useGenderReversers` and `breedingDB` parameters. |

### UI

| File | Purpose |
|---|---|
| `PalCalc.UI/ViewModel/Solver/ISolverTool.cs` | What a tool provides, and the steps for adding one. |
| `PalCalc.UI/ViewModel/ToolListViewModel.cs` | The available tools and the selected tool. |
| `PalCalc.UI/ViewModel/Solver/BreedableToolViewModel.cs` | Runs the calculator, and owns groups, cards, search, and Expand All. |
| `PalCalc.UI/View/Main/ToolListView.xaml` | The list in the Tools section. |
| `PalCalc.UI/View/Main/BreedableToolView.xaml` | The top box, cards, hover popup, and search pulse. |
| `PalCalc.UI/View/Main/BreedableToolView.xaml.cs` | Mouse wheel and scrollbar handling. |
| `PalCalc.UI.Tests/BreedableToolTests.cs` | Groups, parents, search, and Expand All. |
| `PalCalc.UI/View/SolverPage.xaml` | Adds the Tools section and tool host. Hides panels while a tool is selected. |
| `PalCalc.UI/ViewModel/SolverPageViewModel.cs` | Owns the tools, keeps tool and target selection exclusive, and hands off in `RunSolver`. |
| `PalCalc.UI/View/Pal/PalIVsView.xaml` and `.cs` | Gained an optional `Columns` property, which defaults to the original 3. |
| `PalCalc.UI/Localization/` | 8 new codes in `LocalizationCodes.resx`, `en.resx`, and `LocalizationCodes.Designer.cs`. |

## Maintenance

**Game updates:** new Pals and breeding data need no changes. A few tests name specific Pals (such as Penking + Bushi = Sibelyx), so a change to those breeding pairs will fail them. Update the expected Pals if that happens.

**Assumptions in the calculator:**

- Every species can hatch as either gender, which is true for all 299 today. A single-gender species would need the bred-Pal logic to change.
- Same-species pairs only produce themselves, which is also true for all 299. If that changes, remove the filter on `candidates`.
- Gender rules come from `BreedingResult.Matches`, and the solver's filtering comes from `BreedingSolverSettings`, so changes to either carry over.

**Stopping:** each generation has to add a species or a gender, and each of those can only happen once per species, so the loop has a hard limit. Anything added to the loop has to keep that true.

**Concurrency:** all working data is local to `Calculate`, and it only reads settings and the breeding DB, so it's safe while a solver job is running. It runs directly rather than as a job because it takes a few tens of milliseconds, and `SolverJobViewModel` is tied to solver requests.

**Adding a tool:** implement `ISolverTool`, add a `DataTemplate` for it in `SolverPage.xaml`, and add it to the list in `SolverPageViewModel`.

**Adding text:** new codes go in `LocalizationCodes.resx`, `en.resx`, and `LocalizationCodes.Designer.cs`. Other languages fall back to English until translated.

**UI details which aren't obvious:**

- View models holding `ILocalizedText` need to be plain classes. Records generate `ToString`, `ILocalizedText.ToString` throws, and UI Automation calls it on list items, which crashes the app for accessibility tools.
- `DataTemplate`s with an `x:Key` in `BreedableToolView.xaml` can't also set a shared `DataType`, or the dictionary reports a duplicate key at runtime.
- `IsEnabled` turns off the hover for bred parents. Binding `IsHitTestVisible` instead stops popups opening at all.
- The Adonis `Expander` template has a `ScrollViewer` which never scrolls but still handles wheel events, so `Groups_PreviewMouseWheel` scrolls the outer one first.
- `UpdateScrollBar` decides on the outer scrollbar before a group animates, otherwise it appears partway through and re-wraps the cards at the end. It reads the `HeaderSite` and `ExpandSite` parts of the Adonis template. If those are renamed the heights read as 0, which falls back to `Auto` and brings the re-wrap back without breaking anything else.
- `SearchPulse` is the only search animation. The card borders, card glows, and group headers all follow its opacity so that they stay in step. It only runs while something is highlighted.
- Collapsed group headers wait 0.25 seconds to pulse, since the `Expander` collapse takes 0.2 seconds and the cards are still visible until then.
- The hover popup copies a few lines from the location popup in `BreedingResultView.xaml`, so changes to that popup won't carry over.
