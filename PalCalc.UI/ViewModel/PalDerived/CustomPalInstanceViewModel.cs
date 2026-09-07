using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.PalDerived
{
    public class CustomPalInstanceGender
    {
        private CustomPalInstanceGender(PalGender gender)
        {
            Value = gender;
            Label = gender.Label();
        }

        public PalGender Value { get; }
        public ILocalizedText Label { get; }

        public static CustomPalInstanceGender Make(PalGender gender) => gender == PalGender.MALE ? Male : Female;

        public static CustomPalInstanceGender Male { get; } = new CustomPalInstanceGender(PalGender.MALE);
        public static CustomPalInstanceGender Female { get; } = new CustomPalInstanceGender(PalGender.FEMALE);

        public static List<CustomPalInstanceGender> Options { get; } = [Male, Female];
    }

    public partial class CustomPalInstanceViewModel : ObservableObject
    {
        static CustomPalInstanceViewModel designInstance;
        public static CustomPalInstanceViewModel DesignInstance => designInstance ??= new CustomPalInstanceViewModel(
            new PalInstance()
            {
                Gender = PalGender.FEMALE,
                Pal = PalDB.LoadEmbedded().Pals.First(),
                PassiveSkills = [PalDB.LoadEmbedded().StandardPassiveSkills.First()]
            }
        );

        public CustomPalInstanceViewModel(CustomContainerViewModel container)
        {
            Pal = null;
            Gender = CustomPalInstanceGender.Male;
            Location = new PalLocation() { ContainerId = container.ContainerId, Type = LocationType.Custom };
        }

        public CustomPalInstanceViewModel(PalInstance instance)
        {
            Pal = PalViewModel.Make(instance.Pal);
            Gender = CustomPalInstanceGender.Make(instance.Gender);
            Location = instance.Location;
            
            var passiveVms = instance.PassiveSkills.Select(PassiveSkillViewModel.Make).ToList();
            Passive1 = passiveVms.Skip(0).FirstOrDefault();
            Passive2 = passiveVms.Skip(1).FirstOrDefault();
            Passive3 = passiveVms.Skip(2).FirstOrDefault();
            Passive4 = passiveVms.Skip(3).FirstOrDefault();

            var attackVms = instance.ActiveSkills.Select(ActiveSkillViewModel.Make).ToList();
            Attack1 = attackVms.Skip(0).FirstOrDefault();
            Attack2 = attackVms.Skip(1).FirstOrDefault();
            Attack3 = attackVms.Skip(2).FirstOrDefault();
            Attack4 = attackVms.Skip(3).FirstOrDefault();
            Attack5 = attackVms.Skip(4).FirstOrDefault();
            Attack6 = attackVms.Skip(5).FirstOrDefault();

            IvHp = instance.IV_HP;
            IvAttack = instance.IV_Attack;
            IvDefense = instance.IV_Defense;
        }

        public ImageSource Icon => Pal?.Icon ?? PalIcon.DefaultIcon;
        public ImageBrush IconBrush => Pal?.IconBrush ?? PalIcon.DefaultIconBrush;

        public PalLocation Location { get; }

        [NotifyPropertyChangedFor(nameof(Icon))]
        [NotifyPropertyChangedFor(nameof(IconBrush))]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        [ObservableProperty]
        private PalViewModel pal;

        [ObservableProperty]
        private CustomPalInstanceGender gender;

        [ObservableProperty]
        private PassiveSkillViewModel passive1;

        [ObservableProperty]
        private PassiveSkillViewModel passive2;

        [ObservableProperty]
        private PassiveSkillViewModel passive3;

        [ObservableProperty]
        private PassiveSkillViewModel passive4;

        [ObservableProperty]
        private ActiveSkillViewModel attack1;

        [ObservableProperty]
        private ActiveSkillViewModel attack2;

        [ObservableProperty]
        private ActiveSkillViewModel attack3;

        [ObservableProperty]
        private ActiveSkillViewModel attack4;

        [ObservableProperty]
        private ActiveSkillViewModel attack5;

        [ObservableProperty]
        private ActiveSkillViewModel attack6;

        [ObservableProperty]
        private int ivHp = 0;

        [ObservableProperty]
        private int ivAttack = 0;

        [ObservableProperty]
        private int ivDefense = 0;

        public bool IsValid => Pal != null;

        private List<ActiveSkill> AttackSkills =>
            new List<ActiveSkill> { Attack1?.ModelObject, Attack2?.ModelObject, Attack3?.ModelObject, Attack4?.ModelObject, Attack5?.ModelObject, Attack6?.ModelObject }
                .SkipNull()
                .ToList();

        public PalInstance ModelObject => !IsValid ? null : new PalInstance()
        {
            Gender = Gender.Value,
            Level = 1,
            Pal = Pal?.ModelObject,
            Location = new PalLocation()
            {
                ContainerId = Location.ContainerId,
                Type = LocationType.Custom,
            },
            PassiveSkills = new List<PassiveSkill>()
            {
                Passive1?.ModelObject,
                Passive2?.ModelObject,
                Passive3?.ModelObject,
                Passive4?.ModelObject
            }.SkipNull().Distinct().ToList(),
            IV_HP = IvHp,
            IV_Shot = IvAttack,
            IV_Defense = IvDefense,
            ActiveSkills = AttackSkills.Distinct().ToList(),
            EquippedActiveSkills = AttackSkills.Distinct().ToList()
        };
    }
}
