## Miscellaneous Comments

The following options were considered (but ignored) for improving accuracy and/or breeding effort of the final results.

### Including self-breeding effort as a frontier-state discriminator (`EffectivePropertiesKey`) and/or as part of the retained-alternative selection process

- The effect of a "Required Gender" constraint depends on the bred pal's self-breeding effort. You can have two pals with the same estimate, but if one pal has a lower self-breeding effort, then required-gender constraints on that pal will be more efficient than the other pal
- Frontier selection only considers the base effort, not potential effort from a later required-gender constraint
- This was [temporarily added and tested](https://github.com/tylercamp/palcalc/issues/95#issuecomment-2585340511). It did not change the final solver results and increased frontier size ~10x

### Tracking attack selections and special cakes exactly, and using them as effective properties

- This would give the most precise and accurate results: like passive and IV inheritance, we'd track all possible combinations with a lossy view via target-filtered attacks
- Due to the 3-attack inheritance limit on individual pals, and the ability to target 6 attacks on the final result, we'd need accurate representations of all possible distributions between parents (unlike passive skill inheritance)
- The growth of this is immediately and obviously untenable for even just 4 target attacks
- We now make some observations and assumptions about optimal-attack paths to simplify:
  - Observations
    - (1) Attack inheritance has relatively low effect on probabilities (inheritance is either 50% or 100%, there's ways to reliably get 100%)
    - (2) You can arbitrarily change the available attacks to inherit on pals
    - (3) The structure of an optimal breeding tree is affected by its most strict requirements ("if something's easy to do, we don't need to spend much time planning for it")
    - (4) Passive and IV inheritance can't be manipulated like attack skills can, so most of the steps in a tree will revolve around satisfying those constraints
    - (5) A target of six specific attacks can only be achieved by breeding from two parents with three specific attacks. An intermediate pal with six specific attacks is useless since only three can be inherited from a single parent; i.e., inheriting more than three attacks is only valuable at the very last step
  - Assumption
    - The majority of optimal attack-solving-enabled trees will begin with a focus on passive and IV inheritance
    - Special cakes, if needed, will always appear towards the end of the tree / near the final result pal
  - Simplification
    - It will suffice for each solver step to simply track the attacks which are reachable from a given step. The actual distribution of attacks amongst the parents can happen at the end _(since attacks will have relatively little effect on the structure in the end)_
    - When distributing attacks, a rough rule of thumb is: assign attacks as late as possible; among those assigned attacks, use special cakes as late as possible
- We approximate with these simplifications by tracking a flattened representation of the available attack types and distribution of attacks ("attack profiles") _on the `BredPalReference`_ rather than retaining and assessing the whole hierarchy when comparing candidates. i.e., we retain just enough information to meaningfully compare at a high level and to make the final assignment decisions at the end of the process

### Tracking breeding estimates for each attack profile

- 1-skill attack inheritance has either a 50% chance or 100% chance to get a specific attack from one of the parents. This would need to be combined with the pal's gender probabilities and self-breeding steps to give an accurate cost estimate when using this pal as a parent
- Accumulating a flattened view of these options would require comparing on self breeding effort and materialized parent effort
- Due to the additional distinct properties, each accumulation of attack profile combinations from parents causes exponential growth of possible profiles on the child
- Even if these flattened views are only maintained on the child pals for deferred evaluation (rather than needing a full parent traversal), the merging and comparison logic quickly dominates the processing time involved for producing and comparing pals
- We compromise with two observations:
  - (1) Attack inheritance is relatively low-cost: at most it adds a 50% probability, compared to IV (e.g. 20%) and passive probability (e.g. 5%). 100% inheritance chance is possible, which isn't true for IVs and passives. The player can manually change the skill pool of the parents, which isn't true for IVs and passives
  - (2) One special cake is used for each breeding attempt; i.e., special cakes, if used, directly correlate with the number of breeding attempts needed
- If we consider attack inheritance probability as _relatively_ trivial, and we note that special cakes directly relate to breeding effort, we can drop other comparison details and use special cakes as the _sole metric_ for comparing search-time attack profile paths. This can give slightly-less-optimal paths, but the performance improvement is substantial enough that it's worth the slight loss in accuracy (perf. comes from less overall data to compute, and fewer allocations to trigger GC)

### Preserving final effort/cake trade-offs

During search, cake count is a useful shortcut for comparing attack options.
But if we keep optimizing only for cakes at the end, we can end up preferring
an extremely slow breeding path just because it saves one cake.

Instead, finalization keeps useful tradeoffs: a faster path can survive alongside
a slower path that uses fewer cakes. If one option takes no more effort and uses
no more cakes, with an improvement in at least one, we can drop the worse option.

We calculate the costs first and only build the trees we keep. Each option saves
its exact parent choices, so building the tree later gives us the same plan we
evaluated. We can also skip evaluating a candidate if another comparable result
is already better than its lower bounds. For effort, we calculate those bounds
without attack rolls, but still include gender adjustments, passive and IV odds,
and the usual sum/max combination of parent effort. The estimated cake counts
give us the lower bound for cakes.

We only compare different candidate trees this way when they match on output
passives, IV ranges, gender, concrete input choices, step count, gold cost,
wild-Pal count, and training requirements. Final result simplification and the
UI also keep different cake totals separate, so a slow option that saves cakes
isn't removed just because a faster option uses more.

This lets us reduce work without adding another slowdown threshold or result
cap. It still takes more work when there are many useful tradeoffs to keep.
And it doesn't change search pruning: a faster path that uses more cakes may
already have been discarded during search, before finalization gets to compare it.
