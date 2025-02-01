using Serilog;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PalCalc.UI.View.Utils
{
    // (thanks again chatgpt)

    public class PopupToolTipTrigger : ContentControl
    {
        private Popup _popup;
        private DispatcherTimer _showTimer;
        private bool _isUnloaded;
        private Window _boundWindow;

        public static readonly DependencyProperty ToolTipContentTemplateProperty =
            DependencyProperty.Register(nameof(ToolTipContentTemplate), typeof(DataTemplate), typeof(PopupToolTipTrigger));

        public static readonly DependencyProperty InitialShowDelayProperty =
            DependencyProperty.Register(nameof(InitialShowDelay), typeof(int), typeof(PopupToolTipTrigger), new PropertyMetadata(500));

        public static readonly DependencyProperty PopupAnimationProperty =
            DependencyProperty.Register(nameof(PopupAnimation), typeof(PopupAnimation), typeof(PopupToolTipTrigger), new PropertyMetadata(PopupAnimation.Fade));

        static PopupToolTipTrigger()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PopupToolTipTrigger), new FrameworkPropertyMetadata(typeof(PopupToolTipTrigger)));
        }

        static PopupToolTipTrigger ActiveTrigger;

        public PopupToolTipTrigger()
        {
            Background = Brushes.Transparent;

            _showTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _showTimer.Tick += ShowTimer_Tick;

            MouseDown += PopupToolTipTrigger_MouseDown;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;

            _isUnloaded = false;
            Unloaded += PopupToolTipTrigger_Unloaded;
        }

        private void PopupToolTipTrigger_Unloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _showTimer.Stop();
            HidePopup();
        }

        private void PopupToolTipTrigger_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isUnloaded) return;

            _showTimer.Stop();
            HidePopup();
        }

        public DataTemplate ToolTipContentTemplate
        {
            get => (DataTemplate)GetValue(ToolTipContentTemplateProperty);
            set => SetValue(ToolTipContentTemplateProperty, value);
        }

        public int InitialShowDelay
        {
            get => (int)GetValue(InitialShowDelayProperty);
            set => SetValue(InitialShowDelayProperty, value);
        }

        public PopupAnimation PopupAnimation
        {
            get => (PopupAnimation)GetValue(PopupAnimationProperty);
            set => SetValue(PopupAnimationProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _popup = new Popup
            {
                Placement = PlacementMode.MousePoint,
                StaysOpen = true,
                AllowsTransparency = true,
                IsOpen = false,
            };

            _popup.SetBinding(Popup.PopupAnimationProperty, new Binding(nameof(PopupAnimation)) { Source = this });

            var contentControl = new ContentControl();
            contentControl.SetBinding(ContentControl.ContentTemplateProperty, new Binding(nameof(ToolTipContentTemplate)) { Source = this });
            contentControl.SetBinding(ContentControl.ContentProperty, new Binding(nameof(DataContext)) { Source = this });

            // Optionally, bind the DataContext of the tooltip to the DataContext of the parent control
            contentControl.SetBinding(ContentControl.DataContextProperty, new Binding
            {
                Source = this,
                Path = new PropertyPath(nameof(DataContext)),
                Mode = BindingMode.OneWay
            });

            _popup.Child = contentControl;

            _popup.MouseEnter += OnMouseEnter;
            _popup.MouseLeave += OnMouseLeave;
        }

        private ILogger logger = Log.ForContext<PopupToolTipTrigger>();

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (_isUnloaded) return;

            if (_popup != null && !_showTimer.IsEnabled && !_popup.IsOpen)
            {
                _showTimer.Interval = TimeSpan.FromMilliseconds(InitialShowDelay);
                _showTimer.Start();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_popup != null) _showTimer.Stop();
        }

        private void ShowTimer_Tick(object sender, EventArgs e)
        {
            _showTimer.Stop();
            if (!_isUnloaded && _popup != null) ShowPopup();
        }

        private void ShowPopup()
        {
            if (_isUnloaded || _popup.IsOpen) return;

            if (ActiveTrigger != null) ActiveTrigger.HidePopup();
            ActiveTrigger = this;

            _popup.IsOpen = true;
            if (Window.GetWindow(this) is Window parentWindow)
            {
                _boundWindow = parentWindow;

                parentWindow.MouseMove += ParentWindow_MouseMove;
                parentWindow.Deactivated += ParentWindow_Deactivated;
                _popup.MouseLeave += _popup_MouseLeave;
            }
        }

        private void HidePopup()
        {
            if (_popup == null || !_popup.IsOpen) return;

            if (ActiveTrigger == this) ActiveTrigger = null;

            _popup.IsOpen = false;
            _popup.MouseLeave -= _popup_MouseLeave;

            if (_boundWindow != null)
            {
                _boundWindow.MouseMove -= ParentWindow_MouseMove;
                _boundWindow.Deactivated -= ParentWindow_Deactivated;
            }

            _boundWindow = null;
        }

        // (a few workarounds to close the popup when the mouse moves too far away)

        private void _popup_MouseLeave(object sender, MouseEventArgs e)
        {
            // needed if we move the mouse off the popup but _not_ onto the parent window, meaning we wouldn't get further
            // mouse events. if it was on the parent window then we could do the "close if mouse moves far away" logic. 
            if (!this.IsMouseOver && Window.GetWindow(this)?.IsMouseOver != true)
                HidePopup();
        }

        private void ParentWindow_Deactivated(object sender, EventArgs e)
        {
            // there's a sliver of space where you can maneuver your mouse off the popup, and then off the window, and the
            // popup will remain no matter how far away you move your mouse. `MouseLeave` on the window itself doesn't work
            // for some reason, so as a compromise we'll hide if the parent window loses focus.
            //
            // (a user would probably be confused why the popup isn't going away and click elsewhere on the screen, causing
            // this to trigger and close the popup)
            HidePopup();
        }

        private void ParentWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isUnloaded && _popup != null && _popup.Child is FrameworkElement popupContent)
            {
                // Get the mouse position in screen coordinates
                Point mouseScreenPos = e.GetPosition(Application.Current.MainWindow);
                mouseScreenPos = Application.Current.MainWindow.PointToScreen(mouseScreenPos);

                // Get the bounds of the Popup in screen coordinates
                Point popupTopLeft = popupContent.PointToScreen(new Point(0, 0));
                Point popupBottomRight = popupContent.PointToScreen(new Point(popupContent.ActualWidth, popupContent.ActualHeight));
                Rect popupBounds = new Rect(popupTopLeft, popupBottomRight);

                // Calculate the distance to the nearest edge of the popup
                double deltaX = Math.Max(0, Math.Max(popupBounds.Left - mouseScreenPos.X, mouseScreenPos.X - popupBounds.Right));
                double deltaY = Math.Max(0, Math.Max(popupBounds.Top - mouseScreenPos.Y, mouseScreenPos.Y - popupBounds.Bottom));
                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // Close the popup if the distance exceeds the threshold
                if (distance > 15 && !this.IsMouseOver)
                {
                    HidePopup();
                }
            }
        }
    }
}
