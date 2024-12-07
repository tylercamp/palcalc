using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

/*
 * TODO
 * 
 * - Consider adding "desired property consolidation" again?
 * - Track + prefer higher IVs even if no min. IVs are set
 * - Add a pause button
 * - Use bitset for grouping instead of hash?
 * - Additional check during breeding step to build up best-result times before merge step
 * - Check for other spots which just use Batched instead of BatchedForParallel
 */

namespace PalCalc.Solver
{
    public enum SolverPhase
    {
        Initializing,
        Breeding,
        Finished,
    }

    public class SolverStatus
    {
        public SolverPhase CurrentPhase { get; set; }
        public int CurrentStepIndex { get; set; }
        public int TargetSteps { get; set; }
        public bool Canceled { get; set; }

        public int CurrentWorkSize { get; set; }

        public int WorkProcessedCount { get; set; }
        public int TotalWorkProcessedCount { get; set; }
    }

    class WorkBatchProgress
    {
        public int NumProcessed;
    }

    public class BreedingSolver
    {
        private static ILogger logger = Log.ForContext<BreedingSolver>();

        // returns number of ways you can choose k combinations from a list of n
        // TODO - is this the right way to use pascal's triangle??
        static int Choose(int n, int k) => PascalsTriangle.Instance[n - 1][k - 1];

        static IV_Range MakeIV(int minValue, int value) =>
            new(
                isRelevant: minValue != 0 && value >= minValue,
                value: value
            );

        GameSettings gameSettings;
        PalDB db;
        List<PalInstance> ownedPals;

        List<Pal> allowedWildPals;
        List<Pal> bannedBredPals;
        int maxBreedingSteps, maxSolverIterations, maxWildPals, maxBredIrrelevantPassives, maxInputIrrelevantPassives;
        TimeSpan maxEffort;
        PruningRulesBuilder pruningBuilder;
        int maxThreads;

        // https://github.com/tylercamp/palcalc/issues/22#issuecomment-2509171056
        // [NumDesired - 1]
        // eg if there's 1 specific type of IV we want, then ivDP[0] gives the probability of getting that IV
        // (not including 50/50 chance of inheriting from specific parent)
        float[] ivDesiredProbabilities;

