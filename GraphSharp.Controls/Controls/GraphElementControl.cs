using System;
using System.Diagnostics;
using System.Windows;

namespace GraphSharp.Controls
{
	public class HighlightTriggeredEventArgs : RoutedEventArgs
	{
		public bool Cancel { get; set; }
		public bool IsPositiveTrigger { get; private set; }

		public HighlightTriggeredEventArgs( RoutedEvent evt, object source, bool isPositiveTrigger )
			: base( evt, source )
		{
			Cancel = false;
			IsPositiveTrigger = isPositiveTrigger;
		}
	}

	public class HighlightInfoChangedEventArgs : RoutedEventArgs
	{
		public object OldHighlightInfo { get; private set; }
		public object NewHighlightInfo { get; private set; }

		public HighlightInfoChangedEventArgs( RoutedEvent evt, object source, object oldHighlightInfo, object newHighlightInfo )
			: base( evt, source )
		{
			OldHighlightInfo = oldHighlightInfo;
			NewHighlightInfo = newHighlightInfo;
		}
	}

	public delegate void HighlightInfoChangedEventHandler( object sender, HighlightInfoChangedEventArgs args );

	public delegate void HighlightTriggerEventHandler( object sender, HighlightTriggeredEventArgs args );

	public static class GraphElementBehaviour
	{
		public static readonly RoutedEvent HighlightEvent = EventManager.RegisterRoutedEvent( "Highlight", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( GraphElementBehaviour ) );
		public static void AddHighlightHandler( DependencyObject d, RoutedEventHandler handler )
		{
            var e = d as UIElement;
			if ( e != null )
				e.AddHandler( HighlightEvent, handler );
		}

		public static void RemoveHighlightHandler( DependencyObject d, RoutedEventHandler handler )
		{
            var e = d as UIElement;
			if ( e != null )
				e.RemoveHandler( HighlightEvent, handler );
		}

		public static readonly RoutedEvent UnhighlightEvent = EventManager.RegisterRoutedEvent( "Unhighlight", RoutingStrategy.Bubble, typeof( RoutedEventHandler ), typeof( GraphElementBehaviour ) );
		public static void AddUnhighlightHandler( DependencyObject d, RoutedEventHandler handler )
		{
            var e = d as UIElement;
			if ( e != null )
				e.AddHandler( UnhighlightEvent, handler );
		}

		public static void RemoveUnhighlightHandler( DependencyObject d, RoutedEventHandler handler )
		{
            var e = d as UIElement;
			if ( e != null )
				e.RemoveHandler( UnhighlightEvent, handler );
		}

		internal static readonly RoutedEvent HighlightTriggeredEvent = EventManager.RegisterRoutedEvent( "HighlightTriggered", RoutingStrategy.Bubble, typeof( HighlightTriggerEventHandler ), typeof( GraphElementBehaviour ) );
		public static void AddHighlightTriggeredHandler( DependencyObject d, RoutedEventHandler handler )
		{
            var e = d as UIElement;
			if ( e != null )
				e.AddHandler( HighlightTriggeredEvent, handler );
		}

		public static void RemoveHighlightTriggeredHandler( DependencyObject d, RoutedEventHandler handler )
		{
            var e = d as UIElement;
			if ( e != null )
				e.RemoveHandler( HighlightTriggeredEvent, handler );
		}

		public static readonly RoutedEvent HighlightInfoChangedEvent = EventManager.RegisterRoutedEvent( "HighlightInfoChanged", RoutingStrategy.Bubble, typeof( HighlightInfoChangedEventHandler ), typeof( GraphElementBehaviour ) );
		public static void AddHighlightInfoChangedHandler( DependencyObject d, RoutedEventHandler handler )
		{
            var e = d as UIElement;
			if ( e != null )
				e.AddHandler( HighlightInfoChangedEvent, handler );
		}

		public static void RemoveHighlightInfoChangedHandler( DependencyObject d, RoutedEventHandler handler )
		{
            var e = d as UIElement;
			if ( e != null )
				e.RemoveHandler( HighlightInfoChangedEvent, handler );
		}

		public static bool GetHighlightTrigger( DependencyObject obj )
		{
			return (bool)obj.GetValue( HighlightTriggerProperty );
		}

		public static void SetHighlightTrigger( DependencyObject obj, bool value )
		{
			obj.SetValue( HighlightTriggerProperty, value );
		}

		// Using a DependencyProperty as the backing store for HighlightTrigger.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty HighlightTriggerProperty =
			DependencyProperty.RegisterAttached( "HighlightTrigger", typeof( bool ), typeof( GraphElementBehaviour ), new UIPropertyMetadata( false, null, HighlightTriggerCoerce ) );

		private static object HighlightTriggerCoerce( DependencyObject d, object baseValue )
		{
			var e = d as UIElement;
			if ( e == null )
				return baseValue;

