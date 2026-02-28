using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Mei_Music
{
    /// <summary>
    /// Base class for overlay cards that support dragging via a header region.
    /// Raises <see cref="DragMoveDeltaEventArgs"/> as the user drags the header.
    /// </summary>
    public abstract class DraggableOverlayCardBase : UserControl
    {
        private Point _dragStart;

        /// <summary>
        /// Raised continuously while the user drags the header to move the card.
        /// Consumers can translate the hosting element using the provided deltas.
        /// </summary>
        public event EventHandler<DragMoveDeltaEventArgs>? DragMoveDelta;

        /// <summary>
        /// Starts a drag operation from the header region.
        /// Intended to be wired to the header border's <see cref="UIElement.MouseLeftButtonDown"/> event.
        /// </summary>
        protected void HeaderDragBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);

            if (sender is UIElement element)
            {
                element.CaptureMouse();
            }
        }

        /// <summary>
        /// Emits drag delta while the header is captured and the left button is pressed.
        /// Intended to be wired to the header border's <see cref="UIElement.MouseMove"/> event.
        /// </summary>
        protected void HeaderDragBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not UIElement element ||
                !element.IsMouseCaptured ||
                e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point current = e.GetPosition(null);
            double dx = current.X - _dragStart.X;
            double dy = current.Y - _dragStart.Y;
            _dragStart = current;

            DragMoveDelta?.Invoke(this, new DragMoveDeltaEventArgs(dx, dy));
        }

        /// <summary>
        /// Ends the drag operation when the user releases the left mouse button.
        /// Intended to be wired to the header border's <see cref="UIElement.MouseLeftButtonUp"/> event.
        /// </summary>
        protected void HeaderDragBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element && element.IsMouseCaptured)
            {
                element.ReleaseMouseCapture();
            }
        }
    }
}

