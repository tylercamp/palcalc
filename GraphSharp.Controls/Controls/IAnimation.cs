using System;
using System.Windows.Controls;

namespace GraphSharp.Controls
{
    public interface IAnimation
    {
        /// <summary>Animates the control of a vertex to a given position.</summary>
        /// <param name="context">The context of the animation.</param>
        /// <param name="control">The control which should be animated to its new position.</param>
        /// <param name="x">The new horizontal coordinate.</param>
        /// <param name="y">The new vertical coordinate.</param>
        /// <param name="duration">The duration of the animation.</param>
        void Animate(IAnimationContext context, Control control, double x, double y, TimeSpan duration);
    }
}
