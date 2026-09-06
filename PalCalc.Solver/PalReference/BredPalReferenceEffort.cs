using PalCalc.Model;

namespace PalCalc.Solver.PalReference;

internal static class BredPalReferenceEffort
{
    public static TimeSpan CombineParentEffort(
        GameSettings gameSettings,
        IPalReference parent1,
        IPalReference parent2,
        TimeSpan parent1Effort,
        TimeSpan parent2Effort
    ) => gameSettings.MultipleBreedingFarms &&
        parent1 is BredPalReference && parent2 is BredPalReference
            ? parent1Effort > parent2Effort ? parent1Effort : parent2Effort
            : parent1Effort + parent2Effort;

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
