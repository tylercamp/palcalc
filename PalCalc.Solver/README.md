# Solving Overview

The solver maintains a list of all "relevant pals" for reaching the target, called the "working set".

It starts from all owned pals and wild pals (if enabled), and performs some "pruning" to filter out duplicate and irrelevant pals. This first pruning looks for at least one of each pal, each gender, and each subset of the desired traits.

Then, for each breeding step, all pals in the working set are combined as parent to produce pals which inherit the desired traits.

Terms:

- `Desired Trait` - A required or optional trait
- `Working Set` - The accumulated list of pals which are being compared. Contains owned, wild, and bred pals
- `Pal Reference` - A reference to any type of pal, e.g. owned, bred, and wild. Maintains a breeding effort - an estimated time to acquire that pal - where owned pals have zero effort, wild pals have effort estimated based on their properties (sell price, etc.).
  - Breeding effort for bred pals is based on the effort of its parents and the effort for breeding the child itself.
  - The child's self-breeding effort is calculated during solving.
  - If "Multiple Breeding Farms" is disabled, the total effort of the child is its self-effort + the effort of both parents.
  - If "Multiple Breeding Farms" is enabled, the total effort of the child is its self-effort + the largest effort of either parent.
- `Wildcard Gender` - A pal reference without a designated gender. May be resolved to a specific gender.
- `Gender Resolution` - A pal reference with wildcard gender is copied and the copy is set to a specific gender. The effort estimation of the copy is modified depending on the pal's specific gender probabilities.

**The complete process (may become outdated in later updates):**

- `1.` The pal specifier (target pal) is normalized to remove duplicate traits and any optional traits which are in the list of required traits.
- `2.` The list of available pals is reduced to contain one of each pal with a given gender, and a unique subset of the list of desired traits. Pals are filtered based on "Max Input Irrelevant Traits".
  - If there is a male and female of the same pal with the same set of desired traits, a composite reference is made which combines them and has "wildcard" gender. Both pals, and the composite reference, are preserved in the final list.
- `3.` If "Num Wild Pals" > 0, a wild representation of any unowned pal is added to the list with "wildcard" gender. For each wild pal, multiples of that pal may be added with up to "Max Input Irrelevant Traits" random traits. (Wild pals with more random traits have a higher probability / lower estimated effort.)
- `4.` The working set is initialized with the gathered list of pals.
- `5.` The breeding loop begins and runs up to "Max Breeding Steps" times.
  - `1.` Child pals are collected for the pals currently in the working set.
    - `1.` Get the full list of (parent A, parent B) combinations from the list of available pals in the working set.
    - `2.` Filter the parent pairs based on compatibility and relevance.
      - `1.` Parents with the same gender are skipped.
      - `2.` Bred parents whose combined wild pal participants exceed "Num Wild Pals" are skipped.
      - `3.` Bred parents whose combined breeding steps exceed "Max Breeding Steps" are skipped.
      - `4.` Parents which cannot reach the target pal within the remaining number of breeding steps are skipped.
      - `5.` If "Max Irrelevant Traits" is zero, parents are skipped if either of them have at least one trait and neither parent has any relevant traits. (It's impossible to produce a child with zero traits if either parent has at least one trait.)
    - `3.` "Finalize" the genders of the parents if any are wildcards.
      - `1.` If both parents are wildcards and the type of child pal depends on the gender of the parents (e.g. Katress + Wixen), make two new parent pairs with (Male, Female) and (Female, Male) resolved genders to cover both results.
      - `2.` If either parent is still a wildcard, resolve them to specific genders.
        - If either parent is not a wildcard, the wildcard parent takes the opposite gender.
        - If both parents are wildcards, the parents may be assigned specific genders depending on their pal-specific gender probabilities, or the least-effort parent may be assigned "Opposite Wildcard".
    - `4.` Find the child pal for the parents and create multiple bred pal references depending on the selected trait options.
      - `1.` The list of desired traits is collected from the parents, and a list of permutations of these desired traits (where each permutation still contains all required traits) is made.
      - `2.` For each permutation of traits, create a bred pal reference whose effort is based on the probability of inheriting exactly those traits.
        - Multiple bred pal references may be made depending on "Max Bred Irrelevant Traits", to represent e.g. "exact desired traits", "desired traits + 1 random", etc.
        - The number of irrelevant traits directly affects the child's breeding self-effort. It will also affect the effort of breeding that child in a later breeding step to obtain desired traits.
      - `3.` The working set is checked for matching pals with the same traits. If a pal already exists with the same trait, gender, and takes less effort than this child, the child is skipped.
  - `2.` The full set of discovered children are grouped by pal, gender, and list of traits. If there are multiple results in a group, the list is [pruned](./ResultPruning/PruningRulesBuilder.cs) to take the "best" options from the list.
  - `3.` Any children which match the desired pal are added to a separate list of final results.
  - `4.` The pruned set of children are merged into the working set. (2) is repeated using the pruned children and the contents of the working set.
    - These pruning steps ensure we only consider the "best" options and reduce the total amount of work, which speeds up the solving process.
  - `5.` Newly-discovered children are tracked for breeding and work is queued to breed the children with each other and with the other pals in the working set.
  - `6.` If no new children are produced, i.e. the contents of the working set are already optimal, the breeding loop may exit early.
- `6.` The list of discovered pals which match the desired pal are returned as the list of final results.

Once the solver finishes, the list of results may be further pruned based on [number of breeding steps, number of referenced players, number of wild pals, etc.](./ResultPruning/PruningRulesBuilder.cs)