﻿using System.Windows;
using System.Windows.Input;

namespace RichCanvas.States.Dragging
{
    public class DraggingContainerState : ContainerState
    {
        private Point _initialPosition;

        public DraggingContainerState(RichItemContainer container) : base(container)
        {
        }

        public override void Enter()
        {
            if (Container.IsSelectable)
            {
                Container.IsSelected = true;
                if (!Container.Host.CanSelectMultipleItems)
                {
                    Container?.Host?.UpdateSelectedItem(Container);
                }
            }
        }

        public override void HandleMouseDown(MouseButtonEventArgs e)
        {
            _initialPosition = e.GetPosition(Container?.Host?.ItemsHost);
            Container?.RaiseDragStartedEvent(_initialPosition);
        }

        public override void HandleMouseMove(MouseEventArgs e)
        {
            var currentPosition = e.GetPosition(Container?.Host?.ItemsHost);
            var offset = currentPosition - _initialPosition;
            if (offset.X != 0 || offset.Y != 0)
            {
                Container?.RaiseDragDeltaEvent(new Point(offset.X, offset.Y));
                _initialPosition = currentPosition;
            }
        }

        public override void HandleMouseUp(MouseButtonEventArgs e)
        {
            Container.RaiseDragCompletedEvent(e.GetPosition(Container.Host.ItemsHost));
        }
    }
}
