using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Solver
{
    public enum AttackSkillAvailability
    {
        Available,
        NotInheritable,
        NotKnownByPals
    }

    public class AvailableAttackSkillViewModel(ActiveSkillViewModel vm, AttackSkillAvailability availability)
    {
        public ActiveSkillViewModel Attack => vm;
        public AttackSkillAvailability Availability => availability;

        public bool IsAvailable => Availability == AttackSkillAvailability.Available;
    }
}