        /// <param name="db"></param>
        /// <param name="ownedPals"></param>
        /// <param name="maxBreedingSteps"></param>
        /// <param name="maxWildPals"></param>
        /// <param name="maxIrrelevantPassives">
        ///     Max number of irrelevant passive skills from any parents or children involved in the final breeding steps (including target pal)
        ///     (Lower value runs faster, but considers fewer possibilities)
        /// </param>
        /// <param name="maxEffort">
        ///     Effort in estimated time to get the desired pal with the given passive skills. Goes by constant breeding time, ignores hatching
        ///     time, and roughly estimates time to catch wild pals (with increasing time based on paldex number).
        /// </param>
        public BreedingSolver(
            GameSettings gameSettings,
            PalDB db,
            PruningRulesBuilder pruningBuilder,
            List<PalInstance> ownedPals,
            int maxBreedingSteps,
            int maxSolverIterations,
            int maxWildPals,
            List<Pal> allowedWildPals,
            List<Pal> bannedBredPals,
            int maxInputIrrelevantPassives,
            int maxBredIrrelevantPassives,
            TimeSpan maxEffort,
            int maxThreads
        )
        {
            this.gameSettings = gameSettings;
            this.db = db;
            this.pruningBuilder = pruningBuilder;
            this.ownedPals = ownedPals;
            this.maxBreedingSteps = maxBreedingSteps;
            this.maxSolverIterations = maxSolverIterations;
            this.allowedWildPals = allowedWildPals;
            this.bannedBredPals = bannedBredPals;
            this.maxWildPals = maxWildPals;
            this.maxInputIrrelevantPassives = Math.Min(3, maxInputIrrelevantPassives);
            this.maxBredIrrelevantPassives = Math.Min(3, maxBredIrrelevantPassives);
            this.maxEffort = maxEffort;
            this.maxThreads = maxThreads <= 0 ? Environment.ProcessorCount : Math.Clamp(maxThreads, 1, Environment.ProcessorCount);

            // for IV inheritance there's the probability of:
            //
            // 1. the chance of inheriting exactly N IVs
            // 2. the chance of those IVs being what we want

            // (1) is stored in GameConstants and can change depending on game updates
            // (2) is represented in `combinationsProbabilityTable` and is just from the possible combinations,
            //     regardless of game logic

            var combinationsProbabilityTable = new Dictionary<int, Dictionary<int, float>>()
            {
                // 1 inherited
                { 1, new() {
                    { 1, 1.0f / 3.0f }, // 1 desired
                    { 2, 0.0f },        // 2 desired (no way to get 2 if we only inherited 1)
                    { 3, 0.0f },        // 3 desired (...)
                } },

                // 2 inherited
                { 2, new() {
                    { 1, 2.0f / 3.0f }, // 1 desired
                    { 2, 1.0f / 3.0f }, // 2 desired
                    { 3, 0.0f }         // 3 desired (no way to get 3 if we only inherited 2
                } },

                // 3 inherited
                { 3, new() {
                    // 3 inherited means all IVs inherited, doesn't matter what IV we actually wanted, we'll always get it
                    { 1, 1.0f },
                    { 2, 1.0f },
                    { 3, 1.0f }
                } }
            };

            /*
            IV probabilities have similar approach as `ProbabilityInheritedTargetPassives` - a pal will end up inheriting
            exactly 1, 2, or 3 IVs; get the probability of each case combined with the probability of those cases giving us
            what we want
            */
            
            // stores the final probabilities of getting some number of desired IVs
            //
            // (the final real probability will also need to account for 50/50 chance of inheriting the IV from either specific parent)
            this.ivDesiredProbabilities = new float[3];
            for (int i = 0; i < 3; i++) ivDesiredProbabilities[i] = 0.0f;

            for (int numInherited = 1; numInherited <= 3; numInherited++)
            {
                var probabilityInherited = GameConstants.IVProbabilityDirect[numInherited];
                for (int numDesired = 1; numDesired <= 3; numDesired++)
                {
                    var probabilityMatched = combinationsProbabilityTable[numInherited][numDesired];

                    ivDesiredProbabilities[numDesired - 1] += probabilityInherited * probabilityMatched;
                }
            }
        }

        public event Action<SolverStatus> SolverStateUpdated;

        /// <summary>
        /// Creates a list of desired combinations of passives. Meant to handle the case where there are over MAX_PASSIVES desired passives.
        /// The `requiredPassives` should never have more than MAX_PASSIVES due to a check in `SolveFor`, so this logic is only really
        /// necessary if `requiredPassives` + `optionalPassives` brings us over MAX_PASSIVES.
        /// </summary>
        /// <param name="requiredPassives">The list of passives that will be contained in all permutations.</param>
        /// <param name="optionalPassives">The list of passives that will appear at least once across the permutations, if possible.</param>
        /// <returns></returns>
        static IEnumerable<IEnumerable<PassiveSkill>> PassiveSkillPermutations(IEnumerable<PassiveSkill> requiredPassives, IEnumerable<PassiveSkill> optionalPassives)
        {
#if DEBUG && DEBUG_CHECKS
            if (
                requiredPassives.Count() > GameConstants.MaxTotalPassives ||
                requiredPassives.Distinct().Count() != requiredPassives.Count() ||
                optionalPassives.Distinct().Count() != optionalPassives.Count() ||
                requiredPassives.Intersect(optionalPassives).Any()
            ) Debugger.Break();
#endif

            // can't add any optional passives, just return required passives
            if (!optionalPassives.Any() || requiredPassives.Count() == GameConstants.MaxTotalPassives)
            {
                yield return requiredPassives;
                yield break;
            }

            var numTotalPassives = requiredPassives.Count() + optionalPassives.Count();
            // we're within the passive limit, return all passives
            if (numTotalPassives <= GameConstants.MaxTotalPassives)
            {
                yield return requiredPassives.Concat(optionalPassives);
                yield break;
            }

            var maxOptionalPassives = GameConstants.MaxTotalPassives - requiredPassives.Count();
            foreach (var optional in optionalPassives.ToList().Combinations(maxOptionalPassives).Where(c => c.Any()))
            {
                var res = requiredPassives.Concat(optional);
                yield return res;
            }
        }

