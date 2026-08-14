using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.Solver.PalReference;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Mapped
{
    // TODO - It's possible to have multiple attack skills with the same name, e.g. "Fierce Fang"
    //        has Unique_AmaterasuWolf_Dark_Bite and Unique_BlackPuppy_Bite

    public class ActiveSkillViewModel
    {
        private static readonly DerivedLocalizableText<ActiveSkill> NameLocalizer = new DerivedLocalizableText<ActiveSkill>(
            (locale, skill) => skill.LocalizedNames.GetValueOrElse(locale.ToFormalName(), skill.Name)
        );

        private static Dictionary<ActiveSkill, ActiveSkillViewModel> instances;
        public static ActiveSkillViewModel Make(ActiveSkill skill)
        {
            if (skill == null) return null;

            if (instances == null)
            {
                instances = PalDB.LoadEmbedded().ActiveSkills.ToDictionary(s => s, s => new ActiveSkillViewModel(s, NameLocalizer.Bind(s)));
            }

            if (skill is UnrecognizedActiveSkill or RandomActiveSkill)
            {
                return new ActiveSkillViewModel(skill, new HardCodedText(skill.Name));
            }
            else
            {
                return instances[skill];
            }
        }

        public static IReadOnlyList<ActiveSkillViewModel> All { get; } = PalDB.LoadEmbedded().ActiveSkills.Select(Make).OrderBy(s => s.Name.Value).ToList();

        private static ActiveSkillViewModel designerInstance;
        public static ActiveSkillViewModel DesignerInstance =>
            designerInstance ??= new ActiveSkillViewModel(
                new ActiveSkill("Test", "test", PalDB.LoadEmbedded().Elements.First())
                {
                    Power = 30,
                },
                new HardCodedText("Test")
            );

        private ActiveSkillViewModel(ActiveSkill skill, ILocalizedText name)
        {
            ModelObject = skill;
            Name = name;
        }

        public ActiveSkill ModelObject { get; }

        public ImageSource SkillElementImage => SkillElementIcon.Images.GetValueOrElse(ModelObject.Element?.InternalName, SkillElementIcon.DefaultImage);

        public ILocalizedText Name { get; }
    }
}
