using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.ViewModel.PalDerived;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public partial class PalSpecifierPassiveSkillCollectionViewModel : ObservableObject
    {
        public PalSpecifierPassiveSkillCollectionViewModel()
        {
        }

        public PalSpecifierPassiveSkillCollectionViewModel(IEnumerable<PassiveSkill> modelPassives)
        {
            Passive1 = PassiveSkillViewModel.Make(modelPassives.Skip(0).FirstOrDefault());
            Passive2 = PassiveSkillViewModel.Make(modelPassives.Skip(1).FirstOrDefault());
            Passive3 = PassiveSkillViewModel.Make(modelPassives.Skip(2).FirstOrDefault());
            Passive4 = PassiveSkillViewModel.Make(modelPassives.Skip(3).FirstOrDefault());
        }

        [NotifyPropertyChangedFor(nameof(HasItems))]
        [NotifyPropertyChangedFor(nameof(FixedViewModel))]
        [ObservableProperty]
        private PassiveSkillViewModel passive1;

        [NotifyPropertyChangedFor(nameof(HasItems))]
        [NotifyPropertyChangedFor(nameof(FixedViewModel))]
        [ObservableProperty]
        private PassiveSkillViewModel passive2;

        [NotifyPropertyChangedFor(nameof(HasItems))]
        [NotifyPropertyChangedFor(nameof(FixedViewModel))]
        [ObservableProperty]
        private PassiveSkillViewModel passive3;

        [NotifyPropertyChangedFor(nameof(HasItems))]
        [NotifyPropertyChangedFor(nameof(FixedViewModel))]
        [ObservableProperty]
        private PassiveSkillViewModel passive4;

        public bool HasItems => AsEnumerable().Any();

        public PassiveSkillCollectionViewModel FixedViewModel => new PassiveSkillCollectionViewModel(AsEnumerable());

        public IEnumerable<PassiveSkillViewModel> AsEnumerable() => new List<PassiveSkillViewModel>() { Passive1, Passive2, Passive3, Passive4 }.Where(p => p != null);

        public IEnumerable<PassiveSkill> AsModelEnumerable() => AsEnumerable().Select(p => p.ModelObject).Distinct();

        public void CopyFrom(PalSpecifierPassiveSkillCollectionViewModel other)
        {
            Passive1 = other.Passive1;
            Passive2 = other.Passive2;
            Passive3 = other.Passive3;
            Passive4 = other.Passive4;
        }
    }
}
