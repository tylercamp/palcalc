# Combos By Child

Find All Breedable answers "what can I reach with the Pals I have?" Combos By Child answers the next question: "for this one Pal, which pairs of parents breed it, and which of them do I already own?"

## Overview

**Combos By Child** is the second entry in the Tools section. Pick a child Pal in the top box, and every pair of parent species which breeds it is shown as a card, split into three sections:

- **Ready Now:** both parents are owned and can be paired right now
- **Breed or Catch One First:** one parent is owned
- **Neither Parent Owned:** collapsed to start with

An owned parent shows the Pal with the highest IVs in the gender needed, and where it is, with the same hover as Find All Breedable. A parent which isn't owned says **Can be bred** (the owned Pals can lead to it), **Catch only** (it only breeds from itself, like Frostallion), or **Not breedable from your Pals**. When both parents are owned, the card also shows the IV range the child can have.

A **Sort** dropdown orders the cards inside each section by Highest IVs, A to Z, Z to A, the highest HP, ATK or DEF, or Most Philanthropist. With reversers on, a parent of the wrong gender for the pair gets the swap icon from `PalGenderView`.

How many breeding steps a missing parent takes isn't shown, to keep the sections simple. Attacks are ignored, and so are passives except Philanthropist, and nothing is saved to disk. The child is cleared each time the tool is selected, and the sort is kept until another save is opened or the app closes. Pairs with the flower Gumoss are hidden unless one is owned, since it breeds the same children as the normal Gumoss and would look like a duplicate card.

## How it works

`ParentCombosCalculator` takes the same `BreedingSolverSettings` as the solver, and runs `BreedableCalculator` first so that the two tools always agree on what can be bred. For each breeding entry whose child is the chosen Pal:

1. Try each gender order the entry allows, using `BreedingResult.Matches`.
2. For each parent, pick the best owned Pal in the gender needed, the same way Find All Breedable does. A pair of the same species needs two different Pals.
3. Work out each parent's `ParentAvailability`:
   - `Owned` when a Pal was picked.
   - `Breedable` when `BreedableEntry.BothGendersTier` is set. That covers a species which isn't owned, the missing gender of one which is, and a second Pal of a species where only one is owned.
   - `CatchOnly` when every breeding entry for that species has it as both parents.
   - `NotBreedable` otherwise.
4. Keep the gender order with the most owned parents, then the fewest gender reversers, then (with the Philanthropist sort) the most Philanthropist parents, then the most breedable ones, then the highest IV total.

**Genders:** with reversers on, any owned Pal counts. With reversers off, only one of the gender needed counts, so a pair of two males you own lands in a lower section, the same rule Find All Breedable uses.

**IVs:** the child range is `IV_Value.Merge` of the two owned Pals. A caught or bred parent's IVs aren't known, so there's no range unless both are owned. Sorting uses the child range, or the one owned parent's IVs.

**Philanthropist:** it halves the breeding time, so any owned parent with it shows a small `HandHeart` icon in the passive's rank color after its name and gender, with the passive's name on hover, in every sort. It sits in the name row so every card keeps the same height. The **Most Philanthropist** sort puts pairs with two Philanthropist parents first, then one, then none, with Highest IVs after that. While it's chosen, each owned parent is picked as your best Philanthropist Pal of the species instead of the highest-IV one, but still in the gender needed first, so a wrong-gender Philanthropist isn't picked over a Pal that needs no reverser. Other sorts pick as before. The check is the solver's own (`ToTimeFactor`, the `BreedSpeed` effect), wrapped in `ParentCombosCalculator.HasBreedSpeed`, so it follows any change to that passive's data.

## File breakdown

### Solver

| File | Purpose |
|---|---|
| `PalCalc.Solver/ParentCombosCalculator.cs` | The calculation. Its inputs are `BreedingSolverSettings` and the child. |
| `PalCalc.Solver/ParentCombosResult.cs` | `ParentAvailability`, `ParentCombo` and `ParentCombosResult`. |
| `PalCalc.Solver/BreedableCalculator.cs` | Records `BothGendersTier`, and shares `BestOwned` (with an optional preferred-Pal check, for Philanthropist), `ExactIVs` and `TotalIVs` with the new calculator. `HiddenPalInternalName` is now public, for the child dropdown. |
| `PalCalc.Solver/BreedableResult.cs` | `BreedableEntry` gained `BothGendersTier`. |
| `PalCalc.Solver.Tests/ParentCombosCalculatorTests.cs` | Sections, genders, gender-specific pairs, same-species pairs, catch-only parents, Philanthropist picks and counts, and a check of every pair of every child. |

### UI

| File | Purpose |
|---|---|
| `PalCalc.UI/ViewModel/Solver/ParentCombosToolViewModel.cs` | The child, sort options, sections and cards, loaded a few at a time like Find All Breedable. `Sorted` holds the sort orders. |
| `PalCalc.UI/View/Main/ParentCombosToolView.xaml` and `.cs` | The top box, cards, and the startup screen's `LoadingPage` while loading. Scrolling reuses `BreedableToolView`'s helpers. |
| `PalCalc.UI/ViewModel/Solver/BreedableToolViewModel.cs` | `BreedableParentViewModel` gained an optional required gender (for `NeedsReverser`) and hint, and `BreedSpeedPassive` for the Philanthropist icon. Find All Breedable passes neither and doesn't show the icon. |
| `PalCalc.UI/View/Main/BreedableToolView.xaml.cs` | `UpdateScrollBar` and `ScrollGroups` became `internal static` so both tools share them. |
| `PalCalc.UI/ViewModel/Solver/ISolverTool.cs` | Gained `OnSelected`, with an empty default, called before `Run` each time a tool is selected. |
| `PalCalc.UI/View/Main/ToolListView.xaml` | Each tool is a bordered entry, now that there's more than one. |
| `PalCalc.UI/ViewModel/SolverPageViewModel.cs` and `View/SolverPage.xaml` | Add the tool and its `DataTemplate`, and call `OnSelected`. |
| `PalCalc.UI/Localization/` | 17 new codes in `LocalizationCodes.resx`, `en.resx`, and `LocalizationCodes.Designer.cs`. |
| `PalCalc.UI.Tests/ParentCombosToolTests.cs` | Sections, hints, the reverser icon, the Philanthropist pick, the child and sort, and every sort order. |

## Maintenance

**Game updates:** new Pals and breeding data need no changes. Some tests name specific Pals (Penking + Bushi = Sibelyx, Katress + Wixen, Faleris + Jormuntide = Faleris Aqua), so a change to those pairs will fail them.

**Assumptions:**

- Each pair of species appears once per child in `PalBreedingDB.Breeding`, true for all 44,851 entries today. If both orders ever appear, each would get its own card.
- `CatchOnly` is worked out from the breeding data, not from spawn data. 26 species only breed from themselves today, including the legendaries. One which can't actually be caught would still say "Catch only", and that hasn't been checked against spawn data.
- Which species can be bred comes from `BreedableCalculator`, so changes to its rules carry over to the hints.

**UI details:** the parent hover, card styling, scrolling, and loading follow Find All Breedable, so its notes in [README-BREEDABLE.md](./README-BREEDABLE.md) apply here too. Here the loading runs on every child or sort change, and a newer change replaces one still loading. The child dropdown only creates items for the visible rows, so UI Automation has to type into it before choosing an item.
