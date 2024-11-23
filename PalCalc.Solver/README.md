# Solving Overview

The solver maintains a list of all "relevant pals" for reaching the target, called the "working set".

It starts from all owned pals and wild pals (if enabled), and filters out duplicate and irrelevant pals. This first step looks for at least one of each pal, each gender, and each subset of the desired passive skills.

Then, for each breeding step, all pals in the working set are combined as parent to produce pals which inherit the desired passives. 

Terms:

- `Desired Passive` - A required or optional passive skill
- `Working Set` - The accumulated list of pals which are being compared. Contains owned, wild, and bred pals. Will only keep the "optimal" (fastest) instance of each pal+gender+passive combo.
- `Pal Reference` - A reference to any type of pal, e.g. owned, bred, and wild. Maintains a breeding effort - an estimated time to acquire that pal - where owned pals have zero effort, wild pals have effort estimated based on their properties (sell price, etc.).
  - Breeding effort for bred pals is based on the effort of its parents and the effort for breeding the child itself.
  - The child's self-breeding effort is calculated during solving.
  - If "Multiple Breeding Farms" is disabled, the total effort of the child is its self-effort + the effort of both parents.
  - If "Multiple Breeding Farms" is enabled, the total effort of the child is its self-effort + the largest effort of either parent.
- `Wildcard Gender` - A pal reference without a designated gender. May be resolved to a specific gender.
- `Gender Resolution` - A pal reference with wildcard gender is copied and the copy is set to a specific gender. The effort estimation of the copy is modified depending on the pal's specific gender probabilities.

### Effort Estimation

When Pal Calc finds a set of parents to breed, it will produce children which inherit all desired passives from the parents. Each child will have an effort estimation, which is the amount of time needed to produce that child which inherits those specific passives.

The effort estimate is largely possible thanks to [this reverse engineering of the Palworld inheritance calculation by /u/mgxts](https://www.reddit.com/r/Palworld/comments/1af9in7/comment/kppjq4n/). It provides the complete process as well as probabilities of inheriting different numbers of passives from parents. (See [the other README in this folder](./README-BREED-ESTIMATE.md) for details on the exact calculation.)

These probabilities are used when estimating breeding effort. If the child has, e.g., an 8% chance of inheriting all the desired passives from its parents, that's roughly a 1 in 13 chance of success, i.e. on average 13 attempts would be needed to produce that child. At 5 minutes per breeding attempt, that comes to ~65 minutes. (Though Pal Calc will internally double the Breeding Time setting - 10 minutes instead of 5 - to account for lost productivity at night.)

Producing a child which _only_ inherits the desired passives may have an 8% chance, but a child with desired passives and one undesired passive may have a 15% chance. Depending on the "Max Bred Irrelevant Passives" setting, Pal Calc may also produce children including these undesired passives which have a higher probability to obtain. Note that while the undesired passive improves the probability of getting that child, it also reduces the probability of passing on the desired passives from that child.

If "Max Bred Irrelevant Passives" is set, Pal Calc will produce multiple children from the parents which include these higher-probability children with irrelevant passives. All of these children will be considered equally in future breeding steps.

### Result Pruning

The largest technical challenge in Pal Calc was reducing runtime, so that solving could complete in minutes rather than months. If we start with 500 pals and breed them, we get approx. 25,000 (500\*500) children. The next breeding step could have 625,000,000 children (25000\*25000), and the step after that would have several trillion children to check. (Numbers not exactly correct, but the amount of work needed after each step would still scale very quickly.)

To get around this Pal Calc will prune its results at each step to reduce the number of breeding calculations it makes. This primarily checks against breeding effort, i.e. a child with a high breeding effort will be ignored if it has already found an equivalent pal (same type of pal, gender, passives) which has lower effort.

This greatly helps, but we still often end up with multiple children which take the same effort. Previous logic would just take the first child that was found, but this added a lot of randomness to the results since the order in which children were produced could change. This would also prevent Pal Calc from offering multiple equivalent paths to the same pal, e.g. in case a pal in the discovered path was being used for something and we didn't want it for breeding. We'd prefer to have consistent results and multiple options for breeding, and there are still other parameters that we might want to consider (e.g. location of pals involved, we'd prefer paths which only use pals in Palboxes.)

When Pal Calc produces children in the breeding process it will _always_ skip children which take more effort / are less efficient than children that were previously found. For cases where multiple children have the same effort, Pal Calc has a set of additional pruning steps to pare down the list:

- Sort by total breeding steps
- Sort by number of players involved
- Sort by locations of pals (prefer palbox over base over party)
- etc.
- ... pick the top 3 from this multi-sorted list

(See [PruningRulesBuilder.cs](./ResultPruning/PruningRulesBuilder.cs).)

This pruning is applied at the end of each breeding step before building the next list of parent pairs for breeding. (See [WorkingSet.cs](./WorkingSet.cs).)

(Unfortunately there is still some randomness even after applying this pruning, since e.g. there may be multiple paths with the same effort estimate, same total breeding steps, same pal locations, etc.)

### Other Notes

There's a common idea that the distribution of desired passives betweens parents, e.g. a 0/4, 1/3, 2/2 split, affects which passives are inherited by the child. This is [apparently false](https://www.reddit.com/r/Palworld/comments/1af9in7/comment/kppjq4n/). Palworld will make a combined, deduplicated list of passives from the parents, and the inherited passives are based on that combined list.

Pal Calc does not have any preference for how passives are distributed amongst parents, etc. when searching through breeding paths. It will always consider _all_ possible breeding combinations between _all_ discovered parents, and will preserve the fastest option it's found, whatever that may be.

The solver was written to try to ensure that it will always output the same results if you use the same save data and settings, but there will still be some amount of randomness in the results it produces [due to the pruning process](#result-pruning).

Regardless of randomness or choices in pruning logic, the main indicator of which options are kept is based on breeding time estimates, so Pal Calc will always output the fastest method. But since there are typically many other breeding paths equivalent to that "fastest method", the specific path shown may change when the solver runs again.

### Detailed Solver Steps

_This may become outdated in later updates._

- **(1) Prepare the starting list of pals, add to the working set**
  - _The list of available pals is reduced to contain one of each pal with a given gender, and a unique subset of the list of desired passives. Pals are filtered based on "Max Input Irrelevant Passives"._
  - _If there are multiple pals with the same pal type, gender, and list of traits, then a single pal will be chosen based on highest IVs and its stored location. It will try to pick pals in your palbox, base, and party, in that order. See `BreedingSolver.RelevantInstancesForPassiveSkills`._
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
  - **(2.3) Reduce and merge the optimized children into the working set**
    - _The full set of discovered children are grouped by pal, gender, and list of passives. If there are multiple results in a group, the list is [pruned](./ResultPruning/PruningRulesBuilder.cs) to take the "best" options from the list._
    - _The pruned set of children are merged into the working set. (2) is repeated using the pruned children and the contents of the working set._
      - _These pruning steps ensure we only consider the "best" options and reduce the total amount of work, which speeds up the solving process. If the working set contains a pal, then that pal is the most optimal way to make that pal that's been found so far._
    - _If no new children were produced, i.e. working set contains all possible optimal results and additional iterations would be pointless, the breeding process exits early._
  - **(2.4) Process repeats, up to "Max Breeding Steps" times**
- **(3.) List of pals matching the target are returned, may be further pruned/filtered by PalCalc.UI**
  - _Results may be grouped using [PalPropertyGrouping](./PalPropertyGrouping.cs), and each group can be reduced using common [pruning](./ResultPruning/PruningRulesBuilder.cs)._
