# Find All Breedable

The solver answers "how do I breed this Pal with these passives?" The Find All Breedable tool answers the question before that: "what can I reach at all with the Pals I have, and how far away is it?"

## Overview

**Find All Breedable** is in the Tools menu at the top of the window, beside Language, Theme, and Help, which shows while a save is open in the solver. It opens its own window, beside the same Source Pals as the solver page, so the solver can still be used while it's open. It shows every species as a card, grouped by how many breeding steps it takes to reach:

- **Already Owned:** where the owned Pal with the highest IVs is
- **1 to N Step(s):** one parent pair, with each parent's location
- **Not Reachable:** species which the selected Pals can't lead to

A contents list on the left jumps to each group. Searching highlights the matching cards, and pulses each group in the contents which has a match, since its cards might be off-screen. **Show IVs** adds each card's IVs (the best owned Pal's, or the range a bred Pal can have). It's off by default, since the cards build faster without it and the best-IV parents are picked either way.

Passives, attacks, and effort are ignored. The calculation only looks at species, genders, and known IVs. The flower Gumoss never gets a card, since it would look like a duplicate of the normal Gumoss.

It's built on what already exists. It uses the solver page's Source Pals, `BreedingSolverSettings`, the Use Gender Reversers setting, and the gender rules in `BreedingResult.Matches`. It reruns each time it's opened or Run is pressed, it doesn't change the solver page, and nothing is saved to disk.

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
| `PalCalc.UI/ViewModel/Solver/SolverToolsMenuViewModel.cs` | The Tools menu's list of tools and `OpenToolCommand`. |
| `PalCalc.UI/ViewModel/Solver/SolverToolWindowViewModel.cs` | A tool, the shared Source Pals, and the Run command for its window. |
| `PalCalc.UI/ViewModel/Solver/BreedableToolViewModel.cs` | Runs the calculator, and owns groups, cards, search, Show IVs, and loading the cards a few at a time. |
| `PalCalc.UI/View/Main/SolverToolWindow.xaml` and `.cs` | The tool's window: the tool on the left, Source Pals on the right. Closing it works like `SaveInspectorWindow`. |
| `PalCalc.UI/View/Main/BreedableToolView.xaml` | The top box, contents list, cards, hover popup, search pulse, and the startup screen's `LoadingPage` while loading. |
| `PalCalc.UI/View/Main/BreedableToolView.xaml.cs` | Scrolling to a group from the contents, and back to the top for a new run. |
| `PalCalc.UI/View/Main/SourcePalsView.xaml` and `.cs` | The Source Pals filters and players, moved out of `SolverPage.xaml` unchanged so that the tool window can use them too. |
| `PalCalc.UI/View/SolverPage.xaml` | Uses `SourcePalsView` in place of the same markup. |
| `PalCalc.UI/ViewModel/SolverPageViewModel.cs` | Owns the tools and their windows, runs them with the current settings, reruns open ones when the save reloads, and closes them when the page goes away. |
| `PalCalc.UI/View/AppToolbar.xaml` | The Tools menu, between Theme and Help, hidden when there are no tools. |
| `PalCalc.UI/ViewModel/AppToolbarViewModel.cs` | Holds the Tools menu's view model. |
| `PalCalc.UI/ViewModel/AppWindowViewModel.cs` | Gives the Tools menu the solver page's tools when a save opens, and clears them when it closes. |
| `PalCalc.UI.Tests/BreedableToolTests.cs` | Groups, parents, search, Show IVs, and loading. |
| `PalCalc.UI.Tests/SolverToolMenuTests.cs` | The Tools menu's tools and `OpenToolCommand`. |
| `PalCalc.UI/View/Pal/PalIVsView.xaml` and `.cs` | Gained an optional `Columns` property, which defaults to the original 3. |
| `PalCalc.UI/Localization/` | 7 new codes in `LocalizationCodes.resx`, `en.resx`, and `LocalizationCodes.Designer.cs`. |

## Maintenance

