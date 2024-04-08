using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace GraphSharp.Controls.Transitions
{
    public class FadeTransition : TransitionBase
    {
        private readonly double _startOpacity;
        private readonly double _endOpacity;
        private readonly int _rounds = 1;

        protected FadeTransition( double startOpacity, double endOpacity )
            : this( startOpacity, endOpacity, 2 )
        {
        }

        protected FadeTransition(double startOpacity, double endOpacity, int rounds)
        {
            _startOpacity = startOpacity;
            _endOpacity = endOpacity;
            _rounds = rounds;
        }

        public override void Run(IAnimationContext context, Control control, TimeSpan duration, Action<Control> endMethod )
        {
            var storyboard = new Storyboard();

            DoubleAnimation fadeAnimation;

            if ( _rounds > 1 )
            {
                fadeAnimation = new DoubleAnimation( _startOpacity, _endOpacity, new Duration( duration ) );
                fadeAnimation.AutoReverse = true;
                fadeAnimation.RepeatBehavior = new RepeatBehavior( _rounds - 1 );
                storyboard.Children.Add( fadeAnimation );
                Storyboard.SetTarget( fadeAnimation, control );
                Storyboard.SetTargetProperty( fadeAnimation, new PropertyPath( UIElement.OpacityProperty ) );
            }

            fadeAnimation = new DoubleAnimation( _startOpacity, _endOpacity, new Duration( duration ) );
            fadeAnimation.BeginTime = TimeSpan.FromMilliseconds( duration.TotalMilliseconds * ( _rounds - 1 ) * 2 );
            storyboard.Children.Add( fadeAnimation );
            Storyboard.SetTarget( fadeAnimation, control );
            Storyboard.SetTargetProperty( fadeAnimation, new PropertyPath( UIElement.OpacityProperty ) );

            if ( endMethod != null )
                storyboard.Completed += ( s, a ) => endMethod( control );
            storyboard.Begin( control );
        }
    }
}
