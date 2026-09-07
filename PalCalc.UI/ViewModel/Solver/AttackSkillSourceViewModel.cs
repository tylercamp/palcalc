using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Solver
{
    public partial class AttackSkillSourceViewModel : ObservableObject
    {
        public AttackSkillSourceViewModel(PalSourceViewModel palSource)
        {
            PropertyChangedEventManager.AddHandler(palSource, PalSourcePalsChanged, nameof(palSource.AvailablePals));
            Attacks = CollectAttacks(palSource).ToList();
        }

        private void PalSourcePalsChanged(object sender, PropertyChangedEventArgs args)
        {
            Attacks = CollectAttacks(sender as PalSourceViewModel).ToList();

            OnPropertyChanged(nameof(AvailableAttacks));
            OnPropertyChanged(nameof(InheritableAttacks));
        }

        private static IEnumerable<AvailableAttackSkillViewModel> CollectAttacks(PalSourceViewModel palSource)
        {
            var knownAttacks = new HashSet<ActiveSkill>(palSource.AvailablePals.SelectMany(p => p.ActiveSkills));

            foreach (var attack in ActiveSkillViewModel.All)
            {
                if (!attack.ModelObject.CanInherit)
                    yield return new AvailableAttackSkillViewModel(attack, AttackSkillAvailability.NotInheritable);

                else if (!knownAttacks.Contains(attack.ModelObject))
                    yield return new AvailableAttackSkillViewModel(attack, AttackSkillAvailability.NotKnownByPals);

                else
                    yield return new AvailableAttackSkillViewModel(attack, AttackSkillAvailability.Available);
            }
        }

        [ObservableProperty]
        private List<AvailableAttackSkillViewModel> attacks;

        public IEnumerable<AvailableAttackSkillViewModel> InheritableAttacks =>
            Attacks.Where(a => a.Availability != AttackSkillAvailability.NotInheritable);

        public IEnumerable<ActiveSkillViewModel> AvailableAttacks =>
            Attacks.Where(a => a.IsAvailable).Select(a => a.Attack);
    }
}
