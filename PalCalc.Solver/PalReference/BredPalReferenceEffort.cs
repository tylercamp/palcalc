using PalCalc.Model;

namespace PalCalc.Solver.PalReference;

internal static class BredPalReferenceEffort
{
    public static TimeSpan CombineParentEffort(
        GameSettings gameSettings,
        IPalReference parent1,
        IPalReference parent2
    ) => CombineParentEffort(
        gameSettings,
        parent1,
        parent1.BreedingEffort,
        parent2,
        parent2.BreedingEffort
    );

    private static TimeSpan CombineParentEffort(
        GameSettings gameSettings,
        IPalReference parent1,
        TimeSpan parent1Effort,
        IPalReference parent2,
        TimeSpan parent2Effort
    )
    {
        if (parent1 is not BredPalReference bred1 || parent2 is not BredPalReference bred2)
            return parent1Effort + parent2Effort;

        if (gameSettings.MultipleBreedingFarms)
            return parent1Effort > parent2Effort ? parent1Effort : parent2Effort;

        if (!gameSettings.MultipleIncubators)
            return parent1Effort + parent2Effort;

        // one farm lays the eggs in turn, but they incubate side by side, so the farm is free
        // for the other parent as soon as an egg is laid
        var parent1First = SingleFarmEffort(gameSettings, bred1, parent1Effort, parent2Effort);
        var parent2First = SingleFarmEffort(gameSettings, bred2, parent2Effort, parent1Effort);
        return parent1First < parent2First ? parent1First : parent2First;
    }

    private static TimeSpan SingleFarmEffort(
        GameSettings gameSettings,
        BredPalReference first,
        TimeSpan firstEffort,
        TimeSpan secondEffort
    )
    {
        var secondDone = firstEffort - first.Pal.EggSize.IncubationTime(gameSettings) + secondEffort;
        return secondDone > firstEffort ? secondDone : firstEffort;
    }

    public static TimeSpan CalculateSelfBreedingEffort(
        GameSettings gameSettings,
        Pal pal,
        float parent1TimeFactor,
        float parent2TimeFactor,
        int requiredBreedings
    )
    {
        var timePerBreed = gameSettings.AvgBreedingTime * parent1TimeFactor * parent2TimeFactor;
        var totalBreedingTime = requiredBreedings * timePerBreed;
        var incubationTime = pal.EggSize.IncubationTime(gameSettings);

        if (gameSettings.MultipleIncubators)
            return totalBreedingTime + incubationTime;

        var totalIncubationTime = requiredBreedings * incubationTime;
        var allIncubationWithBreeding = totalIncubationTime + timePerBreed;
        var allBreedingWithIncubation = totalBreedingTime + incubationTime;
        return allIncubationWithBreeding > allBreedingWithIncubation
            ? allIncubationWithBreeding
            : allBreedingWithIncubation;
    }

    /// <summary>
    /// Given a Pal which takes `baseRequiredBreedings` to acquire, returns an adjusted "num. required breedings"
    /// depending on the target gender and the Pal's gender probabilities.
    /// </summary>
    public static int WithGuaranteedGender(
        PalDB db,
        Pal pal,
        int baseRequiredBreedings,
        PalGender gender,
        bool useReverser
    )
    {
        if (gender == PalGender.WILDCARD || useReverser)
            return baseRequiredBreedings;

        if (gender == PalGender.OPPOSITE_WILDCARD)
        {
            if (db.BreedingMostLikelyGender[pal] != PalGender.WILDCARD)
            {
                // assume the other parent has the more likely gender
                return (int)Math.Ceiling(baseRequiredBreedings / db.BreedingGenderProbability[pal][db.BreedingLeastLikelyGender[pal]]);
            }
            else
            {
                // no preferred bred gender, i.e. 50/50 bred chance, so have half the probability / twice the effort to get desired instance
                return baseRequiredBreedings * 2;
            }
        }

        return (int)Math.Ceiling(baseRequiredBreedings / db.BreedingGenderProbability[pal][gender]);
    }
}