        // we have two parents but don't necessarily have definite genders for them, figure out which parent should have which
        // gender (if they're wild/bred pals) for the least overall effort (different pals have different gender probabilities)
        (IPalReference, IPalReference) PreferredParentsGenders(IPalReference parent1, IPalReference parent2)
        {
            IEnumerable<IPalReference> ParentOptions(IPalReference parent)
            {
                if (parent.Gender == PalGender.WILDCARD)
                {
                    yield return parent.WithGuaranteedGender(db, PalGender.MALE);
                    yield return parent.WithGuaranteedGender(db, PalGender.FEMALE);
                }
                else
                {
                    yield return parent;
                }
            }

            var optionsParent1 = ParentOptions(parent1);
            var optionsParent2 = ParentOptions(parent2);

            var parentPairOptions = optionsParent1.SelectMany(p1v => optionsParent2.Where(p2v => p2v.IsCompatibleGender(p1v.Gender)).Select(p2v => (p1v, p2v)));

            Func<IPalReference, IPalReference, TimeSpan> CombinedEffortFunc = gameSettings.MultipleBreedingFarms
                ? ((a, b) => a.BreedingEffort > b.BreedingEffort ? a.BreedingEffort : b.BreedingEffort)
                : ((a, b) => a.BreedingEffort + b.BreedingEffort);

            TimeSpan optimalTime = TimeSpan.Zero;
            bool hasNoPreference = true;
            foreach (var (p1, p2) in parentPairOptions)
            {
                var effort = CombinedEffortFunc(p1, p2);
                if (optimalTime == TimeSpan.Zero) optimalTime = effort;
                else if (optimalTime != effort)
                {
                    hasNoPreference = false;

                    if (effort < optimalTime) optimalTime = effort;
                }
            }

            if (hasNoPreference)
            {
                // either there is no preference or at least 1 parent already has a specific gender
                var p1wildcard = parent1.Gender == PalGender.WILDCARD;
                var p2wildcard = parent2.Gender == PalGender.WILDCARD;

                // should we set a specific gender on p1?
                if (p1wildcard && (
                    !p2wildcard || // p2 is a specific gender
                    parent1.BreedingEffort < parent2.BreedingEffort // p1 takes less effort than p2
                ))
                {
                    return (parent1.WithGuaranteedGender(db, parent2.Gender.OppositeGender()), parent2);
                }

                // should we set a specific gender on p2?
                if (p2wildcard && (
                    !p1wildcard || // p1 is a specific gender
                    parent2.BreedingEffort <= parent1.BreedingEffort // p2 takes less effort than p1 (need <= to resolve cases where self-effort is same for both wildcards)
                ))
                {
                    return (parent1, parent2.WithGuaranteedGender(db, parent1.Gender.OppositeGender()));
                }

                // neither parents are wildcards
                return (parent1, parent2);
            }
            else
            {
                return parentPairOptions.First(p => optimalTime == CombinedEffortFunc(p.p1v, p.p2v));
            }
        }

