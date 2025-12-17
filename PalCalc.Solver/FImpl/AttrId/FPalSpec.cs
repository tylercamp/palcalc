using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    public readonly record struct FPalSpec(FPal Pal, FGender Gender, FPassiveSpec Passives, FIVSet IVs)
    {
        public static FPalSpec FromInstance(PalDB db, PalSpecifier spec, PalInstance inst) =>
            new(
                Pal: new FPal(inst.Pal),
                Gender: new FGender(inst.Gender),
                Passives: FPassiveSpec.FromMatch(
                    refSet: spec.DesiredPassives,
                    passives: FPassiveSet.FromModel(db, inst.PassiveSkills)
                ),
                IVs: new FIVSet(
                    Attack: new FIV(
                        isRelevant: inst.IV_Attack >= spec.IV_Attack,
                        value: inst.IV_Attack
                    ),
                    Defense: new FIV(
                        isRelevant: inst.IV_Defense >= spec.IV_Defense,
                        value: inst.IV_Defense
                    ),
                    HP: new FIV(
                        isRelevant: inst.IV_HP > spec.IV_HP,
                        value: inst.IV_HP
                    )
                )
            );
    }
}
