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

namespace PalCalc.UI.ViewModel
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
        public CustomPalInstanceViewModel(PalLocation location)
        {
            Pal = null;
            Gender = CustomPalInstanceGender.Male;
            Location = location;
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
        }

        public ImageSource Icon => Pal?.Icon ?? PalIcon.DefaultIcon;
        public ImageBrush IconBrush => Pal?.IconBrush ?? PalIcon.DefaultIconBrush;

        public PalLocation Location { get; }

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

        public bool IsValid => Pal != null;

        public PalInstance ModelObject => !IsValid ? null : new PalInstance()
        {
            Gender = Gender.Value,
            Level = 1,
            Pal = Pal.ModelObject,
            PassiveSkills = new List<PassiveSkill>()
            {
                Passive1.ModelObject,
                Passive2.ModelObject,
                Passive3.ModelObject,
                Passive4.ModelObject
            }.SkipNull().ToList(),
            // TODO - sort out locations
            Location = new PalLocation()
            {
                Type = LocationType.Palbox
            }
        };

        public PalInstanceViewModel CommmonViewModelObject => !IsValid ? null : new PalInstanceViewModel(ModelObject);


    }
}