**Game updates:** new Pals and breeding data need no changes. A few tests name specific Pals (such as Penking + Bushi = Sibelyx), so a change to those breeding pairs will fail them. Update the expected Pals if that happens.

**Assumptions in the calculator:**

- Every species can hatch as either gender, which is true for all 299 today. A single-gender species would need the bred-Pal logic to change.
- Same-species pairs only produce themselves, which is also true for all 299. If that changes, remove the filter on `candidates`.
- Gender rules come from `BreedingResult.Matches`, and the solver's filtering comes from `BreedingSolverSettings`, so changes to either carry over.
- The flower Gumoss (`PlantSlime_Flower`) is left out of `Entries` and `NotReachable` by `HiddenPalInternalName`, because it shares its name and icon with the normal Gumoss. It's only hidden from the results: an owned one still counts as a tier 0 parent, so a card's parent can be a flower Gumoss, shown as Gumoss. It breeds the same child as the normal Gumoss with all 297 other species today. If it gets its own name or icon, remove the constant.

**Stopping:** each generation has to add a species or a gender, and each of those can only happen once per species, so the loop has a hard limit. Anything added to the loop has to keep that true.

**Concurrency:** all working data is local to `Calculate`, and it only reads settings and the breeding DB, so it's safe while a solver job is running and off the UI thread. The tool runs it with `Task.Run` rather than as a job, because it takes a few tens of milliseconds and `SolverJobViewModel` is tied to solver requests.

**Adding a tool:** implement `ISolverTool`, add a `DataTemplate` for it in `SolverToolWindow.xaml`, and add it to the list in `SolverPageViewModel`. It then appears in the Tools menu.

**Adding text:** new codes go in `LocalizationCodes.resx`, `en.resx`, and `LocalizationCodes.Designer.cs`. Other languages fall back to English until translated.

**UI details which aren't obvious:**

- View models holding `ILocalizedText` need to be plain classes. Records generate `ToString`, `ILocalizedText.ToString` throws, and UI Automation calls it on list items, which crashes the app for accessibility tools.
- `DataTemplate`s with an `x:Key` in `BreedableToolView.xaml` can't also set a shared `DataType`, or the dictionary reports a duplicate key at runtime.
- `IsEnabled` turns off the hover for bred parents. Binding `IsHitTestVisible` instead stops popups opening at all.
- Groups are `GroupBox`es, not `Expander`s, so every group is always laid out and the contents list can scroll to any of them. This also removed two workarounds: the Adonis `Expander` template has an inner `ScrollViewer` which eats wheel events, and its animation re-wrapped the cards.
- The contents list scrolls on selection, for the keyboard, and on click, for clicking the group that's already selected.
- `SearchPulse` is the only search animation. The card borders, card glows, and contents entries all follow its opacity so that they stay in step. It only runs while something is highlighted.
- Show IVs sets the IV `ContentControl`'s content only while it's checked, so unchecked cards don't build the IV views at all. The IV template hides itself when there are no IVs, as on a Not Reachable card.
- The Tools menu's items get `OpenToolCommand` from the `Menu` around them, since their own context is the tool. The menu hides itself when there are no tools, which is everywhere but the solver page.
- The hover popup copies a few lines from the location popup in `BreedingResultView.xaml`, so changes to that popup won't carry over.
- **Loading:** building the cards is what's slow (WPF takes a few ms each, over a second for all ~300), not the calculation. So:
  - The cards are added `CardsPerBatch` at a time, with `Dispatcher.Yield(Background)` in between so that WPF lays them out and the loading dots keep moving.
  - While loading, the cards' `ScrollViewer` is `Hidden`, not `Collapsed`. `Collapsed` skips layout, so all of the cost would land at once when it's shown.
  - Each run clears `Groups` first. Otherwise reopening the tool rebuilds the previous run's cards in one go before the new run can hide them.
  - Hiding the cards also scrolls back to the top and clears the contents selection, so a new run starts at the top.
  - A newer run replaces one still loading.
  - With no WPF dispatcher, as in tests, `OffUIThread` and `NextFrame` run straight through, so a run has finished when it returns.
