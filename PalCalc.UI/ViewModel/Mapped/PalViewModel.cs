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

        public PalViewModel(Pal pal)
        {
            ModelObject = pal;

            Name = NameLocalizer.Bind(ModelObject);
        }

        public ILocalizedText Name { get; }

        // TODO - make this private?
        public Pal ModelObject { get; }

        public ImageSource Icon => PalIcon.Images[ModelObject];
        public ImageBrush IconBrush => PalIcon.ImageBrushes[ModelObject];

        // TODO - localize
        public string Label => ModelObject == null ? "" : $"{ModelObject.Name} (#{ModelObject.Id})";

        public override bool Equals(object obj) => ModelObject.Equals((obj as PalViewModel)?.ModelObject);
        public override int GetHashCode() => ModelObject.GetHashCode();
    }
}
