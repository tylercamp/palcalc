# Solver Redesign Notes

This document captures the design discussion for a new, optimized solver implementation.

## Goals

1. **Reduce data duplication** - Current flat list stores redundant property data across pal instances
2. **Enable efficient queries** - O(1) lookups instead of O(n) scans for specific property combinations
3. **Defer expensive calculations** - Calculate effort/costs lazily, only when needed
4. **Improve cache locality** - Group similar data together for better CPU cache utilization
5. **Maintain correctness** - Same optimal results as current solver

## Core Design: Hybrid Single-Phase with Lazy Cost Calculation

Instead of the current approach (generate all candidates with full effort, then prune), use:

1. **Hierarchical dictionary storage** for efficient lookups
2. **Paths stored immediately** during discovery
3. **Costs computed on-demand** and cached
4. **Version-based invalidation** for cache coherence

### Why Not Two-Phase?

Initially considered separating "discovery" (Phase 1) and "resolution" (Phase 2), but this introduced complexity:
- Explicit indeterminate vs specific cost tracking
- Manual cost propagation when better paths found
- Separate data structures for each phase

The hybrid approach gets the benefits (deferred calculation, efficient structure) without these complications.

## Data Structures

### Hierarchical Storage

```
PalStore = Dictionary<
    PalId,
    Dictionary<
        Gender,          // MALE | FEMALE | WILDCARD
        Dictionary<
            PassiveSet,
            PalEntry
        >
    >
>
```

**Benefits**:
- O(1) lookup by (Pal, Gender, Passives)
- No linear scans for gender-compatible pairs
- Natural grouping improves cache locality
- Deduplication of shared properties

**Concern**: May need multiple indexes for different access patterns. Options:
1. Primary index + secondary indexes (references only)
2. Lazy projection views
3. Composite keys with partial matching

### PalEntry

```
PalEntry {
    Spec: { Pal, Gender, Passives, IVs }
    Paths: List<Path>

    // Lazy cost tracking
    CostVersion: int = 0
    CachedCost: Cost? = null

    // For reprocessing optimization
    LastModifiedIteration: int = 0
}
```

### Path Types

```
Path =
    | OwnedPath { Instance: PalInstance }
    | WildPath { NumRandomPassives: int }
    | BreedingPath {
        Parent1Spec: PalSpec,
        Parent2Spec: PalSpec,
        // Cached references (lazily resolved)
        Parent1Entry: PalEntry? = null,
        Parent2Entry: PalEntry? = null,
        Parent1CostVersion: int = 0,
        Parent2CostVersion: int = 0
      }
    | SurgeryPath { InputSpec: PalSpec, Operations: List<Op> }
```

**Note**: BreedingPath stores specs (query keys) not concrete references. Parent entries are resolved lazily and cached with version tracking.

## Cost Resolution

### Version-Based Invalidation

Each `PalEntry` has a `CostVersion`. When a new path is added:
1. Set `CostVersion = 0` (invalidate cache)
2. Next `GetCost()` call will recompute

Global `currentVersion` increments each iteration. Cached costs are valid only when `entry.CostVersion == currentVersion`.

### Lazy Resolution

```
func GetCost(entry: PalEntry, currentVersion: int) -> Cost:
    if entry.CachedCost != null and entry.CostVersion == currentVersion:
        return entry.CachedCost  // Cache hit

    minCost = Cost.Max
    for path in entry.Paths:
        pathCost = GetPathCost(path, currentVersion)  // Recursive for breeding paths
        minCost = Min(minCost, pathCost)

    entry.CachedCost = minCost
    entry.CostVersion = currentVersion
    return minCost
```

**Key insight**: Costs are only calculated when needed (target resolution), not during discovery. Most discovered pals will never have their costs calculated.

## Iteration Structure

### Main Loop