			if ( (bool)baseValue == GetHighlightTrigger( d ) )
				return baseValue;

			var args = new HighlightTriggeredEventArgs( HighlightTriggeredEvent, d, (bool)baseValue );
            try
            {
                e.RaiseEvent(args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception during HighlightTrigger_Coerce - likely the graph is still animating: " + ex);
            }

			return args.Cancel ? GetHighlightTrigger( d ) : baseValue;
		}

		public static readonly DependencyProperty IsHighlightedProperty;
		private static readonly DependencyPropertyKey IsHighlightedPropertyKey =
			DependencyProperty.RegisterAttachedReadOnly( "IsHighlighted", typeof( bool ), typeof( GraphElementBehaviour ), new UIPropertyMetadata( false, IsHighlightedPropertyChanged ) );



		public static bool GetIsHighlighted( DependencyObject obj )
		{
			return (bool)obj.GetValue( IsHighlightedProperty );
		}

		internal static void SetIsHighlighted( DependencyObject obj, bool value )
		{
			obj.SetValue( IsHighlightedPropertyKey, value );
		}

		// When the IsHighlighted Property changes we should raise the 
		// Highlight and Unhighlight RoutedEvents.
		private static void IsHighlightedPropertyChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		{
            var control = d as UIElement;
			if ( control == null )
				return;

			if ( (bool)e.NewValue )
				control.RaiseEvent( new RoutedEventArgs( HighlightEvent, d ) );
			else
				control.RaiseEvent( new RoutedEventArgs( UnhighlightEvent, d ) );
		}

		public static readonly DependencyProperty HighlightInfoProperty;
		private static readonly DependencyPropertyKey HighlightInfoPropertyKey =
			DependencyProperty.RegisterAttachedReadOnly( "HighlightInfo", typeof( object ), typeof( GraphElementBehaviour ),
												 new PropertyMetadata( null, HighlightInfoPropertyChanged ) );



		public static object GetHighlightInfo( DependencyObject obj )
		{
			return obj.GetValue( HighlightInfoProperty );
		}

		internal static void SetHighlightInfo( DependencyObject obj, object value )
		{
			obj.SetValue( HighlightInfoPropertyKey, value );
		}

		private static void HighlightInfoPropertyChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		{
            var control = d as UIElement;
			if ( control == null )
				return;

			control.RaiseEvent( new HighlightInfoChangedEventArgs( HighlightInfoChangedEvent, d, e.OldValue, e.NewValue ) );
		}

		public static readonly DependencyProperty IsSemiHighlightedProperty;
		private static readonly DependencyPropertyKey IsSemiHighlightedPropertyKey =
			DependencyProperty.RegisterAttachedReadOnly( "IsSemiHighlighted", typeof( bool ), typeof( GraphElementBehaviour ),
												 new PropertyMetadata( false ) );

		public static bool GetIsSemiHighlighted( DependencyObject obj )
		{
			return (bool)obj.GetValue( IsSemiHighlightedProperty );
		}

		internal static void SetIsSemiHighlighted( DependencyObject obj, bool value )
		{
			obj.SetValue( IsSemiHighlightedPropertyKey, value );
		}

		public static readonly DependencyProperty SemiHighlightInfoProperty;
		private static readonly DependencyPropertyKey SemiHighlightInfoPropertyKey =
			DependencyProperty.RegisterAttachedReadOnly( "SemiHighlightInfo", typeof( object ), typeof( GraphElementBehaviour ), new PropertyMetadata( null ) );

		public static object GetSemiHighlightInfo( DependencyObject obj )
		{
			return obj.GetValue( SemiHighlightInfoProperty );
		}

		internal static void SetSemiHighlightInfo( DependencyObject obj, object value )
		{
			obj.SetValue( SemiHighlightInfoPropertyKey, value );
		}

		public static readonly DependencyProperty LayoutInfoProperty =
			DependencyProperty.RegisterAttached( "LayoutInfo", typeof( object ), typeof( GraphElementBehaviour ), new UIPropertyMetadata( null ) );

		public static object GetLayoutInfo( DependencyObject obj )
		{
			return obj.GetValue( LayoutInfoProperty );
		}

		public static void SetLayoutInfo( DependencyObject obj, object value )
		{
			obj.SetValue( LayoutInfoProperty, value );
		}

		static GraphElementBehaviour()
		{
			IsSemiHighlightedProperty = IsSemiHighlightedPropertyKey.DependencyProperty;
			SemiHighlightInfoProperty = SemiHighlightInfoPropertyKey.DependencyProperty;
			HighlightInfoProperty = HighlightInfoPropertyKey.DependencyProperty;
			IsHighlightedProperty = IsHighlightedPropertyKey.DependencyProperty;
		}
	}
}