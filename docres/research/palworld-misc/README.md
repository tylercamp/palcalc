# Additional Palworld game logic

Some game calculations matter to PalCalc even though they aren't inheritance
rules. This folder covers topics such as converting world positions into map
coordinates and estimating how long parents take to produce an egg.

We review these supporting topics in response to reported problems, code or data
changes, or specific requests. Their status is recorded in the
[research checklist](../palworld-breeding/README.md#supporting-mechanics).

## World-map coordinates

PalCalc uses saved positions to display map coordinates and place pins. Both
conversions use world X and Y and were recovered directly from Palworld's
shipped Blueprint code. [MapCoord](../../../PalCalc.Model/BaseInstance.cs)
implements them.

See the [versioned map-coordinate evidence](../palworld-breeding/assessments/1.0.5.102999/evidence.md#world-map-coordinates)
for the calibration inputs, asset fields, and widget call chain.

### Coordinate labels

The game's `WBP_Map_Base_C.PrintPosition` widget sets the scale and origin for
the coordinate labels. PalCalc uses the same formula and rounds the displayed
numbers to whole values.

```text
mapX = (worldY - 158000) / 459
mapY = (worldX + 123888) / 459
```

### Map pin placement

The game's `WorldLocationToWidgetOffset` helper uses the `MainMap` bounds from
`DT_WorldMapUIData` to express pin positions as fractions of the image width and
height. PalCalc applies those positions to the exported `FirstRegion` texture.

```text
u = (worldY + 724400) / 1448800
v = (349400 - worldX) / 1448800
```

## Egg-production time

In Palworld 1.0.5.102999, a normal Breeding Farm requires 300 seconds of active
breeding at the base rate. Parent and camp breeding-speed effects change the rate,
and parent work state gates progress. Cake and available output space are checked
at completion. Day/night has no direct effect on the timer. See the
[game timing assessment](../palworld-breeding/assessments/1.0.5.102999/assessment.md#egg-production-time)
for the timing inputs and asset references.

[PalCalc's timing model](../../../PalCalc.Solver/README.md#egg-production-time)
describes the solver's configurable base time and availability allowance.

Reassess when breeding-speed modifiers, farm behavior, or time estimates change.
Trace the egg-created event independently of incubation and separate active
progress from idle/sleep time. Carry forward an estimate only with its assumptions
and provenance; an implementation formula is not proof of the game's formula.

## Adding another topic

Core mechanics affect the accuracy or completeness of calculations relevant to
the breeding solver. Add them to the required mechanics checklist in the research
hub. Supporting mechanics cover the remaining game behavior documented by the
project. Add them to the supporting mechanics checklist.

Use the [breeding research introduction](../palworld-breeding/README.md) and
[assessment workflow](../palworld-breeding/ASSESSMENT-WORKFLOW.md). Keep the same
separation between the full game model, any proposed PalCalc model, and current
implementation. State whether model derivation or comparison is applicable,
outside the request, or has no existing implementation to compare. Use a section
in the relevant game-version report under
`../palworld-breeding/assessments/<game-version>/`, with conclusions in
`assessment.md` and the scoped model and evidence in `evidence.md`. Use matching
headings. Link the findings from the relevant assessment summary table and the
research hub. Record relevant checked-version, game-mechanics or
assessment-scope changes in the changelog. Keep raw generated artifacts under
`_REA-ANALYSIS/runs/` and temporary topic documentation under `_REA-ANALYSIS/txt/`,
following the shared workflow's publication boundary.

State what PalCalc needs, choose the smallest useful scope, and follow the shared
workflow. Keep the versioned findings in the report. Add a short explanation
and links here when readers need a reference for a supporting calculation.
A completed assessment records the game version/date, supporting build identity,
full scoped model, data contracts, applicable PalCalc model/comparison, checks and
unresolved limits. Use pseudocode, a schema, decision table or state transitions
to fit the question. Do not mark uninvestigated topics as verified or launch new
REA work as part of documentation maintenance alone.