        /// <summary>
        /// Calculates the probability of a child pal with `numFinalPassives` passive skills having the all desired passives from
        /// the list of possible parent passives.
        /// </summary>
        /// 
        /// <param name="parentPassives">The the full set of passive skills from the parents (deduplicated)</param>
        /// <param name="desiredParentPassives">The list of passive skills you want to be inherited</param>
        /// <param name="numFinalPassives">The exact amount of final passive skills to calculate for</param>
        /// <returns></returns>
        /// 
        /// <remarks>
        /// e.g. "if we decide the child pal has N passive skills, what's the probability of containing all of the passives we want"
        /// </remarks>
        /// <remarks>
        /// Should be used repeatedly to calculate probabilities for all possible counts of passive skills (max 4)
        /// </remarks>
        /// 
        float ProbabilityInheritedTargetPassives(List<PassiveSkill> parentPassives, List<PassiveSkill> desiredParentPassives, int numFinalPassives)
        {
            // we know we need at least `desiredParentPassives.Count` to be inherited from the parents, but the overall number
            // of passives must be `numFinalPassives`. consider N, N+1, ..., passives inherited from parents, and an inverse amount
            // of randomly-added passives
            //
            // e.g. we want 4 total passives with 2 desired from parents. we could have:
            //
            // - 2 inherited + 2 random
            // - 3 inherited + 1 random
            // - 4 inherited + 0 random
            //
            // ... each of these has a separate probability of getting exactly that outcome.
            //
            // the final probability for these params (fn args) is the sum

            float probabilityForNumPassives = 0.0f;

            for (int numInheritedFromParent = desiredParentPassives.Count; numInheritedFromParent <= numFinalPassives; numInheritedFromParent++)
            {
                // we may inherit more passives from the parents than the parents actually have (e.g. inherit 4 passives from parents with
                // 2 total passives), in which case we'd still inherit just two
                //
                // this doesn't affect probabilities of getting `numInherited`, but it affects the number of random passives which must
                // be added to each `numFinalPassives` and the number of combinations of parent passives that we check
                var actualNumInheritedFromParent = Math.Min(numInheritedFromParent, parentPassives.Count);

                var numIrrelevantFromParent = actualNumInheritedFromParent - desiredParentPassives.Count;
                var numIrrelevantFromRandom = numFinalPassives - actualNumInheritedFromParent;

#if DEBUG && DEBUG_CHECKS
                if (numIrrelevantFromRandom < 0) Debugger.Break();
#endif

                float probabilityGotRequiredFromParent;
                if (numInheritedFromParent == 0)
                {
                    // would only happen if neither parent has a desired passive

                    // the only way we could get zero inherited passives is if neither parent actually has any passives, otherwise
                    // it (seems to) be impossible to get zero direct inherited passives (unconfirmed from reddit thread)
                    if (parentPassives.Count > 0) continue;

                    // if neither parent has any passives, we'll always get 0 inherited passives, so we'll always get the "required"
                    // passives regardless of the roll for `PassiveProbabilityDirect`
                    probabilityGotRequiredFromParent = 1.0f;
                }
                else if (!desiredParentPassives.Any())
                {
                    // just the chance of getting this number of passives from parents
                    probabilityGotRequiredFromParent = GameConstants.PassiveProbabilityDirect[numInheritedFromParent];
                }
                else if (numIrrelevantFromParent == 0)
                {
                    // chance of getting exactly the required passives
                    probabilityGotRequiredFromParent = GameConstants.PassiveProbabilityDirect[numInheritedFromParent] / Choose(parentPassives.Count, desiredParentPassives.Count);
                }
                else
                {
                    // (available passives except desired)
                    // choose
                    // (required num irrelevant)
                    var numCombinationsWithIrrelevantPassive = (float)Choose(parentPassives.Count - desiredParentPassives.Count, numIrrelevantFromParent);

                    // (all available passives)
                    // choose
                    // (actual num inherited from parent)
                    var numCombinationsWithAnyPassives = (float)Choose(parentPassives.Count, actualNumInheritedFromParent);

                    // probability of those passives containing the desired passives
                    // (doesn't affect anything if we don't actually want any of these passives)
                    // (TODO - is this right? got this simple division from chatgpt)
                    var probabilityCombinationWithDesiredPassives = desiredParentPassives.Count == 0 ? 1 : (
                        numCombinationsWithIrrelevantPassive / numCombinationsWithAnyPassives
                    );

                    probabilityGotRequiredFromParent = probabilityCombinationWithDesiredPassives * GameConstants.PassiveProbabilityDirect[numInheritedFromParent];
                }

#if DEBUG && DEBUG_CHECKS
                if (probabilityGotRequiredFromParent > 1) Debugger.Break();
#endif

                var probabilityGotExactRequiredRandom = GameConstants.PassiveRandomAddedProbability[numIrrelevantFromRandom];
                probabilityForNumPassives += probabilityGotRequiredFromParent * probabilityGotExactRequiredRandom;
            }

            return probabilityForNumPassives;
        }

