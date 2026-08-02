using PalCalc.Model;

namespace PalCalc.Solver.PalReference;

public class RandomActiveSkill : ActiveSkill
{
    public RandomActiveSkill() : base("(Random)", "__VIRT_RAND__", null)
    {
        CanInherit = true;
    }

    public override bool Equals(object obj) => ReferenceEquals(this, obj);

    private static ulong randomHash;
    private readonly int hash = (int)(Interlocked.Increment(ref randomHash) % int.MaxValue);
    public override int GetHashCode() => hash;
}
