using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    public class CustomContainer
    {
        public string Label { get; set; }
        public List<PalInstance> Contents { get; set; } = [];
    }
}
