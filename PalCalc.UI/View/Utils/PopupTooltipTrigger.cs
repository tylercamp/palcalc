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

    public class PopupTooltipTrigger : ContentControl
    {
        private Popup _popup;
        private DispatcherTimer _showTimer;

        public static readonly DependencyProperty TooltipContentProperty =
            DependencyProperty.Register(nameof(TooltipContent), typeof(object), typeof(PopupTooltipTrigger));

        public static readonly DependencyProperty InitialShowDelayProperty =
            DependencyProperty.Register(nameof(InitialShowDelay), typeof(int), typeof(PopupTooltipTrigger), new PropertyMetadata(500));

        static PopupTooltipTrigger()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PopupTooltipTrigger), new FrameworkPropertyMetadata(typeof(PopupTooltipTrigger)));
        }

        static PopupTooltipTrigger ActiveTrigger;

        public PopupTooltipTrigger()
        {
            _showTimer = new DispatcherTimer();
            _showTimer.Tick += ShowTimer_Tick;

            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
        }

        public object TooltipContent
        {
            get => GetValue(TooltipContentProperty);
            set => SetValue(TooltipContentProperty, value);
        }

        public int InitialShowDelay
        {
            get => (int)GetValue(InitialShowDelayProperty);
            set => SetValue(InitialShowDelayProperty, value);
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
                PopupAnimation = PopupAnimation.Fade,
            };

            var border = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Background = SystemColors.ControlBrush,
                BorderBrush = SystemColors.ControlDarkBrush,
                Padding = new Thickness(5),
            };

            var contentContainer = new Grid { ClipToBounds = true };

            var contentControl = new ContentControl();
            contentControl.SetBinding(ContentPresenter.DataContextProperty, new Binding(nameof(DataContext)) { Source = this });
            contentControl.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(TooltipContent)) { Source = this });

            contentContainer.Children.Add(contentControl);
            border.Child = contentContainer;
            _popup.Child = border;

            _popup.MouseEnter += OnMouseEnter;
            _popup.MouseLeave += OnMouseLeave;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (_popup != null)
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
            if (_popup != null) ShowPopup();
        }

        private void ShowPopup()
        {
            if (_popup.IsOpen) return;

            if (ActiveTrigger != null) ActiveTrigger.HidePopup();
            ActiveTrigger = this;

            _popup.IsOpen = true;
            if (Window.GetWindow(this) is Window parentWindow)
            {
                parentWindow.MouseMove += ParentWindow_MouseMove;
                parentWindow.Deactivated += ParentWindow_Deactivated;
                _popup.MouseLeave += _popup_MouseLeave;
            }
        }

        private void HidePopup()
        {
            if (!_popup.IsOpen) return;

            if (ActiveTrigger == this) ActiveTrigger = null;

            _popup.IsOpen = false;
            if (Window.GetWindow(this) is Window parentWindow)
            {
                parentWindow.MouseMove -= ParentWindow_MouseMove;
                parentWindow.Deactivated -= ParentWindow_Deactivated;
                _popup.MouseLeave -= _popup_MouseLeave;
            }
        }

        // (a few workarounds to close the popup when the mouse moves too far away)

        private void _popup_MouseLeave(object sender, MouseEventArgs e)
        {
            // needed if we move the mouse off the popup but _not_ onto the parent window, meaning we wouldn't get further
            // mouse events. if it was on the parent window then we could do the "close if mouse moves far away" logic. 
            if (!this.IsMouseOver && !Window.GetWindow(this).IsMouseOver)
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
            if (_popup != null && _popup.Child is FrameworkElement popupContent)
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
                if (distance > 20)
                {
                    HidePopup();
                }
            }
        }
    }
}
