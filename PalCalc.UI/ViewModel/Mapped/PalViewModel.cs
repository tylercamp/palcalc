using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class PalViewModel
    {
        private static readonly DerivedLocalizableText<Pal> NameLocalizer = new DerivedLocalizableText<Pal>(
            (locale, pal) => pal.LocalizedNames.GetValueOrElse(locale.ToFormalName(), pal.Name)
        );

        private static Dictionary<Pal, PalViewModel> instances;
        public static PalViewModel Make(Pal pal)
        {
            if (instances == null)
            {
                instances = PalDB.LoadEmbedded().Pals.ToDictionary(p => p, p => new PalViewModel(p));
            }

            return instances[pal];
        }

        public static IReadOnlyList<PalViewModel> All { get; } = PalDB.LoadEmbedded().Pals.Select(Make).ToList();

        private PalViewModel(Pal pal)
        {
            ModelObject = pal;

            Name = NameLocalizer.Bind(ModelObject);
            Label = LocalizationCodes.LC_PAL_LABEL.Bind(
                new
                {
                    PalName = Name,
                    PaldexNum = ModelObject.Id,
                }
            );
        }

        public ILocalizedText Name { get; }

        public Pal ModelObject { get; }

        public ImageSource Icon => PalIcon.Images[ModelObject];
        public ImageBrush IconBrush => PalIcon.ImageBrushes[ModelObject];

        public ILocalizedText Label { get; }

        public override bool Equals(object obj) => ModelObject.Equals((obj as PalViewModel)?.ModelObject);
        public override int GetHashCode() => ModelObject.GetHashCode();
    }
}