```
for iteration in 1..MaxIterations:
    globalVersion++

    // 1. Generate work items (parent pairs to check)
    workItems = GenerateWorkItems(store, iteration)

    // 2. Discover new paths in parallel
    //    Queue results to background pruner
    Parallel.ForEach(workItems, DiscoverChildren)

    // 3. Background pruner does structural checks
    //    Returns accepted paths at iteration end
    newPaths = pruner.Finish()

    // 4. Apply accepted paths to entries
    for (entry, path) in newPaths:
        entry.Paths.Add(path)
        entry.LastModifiedIteration = iteration
        entry.CostVersion = 0  // Invalidate

    if no newPaths:
        break  // Converged
```

### Work Generation (Avoiding Redundant Pairs)

Same optimization as current solver:

```
if iteration == 1:
    return CartesianProduct(allSpecs, allSpecs)
else:
    old = specs where LastModifiedIteration < iteration - 1
    new = specs where LastModifiedIteration == iteration - 1
    return (old × new) ∪ (new × new)
```

### Background Pruner

Accepts candidate (entry, path) pairs via ConcurrentQueue. Performs structural pruning without cost calculation:

- Reject identical paths
- Reject paths strictly dominated by existing paths
- Apply at iteration end to avoid race conditions

## Open Questions / Concerns

### 1. Cost Propagation

When a better path is found for parent X, all children using X have stale cached costs. Current approach: version-based invalidation catches this on next access.

**Potential issue**: If we prune children based on cost before their parents are optimized, we might discard paths that would have been optimal.

**Mitigation**: Don't do cost-based pruning during discovery. Only prune structurally. Cost-based pruning happens at final resolution.

### 2. Multiple Index Requirements

May need to query by different property combinations:
- By (Pal, Gender) - for finding breeding partners
- By (Passives) - for finding pals with specific traits
- By (Pal) alone - for surgery inputs

**Options**:
- Multiple dictionaries with shared PalEntry references
- Single primary index, accept O(n) for rare query patterns
- Composite index structure

### 3. IVs in the Hierarchy

Current design has IVs inside PalEntry.Spec but not as a dictionary key. This means entries with same (Pal, Gender, Passives) but different IVs are... the same entry?

**Need to clarify**: Should IVs be part of the grouping key, or handled differently?

### 4. Wildcard Gender Handling

Current solver uses WILDCARD and OPPOSITE_WILDCARD genders. How does this fit into the dictionary structure?

**Options**:
- WILDCARD as a separate gender key (alongside MALE/FEMALE)
- Store once, resolve during cost calculation
- Don't store wildcards in main structure, resolve on-the-fly

### 5. MaxBredIrrelevantPassives

Current solver generates multiple children with varying irrelevant passive counts. Each would be a separate PalEntry (different PassiveSet).

This is probably fine - they're legitimately different targets with different costs.

## Performance Expectations

### Improvements Over Current Solver

1. **No effort calculation during discovery** - Only calculate when resolving final results
2. **O(1) partner lookups** - No scanning for gender-compatible pals
3. **Reduced memory churn** - Shared structure, less duplication
4. **Better cache locality** - Similar data grouped together

### Potential Regressions

1. **Dictionary overhead** - More complex structure than flat list
2. **Version checking** - Small overhead on every cost access
3. **Implementation complexity** - More moving parts to debug

## Implementation Notes

### From Current Solver (Keep)

- Probability calculations (Passives.cs, IVs.cs) - well-tested, keep as-is
- Breeding database lookups - already efficient
- Object pooling for hot paths - may still be needed

### From Current Solver (Change)

- WorkingSet flat structure → Hierarchical PalStore
- Eager effort calculation → Lazy with caching
- BredPalReference with concrete parents → BreedingPath with specs
- Multi-stage pruning → Structural pruning during discovery, cost pruning at end

### FImpl Types

The FImpl types (FPassiveSet, FIVSet, etc.) were added for performance optimization. These should integrate well with the new design - they're already ID-based for efficient comparison and hashing.

## Next Steps

1. Define concrete type signatures for PalStore, PalEntry, Path variants
2. Implement hierarchical storage with basic operations
3. Port discovery logic (breeding pair generation, child creation)
4. Implement lazy cost resolution
5. Add background pruner
6. Test against current solver for correctness
7. Benchmark for performance comparison
