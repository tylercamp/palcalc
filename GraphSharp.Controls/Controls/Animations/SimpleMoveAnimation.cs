using System;
using System.Windows.Media.Animation;

namespace GraphSharp.Controls.Animations
{
    public class SimpleMoveAnimation : IAnimation
    {
        #region IAnimation Members

        public void Animate( IAnimationContext context, System.Windows.Controls.Control control, double x, double y, TimeSpan duration )
        {
            if ( !double.IsNaN( x ) )
            {
                double from = GraphCanvas.GetX( control );
                from = double.IsNaN( from ) ? 0.0 : from;

                //create the animation for the horizontal position
                var animationX = new DoubleAnimation(
                    from,
                    x,
                    duration,
                    FillBehavior.HoldEnd );
                animationX.Completed += ( s, e ) =>
                {
                    control.BeginAnimation( GraphCanvas.XProperty, null );
                    control.SetValue( GraphCanvas.XProperty, x );
                };
                control.BeginAnimation( GraphCanvas.XProperty, animationX, HandoffBehavior.Compose );
            }
            if ( !double.IsNaN( y ) )
            {
                double from = GraphCanvas.GetY( control );
                from = ( double.IsNaN( from ) ? 0.0 : from );

                //create an animation for the vertical position
                var animationY = new DoubleAnimation(
                    from, y,
                    duration,
                    FillBehavior.HoldEnd );
                animationY.Completed += ( s, e ) =>
                {
                    control.BeginAnimation( GraphCanvas.YProperty, null );
                    control.SetValue( GraphCanvas.YProperty, y );
                };
                control.BeginAnimation( GraphCanvas.YProperty, animationY, HandoffBehavior.Compose );
            }
        }

        #endregion
    }
}