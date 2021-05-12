﻿using RichCanvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RichCanvas.Gestures
{
    internal class Selecting
    {
        private Point _selectionRectangleInitialPosition;
        private List<RichItemContainer> _selections = new List<RichItemContainer>();

        internal RichItemsControl Context { get; set; }

        internal bool HasSelections => _selections.Count > 0;
        public Selecting()
        {
            DragBehavior.DragDelta += OnDragDeltaChanged;
        }

        private void OnDragDeltaChanged(Point point)
        {
            foreach (var item in _selections)
            {
                var transformGroup = (TransformGroup)item.RenderTransform;
                var translateTransform = (TranslateTransform)transformGroup.Children[1];

                translateTransform.X += point.X;
                translateTransform.Y += point.Y;
            }
        }

        internal void OnMouseDown(MouseEventArgs e)
        {
            var position = e.GetPosition(Context.ItemsHost);
            _selectionRectangleInitialPosition = position;
        }
        internal void OnMouseMove(MouseEventArgs e)
        {
            var position = e.GetPosition(Context.ItemsHost);
            var transformGroup = Context.SelectionRectanlgeTransform;
            var scaleTransform = (ScaleTransform)transformGroup.Children[0];

            double width = position.X - _selectionRectangleInitialPosition.X;
            double height = position.Y - _selectionRectangleInitialPosition.Y;

            if (width < 0 && scaleTransform.ScaleX == 1)
            {
                scaleTransform.ScaleX = -1;
            }

            if (height < 0 && scaleTransform.ScaleY == 1)
            {
                scaleTransform.ScaleY = -1;
            }

            if (height > 0 && scaleTransform.ScaleY == -1)
            {
                scaleTransform.ScaleY = 1;
            }
            if (width > 0 && scaleTransform.ScaleX == -1)
            {
                scaleTransform.ScaleX = 1;
            }
            Context.SelectionRectangle = new Rect(_selectionRectangleInitialPosition.X, _selectionRectangleInitialPosition.Y, Math.Abs(width), Math.Abs(height));
        }
        internal void AddSelection(RichItemContainer container)
        {
            if (!container.IsSelectable)
            {
                _selections.Add(container);
            }
        }

        internal void UnselectAll()
        {
            foreach (var selection in _selections)
            {
                selection.IsSelected = false;
            }
            _selections.Clear();
        }

        internal void UpdateSelectionsPosition()
        {
            for (int i = 0; i < _selections.Count; i++)
            {
                var transformGroup = (TransformGroup)_selections[i].RenderTransform;
                var translateTransform = (TranslateTransform)transformGroup.Children[1];

                _selections[i].Top += translateTransform.Y;
                _selections[i].Left += translateTransform.X;

                translateTransform.X = 0;
                translateTransform.Y = 0;
            }
            Context.ItemsHost.InvalidateMeasure();
        }
    }
}