        /// <summary>
        /// Given the IVs from two parents, returns the probability of inheriting all desired IVs from the parents.
        /// 
        /// A desired IV is determined by whether it's a "valid" (i.e. non-random) IV.
        /// </summary>
        float ProbabilityInheritedTargetIVs(
            IV_IValue A_hp, IV_IValue A_attack, IV_IValue A_defense,
            IV_IValue B_hp, IV_IValue B_attack, IV_IValue B_defense
        )
        {
            // TODO - likely hotpath, optimize?

            IV_IValue[] hps = [A_hp, B_hp];
            IV_IValue[] attacks = [A_attack, B_attack];
            IV_IValue[] defenses = [A_defense, B_defense];

            int numRelevantHP = hps.Count(iv => iv.IsRelevant);
            int numRelevantAttack = attacks.Count(iv => iv.IsRelevant);
            int numRelevantDefense = defenses.Count(iv => iv.IsRelevant);

            int numRequiredIVs = 0;
            if (numRelevantHP > 0) numRequiredIVs++;
            if (numRelevantAttack > 0) numRequiredIVs++;
            if (numRelevantDefense > 0) numRequiredIVs++;

            if (numRequiredIVs == 0) return 1.0f;

            // base probability is the chance of getting the IV categories we want
            float result = ivDesiredProbabilities[numRequiredIVs - 1];

            // even if we got the right IV categories, we might not get the right parents/values
            //
            // for each IV:
            // - if 0 relevant values, we weren't trying to inherit it, no effect
            // - if 1 relevant value, we need to inherit from the right parent, extra 50/50 chance
            // - if 2 relevant values, inheriting from either parent would suffice, no effect
            //
            // so if any IV has just one relevant parent, cut the final probability in half
            if (numRelevantHP == 1) result *= 0.5f;
            if (numRelevantAttack == 1) result *= 0.5f;
            if (numRelevantDefense == 1) result *= 0.5f;

#if DEBUG
            if (result <= 0.0001f) Debugger.Break();
#endif

            return result;
        }

