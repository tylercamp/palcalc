using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    // 8 bits = 8 `IS_MATCH` flags. `MatchStore` is a bitfield associated with
    // a specific `refSet` and each bit indicates whether the Nth passive from
    // the set is present. `CountStore` is a basic integer for the total count
    // of passives represented by the spec.
    public readonly record struct FPassiveSpec(byte CountStore, byte MatchStore)
    {
        public static FPassiveSpec FromMatch(FPassiveSet refSet, FPassiveSet passives)
        {
            byte countRes = (byte)passives.Count;
            byte matchRes = 0;

            for (int i = 0; i < 8; i++)
            {
                matchRes <<= 1;

                var p = refSet[i];
                if (!p.IsEmpty && passives.Contains(p))
                    matchRes |= 1;
            }

            return new FPassiveSpec(countRes, matchRes);
        }

        public FPassiveSet ToDesiredSet(FPassiveSet refSet)
        {
            var res = FPassiveSet.Empty;

            byte curMatchStore = MatchStore;
            for (int i = 0; i < 8; i++)
            {
                if ((curMatchStore & 0x80) != 0)
                {
                    res = res.Concat(refSet[i]);
                }

                curMatchStore <<= 1;
            }

            return res;
        }

        public FPassiveSet ToFilteredSet(FPassiveSet refSet)
        {
            var res = ToDesiredSet(refSet);

            var desiredCount = res.Count;
            if (desiredCount != CountStore)
                res = res.Concat(FPassiveSet.RepeatRandom(CountStore - desiredCount));

            return res;
        }

        public int Count => CountStore;

        public int CountRandom
        {
            get
            {
                byte curMatchStore = MatchStore;
                byte numMatched = 0;

                for (int i = 0; i < 8; i++)
                {
                    if ((curMatchStore & 0x80) != 0)
                    {
                        numMatched++;
                    }

                    curMatchStore <<= 1;
                }

                return CountStore - numMatched;
            }
        }
    }
}
