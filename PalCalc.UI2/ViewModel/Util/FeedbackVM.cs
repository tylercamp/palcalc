using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.ViewModel.Util
{

    internal partial class FeedbackVM(string message)
    {
        public string Message { get; } = message;
        public bool IsPresent => Message != "";

        public static readonly FeedbackVM None = new("");
    }
}
