using PalCalc.Model;
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
        public PalViewModel(Pal pal)
        {
            ModelObject = pal;
        }

        public string Name => ModelObject.Name;

        public Pal ModelObject { get; }

        public ImageSource Icon => PalIcon.Images[ModelObject];
        public ImageBrush IconBrush => PalIcon.ImageBrushes[ModelObject];

        public string Label => ModelObject == null ? "" : $"{ModelObject.Name} (#{ModelObject.Id})";

        public override bool Equals(object obj) => ModelObject.Equals((obj as PalViewModel)?.ModelObject);
        public override int GetHashCode() => ModelObject.GetHashCode();
    }
}
