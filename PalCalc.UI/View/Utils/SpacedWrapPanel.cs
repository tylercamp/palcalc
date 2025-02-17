using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.View.Utils
{
    /// <summary>
    /// A custom WrapPanel-like control that supports extra properties:
    /// - HorizontalSpacing
    /// - VerticalSpacing
    /// - HorizontalContentAlignment
    /// - VerticalContentAlignment
    /// - Orientation
    ///
    /// It handles spacing and alignment while laying out children.
    /// </summary>
    public class SpacedWrapPanel : Panel
    {
        #region Dependency Properties

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(SpacedWrapPanel),
                new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(
                nameof(HorizontalSpacing),
                typeof(double),
                typeof(SpacedWrapPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(
                nameof(VerticalSpacing),
                typeof(double),
                typeof(SpacedWrapPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        public static readonly DependencyProperty HorizontalContentAlignmentProperty =
            DependencyProperty.Register(
                nameof(HorizontalContentAlignment),
                typeof(HorizontalAlignment),
                typeof(SpacedWrapPanel),
                new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));

        public HorizontalAlignment HorizontalContentAlignment
        {
            get => (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty);
            set => SetValue(HorizontalContentAlignmentProperty, value);
        }

        public static readonly DependencyProperty VerticalContentAlignmentProperty =
            DependencyProperty.Register(
                nameof(VerticalContentAlignment),
                typeof(VerticalAlignment),
                typeof(SpacedWrapPanel),
                new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.AffectsArrange));

        public VerticalAlignment VerticalContentAlignment
        {
            get => (VerticalAlignment)GetValue(VerticalContentAlignmentProperty);
            set => SetValue(VerticalContentAlignmentProperty, value);
        }

        #endregion

        #region Measure/Arrange Helpers

        /// <summary>
        /// The main measure pass, which determines how large each child wants to be
        /// and how to wrap them (depending on Orientation).
        /// </summary>
        /// <param name="availableSize">Size available to the panel.</param>
        /// <returns>The size required by the panel to fit all children.</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (InternalIsInfinite(availableSize))
            {
                // If in a StackPanel or container providing infinite space,
                // we attempt to behave in a “wrapping as needed” manner.
                // E.g., in a horizontally oriented StackPanel with infinite width,
                // you could either choose to measure all children on one line
                // or provide some fallback. Here, we simply measure them
                // and rely on the parent's arrangement logic.
            }

            if (Orientation == Orientation.Horizontal)
            {
                return MeasureHorizontal(availableSize);
            }
            else
            {
                return MeasureVertical(availableSize);
            }
        }

        private Size MeasureHorizontal(Size availableSize)
        {
            double panelWidth = 0;
            double panelHeight = 0;

            double currentLineWidth = 0;
            double currentLineHeight = 0;

            foreach (UIElement child in InternalChildren)
            {
                if (child == null) continue;
                if (child.Visibility == Visibility.Collapsed) continue;

                // Measure child with the full available width 
                // (we'll break lines manually as needed).
                child.Measure(availableSize);
                Size childDesired = child.DesiredSize;

                // If adding this child exceeds the available width, we wrap
                if (currentLineWidth + childDesired.Width > availableSize.Width && currentLineWidth > 0)
                {
                    // Wrap: update the panel's total size from this line
                    panelWidth = Math.Max(panelWidth, currentLineWidth - HorizontalSpacing);

                    if (panelHeight > 0) panelHeight += VerticalSpacing;
                    panelHeight += currentLineHeight;

                    // Start a new line
                    currentLineWidth = childDesired.Width + HorizontalSpacing;
                    currentLineHeight = childDesired.Height;
                }
                else
                {
                    // Continue on the same line
                    currentLineWidth += childDesired.Width + HorizontalSpacing;
                    currentLineHeight = Math.Max(currentLineHeight, childDesired.Height);
                }
            }

            if (currentLineWidth > 0) currentLineWidth -= HorizontalSpacing;
            if (panelHeight > 0 && currentLineWidth > 0) panelHeight += VerticalSpacing;

            // Update final line
            panelWidth = Math.Max(panelWidth, currentLineWidth);
            panelHeight += currentLineHeight;

            // Return the total measured size
            return new Size(Math.Ceiling(panelWidth), Math.Ceiling(panelHeight));
        }

        private Size MeasureVertical(Size availableSize)
        {
            // TODO
            throw new NotImplementedException();
        }

        /// <summary>
        /// The main arrange pass, which positions children in lines or columns (depending on Orientation).
        /// We also factor in alignment properties here.
        /// </summary>
        /// <param name="finalSize">The final size allocated by the parent to this panel.</param>
        /// <returns>The size actually used by the panel.</returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Orientation == Orientation.Horizontal)
            {
                return ArrangeHorizontal(finalSize);
            }
            else
            {
                return ArrangeVertical(finalSize);
            }
        }

        private Size ArrangeHorizontal(Size finalSize)
        {
            // 1) Gather lines
            List<LineInfo> lines = new List<LineInfo>();

            double currentX = 0;
            double currentLineHeight = 0;

            // Temporary collection for items on current line
            List<UIElement> currentLineElements = new List<UIElement>();

            // We’ll accumulate totalHeight so we can compute vertical alignment afterward
            double totalHeight = 0;

            foreach (UIElement child in InternalChildren)
            {
                if (child == null) continue;
                if (child.Visibility == Visibility.Collapsed) continue;

                Size childDesired = child.DesiredSize;

                // If the child won't fit in this line (and we already have content on this line), wrap
                bool wrap = (currentX + childDesired.Width > finalSize.Width) && (currentX > 0);
                if (wrap)
                {
                    // We just finished a line
                    lines.Add(new LineInfo
                    {
                        Elements = currentLineElements,
                        LineHeight = currentLineHeight,
                        LineWidth = currentX - HorizontalSpacing // remove trailing spacing
                    });

                    // Accumulate this line's height
                    totalHeight += currentLineHeight + VerticalSpacing;

                    // reset for next line
                    currentX = 0;
                    currentLineHeight = 0;
                    currentLineElements = new List<UIElement>();
                }

                currentLineElements.Add(child);
                currentX += childDesired.Width + HorizontalSpacing;
                currentLineHeight = Math.Max(currentLineHeight, childDesired.Height);
            }

            // Handle the last line if there are leftover children
            if (currentLineElements.Count > 0)
            {
                lines.Add(new LineInfo
                {
                    Elements = currentLineElements,
                    LineHeight = currentLineHeight,
                    LineWidth = currentX - HorizontalSpacing // remove trailing spacing
                });
                totalHeight += currentLineHeight;
            }
            else
            {
                // If we had any lines and we added vertical spacing along the way,
                // remove the last vertical spacing if it was added
                if (lines.Count > 0)
                    totalHeight -= VerticalSpacing;
            }

            // 2) Compute how much we need to offset vertically based on vertical alignment
            double verticalOffset = ComputePanelOffsetY(totalHeight, finalSize.Height);

            // 3) Arrange lines in a single pass
            double lineStartY = verticalOffset;
            foreach (var line in lines)
            {
                double offsetX = ComputeLineOffsetX(line.LineWidth, finalSize.Width);

                // Arrange each child in the line
                double childX = offsetX;
                foreach (UIElement element in line.Elements)
                {
                    Size desired = element.DesiredSize;
                    double childY = ComputeChildOffsetY(desired.Height, line.LineHeight);

                    element.Arrange(new Rect(childX, lineStartY + childY, desired.Width, desired.Height));
                    childX += desired.Width + HorizontalSpacing;
                }

                lineStartY += line.LineHeight + VerticalSpacing;
            }

            // We return finalSize because the panel has to fill whatever the parent gave it.
            // The actual content might be smaller or bigger, but this is typical in WPF panels.
            return finalSize;
        }

        private Size ArrangeVertical(Size finalSize)
        {
            // TODO
            throw new NotImplementedException();
        }

        #endregion

        #region Alignment Utilities

        /// <summary>
        /// Computes how far we should shift a line horizontally based on <see cref="HorizontalContentAlignment"/>.
        /// </summary>
        private double ComputeLineOffsetX(double lineWidth, double panelWidth)
        {
            double remainingSpace = panelWidth - lineWidth;
            if (remainingSpace <= 0) return 0;

            switch (HorizontalContentAlignment)
            {
                case HorizontalAlignment.Center:
                    return remainingSpace / 2;
                case HorizontalAlignment.Right:
                    return remainingSpace;
                case HorizontalAlignment.Stretch:
                    // For a WrapPanel-like control, "Stretch" horizontally doesn’t
                    // typically distribute space among children, so treat similarly to Left.
                    return 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Computes how far we should shift a line vertically based on <see cref="VerticalContentAlignment"/>.
        /// Used when orientation is horizontal to shift each line’s content in the vertical dimension.
        /// </summary>
        private double ComputeChildOffsetY(double childHeight, double lineHeight)
        {
            double remaining = lineHeight - childHeight;
            if (remaining <= 0) return 0;

            switch (VerticalContentAlignment)
            {
                case VerticalAlignment.Center:
                    return remaining / 2;
                case VerticalAlignment.Bottom:
                    return remaining;
                case VerticalAlignment.Stretch:
                    // Typically, child is forced to line height in a real “stretch.”
                    // Here, we are not resizing the child. So treat similarly to Top.
                    return 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// When orientation is horizontal, we can apply an overall vertical offset
        /// after all lines are placed if <see cref="VerticalContentAlignment"/> is Center or Bottom, etc.
        /// This is a naive approach which simply translates all children if needed.
        /// </summary>
        private double ComputePanelOffsetY(double contentHeight, double panelHeight)
        {
            double remaining = panelHeight - contentHeight;
            if (remaining <= 0) return 0;

            switch (VerticalContentAlignment)
            {
                case VerticalAlignment.Center:
                    return remaining / 2;
                case VerticalAlignment.Bottom:
                    return remaining;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Computes how far we should shift a column vertically based on <see cref="VerticalContentAlignment"/>.
        /// Used when orientation is vertical to shift each column’s content in the vertical dimension.
        /// </summary>
        private double ComputeLineOffsetY(double columnHeight, double panelHeight)
        {
            double remainingSpace = panelHeight - columnHeight;
            if (remainingSpace <= 0) return 0;

            switch (VerticalContentAlignment)
            {
                case VerticalAlignment.Center:
                    return remainingSpace / 2;
                case VerticalAlignment.Bottom:
                    return remainingSpace;
                case VerticalAlignment.Stretch:
                    // For a WrapPanel-like control, we don’t typically stretch vertically per column
                    return 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Computes how far we should shift a child horizontally based on <see cref="HorizontalContentAlignment"/>.
        /// Used when orientation is vertical to shift each child in the column horizontally.
        /// </summary>
        private double ComputeChildOffsetX(double childWidth, double columnWidth)
        {
            double remaining = columnWidth - childWidth;
            if (remaining <= 0) return 0;

            switch (HorizontalContentAlignment)
            {
                case HorizontalAlignment.Center:
                    return remaining / 2;
                case HorizontalAlignment.Right:
                    return remaining;
                case HorizontalAlignment.Stretch:
                    // Again, "stretch" would typically size the child, 
                    // but we don’t forcibly resize here.
                    return 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// When orientation is vertical, we can apply an overall horizontal offset
        /// after all columns are placed if <see cref="HorizontalContentAlignment"/> is Center or Right, etc.
        /// </summary>
        private double ComputePanelOffsetX(double contentWidth, double panelWidth)
        {
            double remaining = panelWidth - contentWidth;
            if (remaining <= 0) return 0;

            switch (HorizontalContentAlignment)
            {
                case HorizontalAlignment.Center:
                    return remaining / 2;
                case HorizontalAlignment.Right:
                    return remaining;
                default:
                    return 0;
            }
        }

        #endregion

        #region Utility Classes and Methods

        /// <summary>
        /// Simple struct to store a line or column’s UIElements and its dimension.
        /// </summary>
        private class LineInfo
        {
            public List<UIElement> Elements { get; set; } = new List<UIElement>();
            public double LineWidth { get; set; }
            public double LineHeight { get; set; }
        }

        /// <summary>
        /// Detect if the <see cref="Size"/> is effectively infinite in either dimension.
        /// This often indicates that the panel is in a StackPanel that gives infinite space.
        /// </summary>
        private bool InternalIsInfinite(Size size)
        {
            return double.IsInfinity(size.Width) || double.IsInfinity(size.Height);
        }

        #endregion
    }
}
