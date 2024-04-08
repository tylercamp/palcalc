using System;
using System.Windows;
using System.Windows.Input;

namespace GraphSharp.AttachedBehaviours
{
	public static class DragBehaviour
    {
        #region IsDragEnabled

        public static readonly DependencyProperty IsDragEnabledProperty = DependencyProperty.RegisterAttached( "IsDragEnabled", typeof( bool ), typeof( DragBehaviour ), new UIPropertyMetadata( false, OnIsDragEnabledPropertyChanged ) );

        public static bool GetIsDragEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDragEnabledProperty);
        }

        public static void SetIsDragEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragEnabledProperty, value);
        }

        #endregion

        #region IsDragging

        public static readonly DependencyProperty IsDraggingProperty = DependencyProperty.RegisterAttached( "IsDragging", typeof( bool ), typeof( DragBehaviour ), new UIPropertyMetadata( false ) );

        public static bool GetIsDragging(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDraggingProperty);
        }

        public static void SetIsDragging(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDraggingProperty, value);
        }

        #endregion

        #region X / Y

        public static readonly DependencyProperty XProperty = DependencyProperty.RegisterAttached("X", typeof(double), typeof(DragBehaviour), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static double GetX(DependencyObject obj)
        {
            return (double)obj.GetValue(XProperty);
        }

        public static void SetX(DependencyObject obj, double value)
        {
            obj.SetValue(XProperty, value);
        }

        public static readonly DependencyProperty YProperty = DependencyProperty.RegisterAttached("Y", typeof(double), typeof(DragBehaviour), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static double GetY(DependencyObject obj)
        {
            return (double)obj.GetValue(YProperty);
        }

        public static void SetY(DependencyObject obj, double value)
        {
            obj.SetValue(YProperty, value);
        }

        #endregion

        #region OriginalX / OriginalY

        private static readonly DependencyPropertyKey OriginalXPropertyKey = DependencyProperty.RegisterAttachedReadOnly( "OriginalX", typeof( double ), typeof( DragBehaviour ), new UIPropertyMetadata( 0.0 ) );

        private static double GetOriginalX(DependencyObject obj)
        {
            return (double)obj.GetValue(OriginalXPropertyKey.DependencyProperty);
        }

        private static void SetOriginalX(DependencyObject obj, double value)
        {
            obj.SetValue(OriginalXPropertyKey, value);
        }

		private static readonly DependencyPropertyKey OriginalYPropertyKey = DependencyProperty.RegisterAttachedReadOnly( "OriginalY", typeof( double ), typeof( DragBehaviour ), new UIPropertyMetadata( 0.0 ) );

        private static double GetOriginalY(DependencyObject obj)
        {
            return (double)obj.GetValue(OriginalYPropertyKey.DependencyProperty);
        }

        private static void SetOriginalY(DependencyObject obj, double value)
        {
            obj.SetValue(OriginalYPropertyKey, value);
        }

        #endregion

	    private static void OnIsDragEnabledPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
	    {
	        var element = obj as FrameworkElement;
	        FrameworkContentElement contentElement = null;
	        if (element == null)
	        {
	            contentElement = obj as FrameworkContentElement;
	            if (contentElement == null)
	                return;
	        }

	        if (e.NewValue is bool == false)
	            return;

	        if ((bool) e.NewValue)
	        {
	            // register the event handlers
	            if (element != null)
	            {
	                // registering on the FrameworkElement
	                element.MouseLeftButtonDown += OnDragStarted;
	                element.MouseLeftButtonUp += OnDragFinished;
	            }
	            else
	            {
	                // registering on the FrameworkContentElement
	                contentElement.MouseLeftButtonDown += OnDragStarted;
	                contentElement.MouseLeftButtonUp += OnDragFinished;
	            }
	        }
	        else
	        {
	            // unregister the event handlers
	            if (element != null)
	            {
	                // unregistering on the FrameworkElement
	                element.MouseLeftButtonDown -= OnDragStarted;
	                element.MouseLeftButtonUp -= OnDragFinished;
	            }
	            else
	            {
	                // unregistering on the FrameworkContentElement
	                contentElement.MouseLeftButtonDown -= OnDragStarted;
	                contentElement.MouseLeftButtonUp -= OnDragFinished;
	            }
	        }
	    }

	    private static void OnDragStarted(object sender, MouseButtonEventArgs e)
	    {
	        var obj = sender as DependencyObject;
	        // we are starting the drag
	        SetIsDragging(obj, true);

	        Point pos = e.GetPosition(obj as IInputElement);

	        // save the position of the mouse to the start position
	        SetOriginalX(obj, pos.X);
	        SetOriginalY(obj, pos.Y);

	        // capture the mouse
	        var element = obj as FrameworkElement;
	        if (element != null)
	        {
	            element.CaptureMouse();
	            element.MouseMove += OnDragging;
	        }
	        else
	        {
	            var contentElement = obj as FrameworkContentElement;
	            if (contentElement == null)
	                throw new ArgumentException("The control must be a descendent of the FrameworkElement or FrameworkContentElement!");
	            contentElement.CaptureMouse();
	            contentElement.MouseMove += OnDragging;
	        }
	        e.Handled = true;
	    }

	    private static void OnDragFinished(object sender, MouseButtonEventArgs e)
	    {
	        var obj = (DependencyObject) sender;
	        SetIsDragging(obj, false);
	        obj.ClearValue(OriginalXPropertyKey);
	        obj.ClearValue(OriginalYPropertyKey);

	        // we finished the drag, release the mouse
	        var element = sender as FrameworkElement;
	        if (element != null)
	        {
	            element.MouseMove -= OnDragging;
	            element.ReleaseMouseCapture();
	        }
	        else
	        {
	            var contentElement = sender as FrameworkContentElement;
	            if (contentElement == null)
	                throw new ArgumentException("The control must be a descendent of the FrameworkElement or FrameworkContentElement!");
	            contentElement.MouseMove -= OnDragging;
	            contentElement.ReleaseMouseCapture();
	        }
	        e.Handled = true;
	    }

        private static void OnDragging(object sender, MouseEventArgs e)
        {
            var obj = sender as DependencyObject;
            if (obj == null)
                return;

            if (!GetIsDragging(obj))
                return;

            Point pos = e.GetPosition(obj as IInputElement);
            double horizontalChange = pos.X - GetOriginalX(obj);
            double verticalChange = pos.Y - GetOriginalY(obj);

            var currentX = double.IsNaN(GetX(obj)) ? 0.0 : GetX(obj);
            var currentY = double.IsNaN(GetY(obj)) ? 0.0 : GetY(obj);

            horizontalChange /= currentX < 0 ? 100.0 : 10.0;
            verticalChange /= currentY < 0 ? 100.0 : 10.0;

            SetX(obj, currentX + horizontalChange);
            SetY(obj, currentY + verticalChange);
            e.Handled = true;
        }
	}
}
