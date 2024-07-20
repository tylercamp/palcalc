# Solving Overview

The solver maintains a list of all "relevant pals" for reaching the target, called the "working set".

It starts from all owned pals and wild pals (if enabled), and performs some "pruning" to filter out duplicate and irrelevant pals. This first pruning looks for at least one of each pal, each gender, and each subset of the desired passive skills.

Then, for each breeding step, all pals in the working set are combined as parent to produce pals which inherit the desired passives.

Terms:

- `Desired Passive` - A required or optional passive skill
- `Working Set` - The accumulated list of pals which are being compared. Contains owned, wild, and bred pals. Will only keep the "optimal" instance of each pal+gender+passive combo.
- `Pal Reference` - A reference to any type of pal, e.g. owned, bred, and wild. Maintains a breeding effort - an estimated time to acquire that pal - where owned pals have zero effort, wild pals have effort estimated based on their properties (sell price, etc.).
  - Breeding effort for bred pals is based on the effort of its parents and the effort for breeding the child itself.
  - The child's self-breeding effort is calculated during solving.
  - If "Multiple Breeding Farms" is disabled, the total effort of the child is its self-effort + the effort of both parents.
  - If "Multiple Breeding Farms" is enabled, the total effort of the child is its self-effort + the largest effort of either parent.
- `Wildcard Gender` - A pal reference without a designated gender. May be resolved to a specific gender.
- `Gender Resolution` - A pal reference with wildcard gender is copied and the copy is set to a specific gender. The effort estimation of the copy is modified depending on the pal's specific gender probabilities.

**The complete process (may become outdated in later updates):**

- **(1) Prepare the starting list of pals, add to the working set**
  - _The list of available pals is reduced to contain one of each pal with a given gender, and a unique subset of the list of desired passives. Pals are filtered based on "Max Input Irrelevant Passives"._
  - _If there is a male and female of the same pal with the same set of desired passives, a composite reference is made which combines them and has "wildcard" gender. Both pals, and the composite reference, are preserved in the final list._
  - _If "Num Wild Pals" > 0, a wild representation of any unowned pal is added to the list with "wildcard" gender. For each wild pal, multiples of that pal may be added with up to "Max Input Irrelevant Passives" random passives. (Wild pals with more random passives have a higher probability / lower effort to acquire.)_
- **(2) Begin breeding loop**
  - **(2.1) Build the list of child pals from the working set**
     - **(2.1.1) Filter parent pairs**
       - _Parents with the same gender are skipped._
       - _Bred parents whose combined wild pal participants exceed "Num Wild Pals" are skipped._
       - _Bred parents whose combined breeding steps exceed "Max Breeding Steps" are skipped._
       - _Parents which cannot reach the target pal within the remaining number of breeding steps are skipped._
       - _If "Max Irrelevant Passives" is zero, parents are skipped if either of them have at least one passive and neither parent has any relevant passives. (It's impossible to produce a child with zero passives if either parent has at least one passive.)_
     - **(2.1.2) Resolve parent genders for bred + wild pals with "wildcard" gender**
       - _If both parents are wildcards and the type of child pal depends on the gender of the parents (e.g. Katress + Wixen), make two new parent pairs with (Male, Female) and (Female, Male) resolved genders to cover both results._
       - _If either parent is still a wildcard, resolve them to specific genders._
         - _If either parent is not a wildcard, the wildcard parent takes the opposite gender._
         - _If both parents are wildcards, the parents may be assigned specific genders depending on their pal-specific gender probabilities, and/or the least-effort parent may be assigned "Opposite Wildcard". See `BreedingSolver.PreferredParentGenders`._
     - **(2.1.3) Create list of relevant children which inherit desired passives, calculate probability of producing a child with those passives**
       - _The list of desired passives is collected from the parents, and a list of permutations of these desired passives (where each permutation still contains all required passives) is made. (This is mainly to account for optional passives.)_
       - _For each permutation of passives, create a bred pal reference whose effort is based on the probability of inheriting exactly those passives._
         - _Multiple bred pal references may be made depending on "Max Bred Irrelevant Passives", to represent e.g. "exact desired passives", "desired passives + 1 random", etc._
         - _The number of irrelevant passives directly affects the child's breeding self-effort. It will also affect the effort of breeding that child in a later breeding step to obtain desired passives. See `BreedingSolver.ProbabilityInheritedTargetPassiveSkills.`_
       - _Any children which match the desired pal are added to a separate list of final results._
     - **(2.1.4) "Non-optimal" children are skipped**
       - _The working set is checked for matching pals with the same passives. If a pal already exists with the same passive, gender, and takes less effort than this child, the child is skipped._
       - _This only checks against equivalent pals in the working set. Other pals produced within this breeding step will be filtered later._
  - **(2.2) Collect any children which match the target**
  - **(2.2) Reduce and merge the optimized children into the working set**
    - _The full set of discovered children are grouped by pal, gender, and list of passives. If there are multiple results in a group, the list is [pruned](./ResultPruning/PruningRulesBuilder.cs) to take the "best" options from the list._
    - _The pruned set of children are merged into the working set. (2) is repeated using the pruned children and the contents of the working set._
      - _These pruning steps ensure we only consider the "best" options and reduce the total amount of work, which speeds up the solving process. If the working set contains a pal, then that pal is the most optimal way to make that pal that's been found so far._
    - _If no new children were produced, i.e. working set contains all possible optimal results and additional iterations would be pointless, the breeding process exits early._
  - **(2.4) Process repeats, up to "Max Breeding Steps" times**
- **(3.) List of pals matching the target are returned, may be further pruned/filtered by PalCalc.UI**
  - _Results may be grouped using [PalPropertyGrouping](./PalPropertyGrouping.cs), and each group can be reduced using common [pruning](./ResultPruning/PruningRulesBuilder.cs)._
