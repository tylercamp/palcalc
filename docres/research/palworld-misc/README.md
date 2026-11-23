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

The maintained explanation is in
[solver README](../../../PalCalc.Solver/README.md#egg-production-time),
because this is PalCalc's effort model. It distinguishes the configurable
base time, parent speed factors, PalCalc's availability allowance, and incubation.
No native timing assessment is currently published.

Reassess when breeding-speed modifiers, farm behavior, or time estimates change.
Trace the egg-created event independently of incubation and separate active
progress from idle/sleep time. Carry forward an estimate only with its assumptions
and provenance; an implementation formula is not proof of the game's formula.

## Adding another topic

Use the [breeding research introduction](../palworld-breeding/README.md) and
[assessment workflow](../palworld-breeding/ASSESSMENT-WORKFLOW.md). Keep the same
separation between the full game model, any proposed PalCalc model, and current
implementation. State whether model derivation or comparison is applicable,
outside the request, or has no existing implementation to compare. Use a section
in the relevant game-version report under
`../palworld-breeding/assessments/<game-version>/`, with conclusions in
`assessment.md` and the scoped model/evidence in `evidence.md`. Link it from the
supporting-mechanics table and record relevant checked-version, game-mechanics or
assessment-scope changes in the changelog. Keep raw generated artifacts under
`_REA-ANALYSIS/runs/` and temporary topic documentation under `_REA-ANALYSIS/txt/`,
following the shared workflow's publication boundary.

State what PalCalc needs, choose the smallest useful scope, and follow the shared
workflow. Start with a section here linking its current explanation and evidence;
add a separate reference page only when that explanation becomes too large.
A completed assessment records the game version/date, supporting build identity,
full scoped model, data contracts, applicable PalCalc model/comparison, checks and
unresolved limits. Use pseudocode, a schema, decision table or state transitions
to fit the question. Do not mark uninvestigated topics as verified or launch new
REA work as part of documentation maintenance alone.
