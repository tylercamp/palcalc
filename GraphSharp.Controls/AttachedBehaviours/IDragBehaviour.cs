using System.Collections.Generic;
using System.Windows;

namespace GraphSharp.AttachedBehaviours
{
    public interface IDragBehaviour
    {
        IEnumerable<FrameworkElement> GetChildElements();
    }
}