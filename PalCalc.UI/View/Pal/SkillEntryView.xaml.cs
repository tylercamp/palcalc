using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PalCalc.UI.View.Pal
{
    [ContentProperty(nameof(Children))]
    public partial class SkillEntryView : UserControl
    {
        public SkillEntryView()
        {
            InitializeComponent();
        }

        public UIElementCollection Children => contentGrid.Children;
    }
}
