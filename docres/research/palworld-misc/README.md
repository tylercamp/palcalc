# Additional Palworld game logic

Some game calculations matter to PalCalc even though they aren't inheritance
rules. This folder covers topics such as converting world positions into map
coordinates and estimating how long parents take to produce an egg.

We review these when reported behavior, code or data changes, or an explicit
request calls for it. A standard breeding review records whether each topic was
reviewed, carried forward, or not assessed; it doesn't rerun every investigation.

## World-map coordinates

PalCalc uses positions from saves to show the in-game map coordinate numbers and
to place markers on its map image. These are two different 2D outputs from a 3D
world position. [MapCoord](../../../PalCalc.Model/BaseInstance.cs) currently ignores
Z and applies an affine transform to X and Y: a calculation that can scale,
rotate, and shift the coordinates.

```text
output_x = matrix[0,0] * world_x + matrix[0,1] * world_y + matrix[0,2]
output_y = matrix[1,0] * world_x + matrix[1,1] * world_y + matrix[1,2]
```

- `UIFromWorldCoord` uses `GameConstants.WorldToMapMatrix` for the coordinate
  numbers shown on the in-game map.
- `NormalizedFromWorldCoord` uses `GameConstants.WorldToImageMatrix` for normalized
  map-image positions. Multiply by the displayed image dimensions for pixels.
  The affine calculation itself does not clamp positions to the image.

[GameConstants](../../../PalCalc.Model/GameConstants.cs) holds the current matrices.
[BuildDBProgram](../../../PalCalc.GenDB/BuildDBProgram.cs) documents their provenance:
world/map/image samples are used by
[MapTransformSolver](../../../PalCalc.GenDB/MapTransformSolver.cs) to fit affine
matrices. [MapReader](../../../PalCalc.GenDB/GameDataReaders/MapReader.cs) reads
map bounds and a region texture, but this alone does not establish the native UI
coordinate formula. Its current scope recognizes `MainMap` and `FirstRegion`.

This description comes from the repository source, reviewed on 2026-10-18. The
published breeding assessments haven't verified the game's map-conversion
formula or checked these calibration constants against a particular build.
Their presence in PalCalc's code doesn't mean they have been verified with REA.

A targeted reassessment should record the executable and map-asset identities,
input/output units, axis swaps/signs/offsets, Z handling, region/texture selection,
and behavior at several known landmarks and boundaries. Preserve samples or
selected native evidence and distinguish fitted approximation from exact formula.

## Egg-production time

The maintained explanation is in
[solver README](../../../PalCalc.Solver/README.md#egg-production-time),
because this is PalCalc's effort model. It distinguishes the configurable
base time, parent speed factors, PalCalc's availability allowance, and incubation.
No native timing assessment is published in the retained breeding reports.

Reassess when breeding-speed modifiers, farm behavior, or time estimates change.
Trace the egg-created event independently of incubation and separate active
progress from idle/sleep time. Carry forward an estimate only with its assumptions
and provenance; an implementation formula is not proof of the game's formula.

## Adding another topic

Use the [breeding research introduction](../palworld-breeding/README.md) and
[assessment workflow](../palworld-breeding/ASSESSMENT-WORKFLOW.md). Keep the same
separation between the full game model, the model used by PalCalc, and the
comparison with the reviewed baseline. Store a new assessment as
`assessments/<date>-build-<id>-<topic>/assessment.md` and `evidence.md`; keep raw
investigation files under the ignored root run directory.

State what PalCalc needs, choose the smallest useful scope, and follow the shared
workflow. Start with a section here linking its current explanation and evidence;
add a separate reference page only when that explanation becomes too large.
A completed assessment records the build/date, full scoped pseudocode, data
contracts, reduced model, comparison, checks, and unresolved limits. Do not mark
uninvestigated topics as verified or launch new REA work as part of documentation
maintenance alone.