        public List<IPalReference> SolveFor(PalSpecifier spec, CancellationToken token)
        {
            spec.Normalize();

            if (spec.RequiredPassives.Count > GameConstants.MaxTotalPassives)
            {
                throw new Exception("Target passive skill count cannot exceed max number of passive skills for a single pal");
            }

            var statusMsg = new SolverStatus() { CurrentPhase = SolverPhase.Initializing, CurrentStepIndex = 0, TargetSteps = maxSolverIterations, Canceled = token.IsCancellationRequested };
            SolverStateUpdated?.Invoke(statusMsg);

            /* Build the initial list of pals to breed */

            // attempt to deduplicate pals and *intelligently* reduce the initial working set size
            //
            // effectively need to group pals based on their passives, IVs, and gender
            // (though they only count if they're passives/IVs that are desired/in the PalSpecifier)
            //
            // if there are "duplicates", we'll pick based on the pal's IVs and where the pal is stored
            //

            // PalProperty makes it easy to group by different properties
            // (main grouping function)
            var allPropertiesGroupFn = PalProperty.Combine(PalProperty.Pal, PalProperty.RelevantPassives, PalProperty.IvRelevance, PalProperty.Gender);

            // (needed for the last step where we try to combine two pals into one (`CompositePalReference`) if they are
            // different genders but otherwise have all the same properties)
            var allExceptGenderGroupFn = PalProperty.Combine(PalProperty.Pal, PalProperty.RelevantPassives, PalProperty.IvRelevance);

            bool WithinBreedingSteps(Pal pal, int maxSteps) => db.MinBreedingSteps[pal][spec.Pal] <= maxSteps;

            var initialContent = ownedPals
                // skip pals if they can't be used to reach the desired pals (e.g. Jetragon can only be bred from other Jetragons)
                .Where(p => WithinBreedingSteps(p.Pal, maxBreedingSteps))
                // apply "Max Input Irrelevant Passives" setting
                .Where(p => p.PassiveSkills.Except(spec.DesiredPassives).Count() <= maxInputIrrelevantPassives)
                // convert from Model to Solver repr
                .Select(p => new OwnedPalReference( 
                    instance: p,
                    effectivePassives: p.PassiveSkills.ToDedicatedPassives(spec.DesiredPassives),
                    effectiveHp: MakeIV(spec.IV_HP, p.IV_HP),
                    effectiveAttack: MakeIV(spec.IV_Attack, p.IV_Shot),
                    effectiveDefense: MakeIV(spec.IV_Defense, p.IV_Defense)
                ))
                // group pals by their "important" properties and select the "best" pal from each group
                .GroupBy(p => allPropertiesGroupFn(p))
                .Select(g => g
                    .OrderBy(p => p.ActualPassives.Count)
                    .ThenBy(p => PreferredLocationPruning.LocationOrderingOf(p.UnderlyingInstance.Location.Type))
                    .ThenByDescending(p => p.UnderlyingInstance.IV_HP + p.UnderlyingInstance.IV_Shot + p.UnderlyingInstance.IV_Defense)
                    .First()
                )
                // try to consolidate pals which are the same in every way that matters but are opposite genders
                .GroupBy(p => allExceptGenderGroupFn(p))
                .Select(g => g.ToList())
                .SelectMany<List<OwnedPalReference>, IPalReference>(g =>
                {
                    // only one pal in this group, cant turn into a composite, keep as-is
                    if (g.Count == 1) return g;

                    // shouldn't happen, at this point groups should have at most one male and at most one female
                    if (g.Count != 2) throw new NotImplementedException();

                    var malePal = g.SingleOrDefault(p => p.Gender == PalGender.MALE);
                    var femalePal = g.SingleOrDefault(p => p.Gender == PalGender.FEMALE);
                    var composite = new CompositeOwnedPalReference(malePal, femalePal);

                    // the pals are practically the same aside from gender, i.e. they satisfy all the same requirements, but they could
                    // still have different numbers of irrelevant passives.
                    //
                    // if they're *really* the same in all aspects, we can just merge them, otherwise we can merge but
                    // should still keep track of the original pals

                    // (note - these pals weren't combined in earlier groupings since PalProperty.RelevantPassives is intentionally used
                    //         instead of EffectivePassives or ActualPassives)
                    if (malePal.EffectivePassivesHash == femalePal.EffectivePassivesHash)
                    {
                        return [composite];
                    }
                    else
                    {
                        return [
                            malePal,
                            femalePal,
                            composite
                        ];
                    }
                })
                .ToList();

            if (maxWildPals > 0)
            {
                // add wild pals with varying number of random passives
                initialContent.AddRange(
                    allowedWildPals
                        .Where(p => !ownedPals.Any(i => i.Pal == p))
                        .Where(p => WithinBreedingSteps(p, maxBreedingSteps))
                        .SelectMany(p =>
                            Enumerable
                                .Range(
                                    0,
                                    // number of "effectively random" passives should exclude guaranteed passives which are part of the desired list of passives
                                    Math.Max(
                                        0,
                                        maxInputIrrelevantPassives - p.GuaranteedPassiveSkills(db).Except(spec.DesiredPassives).Count()
                                    )
                                )
                                .Select(numRandomPassives => new WildPalReference(p, p.GuaranteedPassiveSkills(db).Intersect(spec.DesiredPassives), numRandomPassives))
                        )
                        .Where(pi => pi.BreedingEffort <= maxEffort)
                );
            }

            var workingSet = new WorkingSet(spec, pruningBuilder, initialContent, maxThreads, token);

            for (int s = 0; s < maxSolverIterations; s++)
            {
                if (token.IsCancellationRequested) break;

                List<WorkBatchProgress> progressEntries = [];

                bool didUpdate = workingSet.Process(work =>
                {
                    logger.Debug("Performing breeding step {step} with {numWork} work items", s+1, work.Count);

                    statusMsg.CurrentPhase = SolverPhase.Breeding;
                    statusMsg.CurrentStepIndex = s;
                    statusMsg.Canceled = token.IsCancellationRequested;
                    statusMsg.CurrentWorkSize = work.Count;
                    statusMsg.WorkProcessedCount = 0;
                    SolverStateUpdated?.Invoke(statusMsg);

                    void EmitProgressMsg(object _)
                    {
                        lock (progressEntries)
                        {
                            var progress = progressEntries.Sum(e => e.NumProcessed);
                            statusMsg.WorkProcessedCount = progress;
                        }
                        SolverStateUpdated?.Invoke(statusMsg);
                    }

                    const int updateInterval = 100;
                    var progressTimer = new Timer(EmitProgressMsg, null, updateInterval, updateInterval);

                    var resEnum = work
                        .BatchedForParallel()
                        .AsParallel()
                        .WithDegreeOfParallelism(maxThreads)
                        .SelectMany(workBatch =>
                        {
                            var progress = new WorkBatchProgress();
                            lock (progressEntries)
                                progressEntries.Add(progress);

                            return workBatch
                                .Tap(_ => progress.NumProcessed++)
                                .TakeWhile(_ => !token.IsCancellationRequested)
                                .Where(p => p.Item1.IsCompatibleGender(p.Item2.Gender))
                                .Where(p => p.Item1.NumWildPalParticipants() + p.Item2.NumWildPalParticipants() <= maxWildPals)
                                .Where(p => p.Item1.NumTotalBreedingSteps + p.Item2.NumTotalBreedingSteps < maxBreedingSteps)
                                .Where(p =>
                                {
                                    var childPals = db.BreedingByParent[p.Item1.Pal][p.Item2.Pal].Select(br => br.Child);

                                    // don't bother checking any pals if it's impossible for them to reach the target within the remaining
                                    // number of iterations
                                    return childPals.Any(c => db.MinBreedingSteps[c][spec.Pal] <= maxSolverIterations - s - 1);
                                })
                                .Where(p =>
                                {
                                    // if we disallow any irrelevant passives, neither parents have a useful passive, and at least 1 parent
                                    // has an irrelevant passive, then it's impossible to breed a child with zero total passives
                                    //
                                    // (child would need to have zero since there's nothing useful to inherit and we disallow irrelevant passives,
                                    //  impossible to have zero since a child always inherits at least 1 direct passive if possible)
                                    if (maxBredIrrelevantPassives > 0) return true;

                                    var combinedPassives = p.Item1.EffectivePassives.Concat(p.Item2.EffectivePassives);

                                    var anyRelevantFromParents = combinedPassives.Intersect(spec.DesiredPassives).Any();
                                    var anyIrrelevantFromParents = combinedPassives.Except(spec.DesiredPassives).Any();

                                    return anyRelevantFromParents || !anyIrrelevantFromParents;
                                })
                                .SelectMany(p =>
                                {
                                    var (parent1, parent2) = p;

                                    // if both parents are wildcards, go through the list of possible gender-specific breeding results
                                    // and modify the parent genders to cover each possible child

#if DEBUG
                                    // (shouldn't happen)

                                    if (parent1.Gender == PalGender.OPPOSITE_WILDCARD || parent2.Gender == PalGender.OPPOSITE_WILDCARD)
                                        Debugger.Break();
#endif

                                    IEnumerable<(IPalReference, IPalReference)> ExpandGendersByChildren()
                                    {
                                        if (parent1.Gender != PalGender.WILDCARD || parent2.Gender != PalGender.WILDCARD)
                                        {
                                            yield return p;
                                        }
                                        else
                                        {
                                            var withModifiedGenders = db.BreedingByParent[parent1.Pal][parent2.Pal].Select(br =>
                                            {
                                                return (
                                                    parent1.WithGuaranteedGender(db, br.RequiredGenderOf(parent1.Pal)),
                                                    parent2.WithGuaranteedGender(db, br.RequiredGenderOf(parent2.Pal))
                                                );
                                            });

                                            foreach (var res in withModifiedGenders)
                                                yield return res;
                                        }
                                    }

                                    return ExpandGendersByChildren();
                                })
                                .Select(p =>
                                {
                                    // arbitrary reordering of (p1, p2) to prevent duplicate results from swapped pairs
                                    // (though this shouldn't be necessary if the `IResultPruning` impls are working right?)
                                    var (parent1, parent2) = p;
                                    if (parent1.GetHashCode() < parent2.GetHashCode()) return (parent1, parent2);
                                    else return (parent2, parent1);
                                })
                                .Select(p => PreferredParentsGenders(p.Item1, p.Item2))
                                .SelectMany(p =>
                                {
                                    var (parent1, parent2) = p;

                                    var parentPassives = parent1.EffectivePassives.Concat(parent2.EffectivePassives).Distinct().ToList();
                                    var possibleResults = new List<IPalReference>();

                                    var passiveSkillPerms = PassiveSkillPermutations(
                                        spec.RequiredPassives.Intersect(parentPassives).ToList(),
                                        spec.OptionalPassives.Intersect(parentPassives).ToList()
                                    ).Select(p => p.ToList()).ToList();

                                    var ivsProbability = ProbabilityInheritedTargetIVs(
                                        parent1.IV_HP, parent1.IV_Attack, parent1.IV_Defense,
                                        parent2.IV_HP, parent2.IV_Attack, parent2.IV_Defense
                                    );

                                    IV_IValue MergeIVs(IV_IValue a, IV_IValue b) =>
                                        (a, b) switch
                                        {
                                            (IV_IValue, IV_IValue) when a.IsRelevant && !b.IsRelevant => a,
                                            (IV_IValue, IV_IValue) when !a.IsRelevant && b.IsRelevant => b,

                                            (IV_IValue, IV_Random) => a,
                                            (IV_Random, IV_IValue) => b,

                                            (IV_Range va, IV_Range vb) => IV_Range.Merge(va, vb),
                                            _ => throw new NotImplementedException()
                                        };

                                    var finalHp = MergeIVs(parent1.IV_HP, parent2.IV_HP);
                                    var finalAttack = MergeIVs(parent1.IV_Attack, parent2.IV_Attack);
                                    var finalDefense = MergeIVs(parent1.IV_Defense, parent2.IV_Defense);

                                    foreach (var targetPassives in passiveSkillPerms)
                                    {
                                        // go through each potential final number of passives, accumulate the probability of any of these exact options
                                        // leading to the desired passives within MAX_IRRELEVANT_PASSIVES.
                                        //
                                        // we'll generate an option for each possible outcome of up to the max possible number of passives, where each
                                        // option represents the likelyhood of getting all desired passives + up to some number of irrelevant passives
                                        var probabilityForUpToNumPassives = 0.0f;

                                        for (int numFinalPassives = targetPassives.Count; numFinalPassives <= Math.Min(GameConstants.MaxTotalPassives, targetPassives.Count + maxBredIrrelevantPassives); numFinalPassives++)
                                        {
#if DEBUG && DEBUG_CHECKS
                                            float initialProbability = probabilityForUpToNumPassives;
#endif

                                            probabilityForUpToNumPassives += ProbabilityInheritedTargetPassives(parentPassives, targetPassives, numFinalPassives);

                                            if (probabilityForUpToNumPassives <= 0)
                                                continue;

#if DEBUG && DEBUG_CHECKS
                                            if (initialProbability == probabilityForUpToNumPassives) Debugger.Break();
#endif

                                            // (not entirely correct, since some irrelevant passives may be specific and inherited by parents. if we know a child
                                            //  may have some specific passive, it may be efficient to breed that child with another parent which also has that
                                            //  irrelevant passive, which would increase the overall likelyhood of a desired passive being inherited)
                                            var newPassives = new List<PassiveSkill>(numFinalPassives);
                                            newPassives.AddRange(targetPassives);
                                            while (newPassives.Count < numFinalPassives)
                                                newPassives.Add(new RandomPassiveSkill());

                                            var res = new BredPalReference(
                                                gameSettings,
                                                db.BreedingByParent[parent1.Pal][parent2.Pal]
                                                    .Single(br => br.Matches(parent1.Pal, parent1.Gender, parent2.Pal, parent2.Gender))
                                                    .Child,
                                                parent1,
                                                parent2,
                                                newPassives,
                                                probabilityForUpToNumPassives,
                                                finalHp,
                                                finalAttack,
                                                finalDefense,
                                                ivsProbability
                                            );

                                            if (!bannedBredPals.Contains(res.Pal))
                                            {
                                                if (res.BreedingEffort <= maxEffort && (spec.IsSatisfiedBy(res) || workingSet.IsOptimal(res)))
                                                    possibleResults.Add(res);
                                            }
                                        }
                                    }

                                    return possibleResults;
                                })
                                .Where(res => res.BreedingEffort <= maxEffort)
                                .ToList();
                        });

                    var res = resEnum.ToList();
                    progressTimer.Dispose();

                    return res;
                });

                if (token.IsCancellationRequested) break;

                lock(progressEntries)
                    statusMsg.WorkProcessedCount = progressEntries.Sum(e => e.NumProcessed);

                statusMsg.TotalWorkProcessedCount += statusMsg.WorkProcessedCount;

                if (!didUpdate)
                {
                    logger.Debug("Last pass found no new useful options, stopping iteration early");
                    break;
                }
            }

            statusMsg.Canceled = token.IsCancellationRequested;
            statusMsg.CurrentPhase = SolverPhase.Finished;
            SolverStateUpdated?.Invoke(statusMsg);

            return workingSet.Result.Select(r =>
            {
                if (spec.RequiredGender != PalGender.WILDCARD)
                    return r.WithGuaranteedGender(db, spec.RequiredGender);
                else
                    return r;

            }).ToList();
        }
    }
}
