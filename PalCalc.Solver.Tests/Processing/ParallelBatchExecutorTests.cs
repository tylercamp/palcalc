using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class ParallelBatchExecutorTests
{
    [TestMethod]
    public void BreedingSolver_PublishesImmutableStatusSnapshots()
    {
        var configuredSolver = OneStepSolver();
        var statuses = new List<SolverStatus>();
        configuredSolver.Solver.StatusUpdated += statuses.Add;

        SolverTestScenario.Solve(configuredSolver, "Wixen Noct");

        Assert.IsTrue(statuses.Count >= 3);
        Assert.AreEqual(SolverPhase.Initializing, statuses[0].CurrentPhase);
        Assert.AreEqual(0, statuses[0].WorkProcessedCount);
        Assert.AreEqual(SolverPhase.Finished, statuses[^1].CurrentPhase);
        Assert.IsFalse(
            statuses
                .Zip(statuses.Skip(1))
                .Any(pair => ReferenceEquals(pair.First, pair.Second))
        );
    }

    [TestMethod]
    public void BreedingSolver_CancellationDuringBatchSetupReachesFinishedStatus()
    {
        using var cancellation = new CancellationTokenSource();
        var configuredSolver = OneStepSolver();
        var request = RequestFor(configuredSolver);
        var controller = new SolverStateController(
            cancellation.Token
        );
        SolverStatus? finalStatus = null;
        configuredSolver.Solver.StatusUpdated += status =>
        {
            if (status.CurrentPhase == SolverPhase.Breeding)
                cancellation.Cancel();

            if (status.CurrentPhase == SolverPhase.Finished)
                finalStatus = status;
        };

        var result = configuredSolver.Solver.Solve(
            request,
            controller
        );

        Assert.IsNotNull(finalStatus);
        Assert.IsTrue(finalStatus.IsCanceled);
        Assert.IsTrue(result.IsCanceled);
    }

    [TestMethod]
    public void BreedingSolver_PausedWorkersResumeAndComplete()
    {
        using var cancellation = new CancellationTokenSource();
        using var enteredBreeding = new ManualResetEventSlim();
        var configuredSolver = OneStepSolver();
        var controller = new SolverStateController(
            cancellation.Token
        );
        configuredSolver.Solver.StatusUpdated += status =>
        {
            if (status.CurrentPhase == SolverPhase.Breeding)
                enteredBreeding.Set();
        };
        controller.Pause();

        var solveTask = Task.Run(
            () => configuredSolver.Solver.Solve(RequestFor(configuredSolver), controller)
        );

        try
        {
            Assert.IsTrue(enteredBreeding.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(solveTask.Wait(TimeSpan.FromMilliseconds(100)));

            controller.Resume();

            Assert.IsTrue(solveTask.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(solveTask.Result.Results.Count > 0);
        }
        finally
        {
            controller.Resume();
            cancellation.Cancel();
        }
    }

    [TestMethod]
    public void Execute_PropagatesWorkerExceptionToCaller()
    {
        var configuredSolver = OneStepSolver(maxThreads: 2);
        var target = Target();
        var controller = new SolverStateController(
            CancellationToken.None
        );
        var context = SolverRunContext.Create(
            new BreedingSolverRequest(target, configuredSolver.Settings),
            controller
        );
        var healthyReference = new OwnedPalReference(
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            effectivePassives: [],
            effectiveIVs: new IV_Set()
        );
        var frontier = new SearchFrontier(
            target,
            [healthyReference],
            configuredSolver.Settings.MaxThreads,
            controller,
            context.SelectionPolicy
        );
        var expansionContext = new CandidateExpansionContext(
            StepIndex: 0,
            Target: target,
            PreFilter: new CandidatePreFilter(
                target,
                configuredSolver.Settings.MaxEffort,
                context.SelectionPolicy,
                frontier,
                configuredSolver.Settings.DB.PalsById.Keys
            )
        );
        var failingReference = new FailingPalReference(
            "Katress".ToPal(SolverTestScenario.DB)
        );
        var work = new LazyCartesianProduct<IPalReference>(
            [failingReference],
            [healthyReference]
        );
        var executor = new ParallelBatchExecutor(
            context,
            TimeSpan.FromSeconds(1)
        );

        Assert.ThrowsException<TestWorkerException>(
            () => executor.Execute(work, expansionContext, _ => { })
        );
    }

    private static SolverTestScenario.ConfiguredSolver OneStepSolver(int maxThreads = 1) =>
        SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxThreads: maxThreads
        );

    private static BreedingSolverRequest RequestFor(
        SolverTestScenario.ConfiguredSolver configuredSolver
    ) =>
        new(Target(), configuredSolver.Settings);

    private static PalSpecifier Target() =>
        new()
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
        };

    private sealed class TestWorkerException : Exception;

    private sealed class FailingPalReference(Pal pal) : IPalReference
    {
        public Pal Pal { get; } = pal;
        public List<PassiveSkill> EffectivePassives { get; } = [];
        public int EffectivePassivesHash => 0;
        public IV_Set IVs { get; } = new();
        public List<PassiveSkill> ActualPassives { get; } = [];
        public PalGender Gender => PalGender.MALE;
        public float TimeFactor => 1;
        public IPalRefLocation Location => BredRefLocation.Instance;
        public TimeSpan BreedingEffort => TimeSpan.Zero;
        public TimeSpan SelfBreedingEffort => TimeSpan.Zero;
        public int TotalCost => 0;
        public int NumTotalBreedingSteps => 0;
        public int NumTotalEggs => 0;
        public int NumTotalWildPals => 0;

        public bool IsOutdated
        {
            get => throw new TestWorkerException();
            set { }
        }

        public IPalReference WithGuaranteedGender(
            PalDB db,
            PalGender gender,
            bool useReverser
        ) =>
            this;
    }
}
