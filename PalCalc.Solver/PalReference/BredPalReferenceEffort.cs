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

    public static int WithGuaranteedGender(
        int requiredBreedings,
        Pal pal,
        PalDB db,
        PalGender gender,
        bool useReverser
    )
    {
        if (gender == PalGender.WILDCARD || useReverser)
            return requiredBreedings;

        if (gender == PalGender.OPPOSITE_WILDCARD)
            return db.BreedingMostLikelyGender[pal] != PalGender.WILDCARD
                ? (int)Math.Ceiling(requiredBreedings / db.BreedingGenderProbability[pal][db.BreedingLeastLikelyGender[pal]])
                : requiredBreedings * 2;

        return (int)Math.Ceiling(requiredBreedings / db.BreedingGenderProbability[pal][gender]);
    }
}
