using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal interface IRemovableSaveGameViewModel
    {
        ICommand RemoveSaveCommand { get; }
    }
}
