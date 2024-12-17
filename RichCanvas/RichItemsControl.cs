﻿using RichCanvas.Helpers;
using RichCanvas.Gestures;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Collections.Specialized;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace RichCanvas
{
    /// <summary>
    /// ItemsControl hosting <see cref="RichCanvas"/>
    /// </summary>
    [TemplatePart(Name = DrawingPanelName, Type = typeof(Panel))]
    [TemplatePart(Name = SelectionRectangleName, Type = typeof(Rectangle))]
    [StyleTypedProperty(Property = nameof(SelectionRectangleStyle), StyleTargetType = typeof(Rectangle))]
    public class RichItemsControl : MultiSelector
    {
        #region Constants

        private const string DrawingPanelName = "PART_Panel";
        private const string SelectionRectangleName = "PART_SelectionRectangle";
        private const string CanvasContainerName = "CanvasContainer";

        #endregion

        #region Private Fields

        internal readonly ScaleTransform ScaleTransform = new ScaleTransform();
        internal readonly TranslateTransform TranslateTransform = new TranslateTransform();

        public RichCanvas? MainPanel { get; set; }
        private PanningGrid? _canvasContainer;
        private bool _isDrawing;
        private readonly Gestures.Drawing _drawingGesture;
        private readonly Selecting _selectingGesture;
        private DispatcherTimer? _autoPanTimer;
        private readonly List<int> _currentDrawingIndexes = new List<int>();
        private bool _fromEvent;
        private RichItemContainer? _selectedContainer;

        #endregion

        #region Properties API
        /// <summary>
        /// Gets whether at least one item is selected.
        /// </summary>
        public bool HasSelections => base.SelectedItems.Count > 1;
        /// <summary>
        /// <see cref="Grid"/> control wrapping the scrolling logic.
        /// </summary>
        public PanningGrid? ScrollContainer => _canvasContainer;

        /// <summary>
        /// Gets or sets mouse position relative to <see cref="RichItemsControl.ItemsHost"/>.
        /// </summary>
        public static DependencyProperty MousePositionProperty = DependencyProperty.Register(nameof(MousePosition), typeof(Point), typeof(RichItemsControl), new FrameworkPropertyMetadata(default(Point)));
        /// <summary>
        /// Gets or sets mouse position relative to <see cref="RichItemsControl.ItemsHost"/>.
        /// </summary>
        public Point MousePosition
        {
            get => (Point)GetValue(MousePositionProperty);
            set => SetValue(MousePositionProperty, value);
        }

        /// <summary>
        /// Gets or sets mouse position relative to <see cref="RichItemsControl.ItemsHost"/> after right click.
        /// </summary>
        public static DependencyProperty RightClickMousePositionProperty = DependencyProperty.Register(nameof(RightClickMousePosition), typeof(Point), typeof(RichItemsControl), new FrameworkPropertyMetadata(default(Point)));
        /// <summary>
        /// Gets or sets mouse position relative to <see cref="RichItemsControl.ItemsHost"/>.
        /// </summary>
        public Point RightClickMousePosition
        {
            get => (Point)GetValue(RightClickMousePositionProperty);
            set => SetValue(RightClickMousePositionProperty, value);
        }

        /// <summary>
        /// Get only key of <see cref="SelectionRectangleProperty"/>.
        /// </summary>
        protected static readonly DependencyPropertyKey SelectionRectanglePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectionRectangle), typeof(Rect), typeof(RichItemsControl), new FrameworkPropertyMetadata(default(Rect)));
        /// <summary>
        /// Gets the selection area as <see cref="Rect"/>.
        /// </summary>
        public static readonly DependencyProperty SelectionRectangleProperty = SelectionRectanglePropertyKey.DependencyProperty;
        /// <summary>
        /// Gets the selection area as <see cref="Rect"/>.
        /// </summary>
        public Rect SelectionRectangle
        {
            get => (Rect)GetValue(SelectionRectangleProperty);
            internal set => SetValue(SelectionRectanglePropertyKey, value);
        }

        /// <summary>
        /// Get only key of <see cref="IsSelectingProperty"/>
        /// </summary>
        protected static readonly DependencyPropertyKey IsSelectingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsSelecting), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(false));
        /// <summary>
        /// Gets whether the operation in progress is selection.
        /// </summary>
        public static readonly DependencyProperty IsSelectingProperty = IsSelectingPropertyKey.DependencyProperty;
        /// <summary>
        /// Gets whether the operation in progress is selection.
        /// </summary>
        public bool IsSelecting
        {
            get => (bool)GetValue(IsSelectingProperty);
            internal set => SetValue(IsSelectingPropertyKey, value);
        }

        /// <summary>
        /// Get only key of <see cref="AppliedTransformProperty"/>.
        /// </summary>
        protected static readonly DependencyPropertyKey AppliedTransformPropertyKey = DependencyProperty.RegisterReadOnly(nameof(AppliedTransform), typeof(TransformGroup), typeof(RichItemsControl), new FrameworkPropertyMetadata(default(TransformGroup)));
        /// <summary>
        /// Gets the transform that is applied to all child controls.
        /// </summary>
        public static DependencyProperty AppliedTransformProperty = AppliedTransformPropertyKey.DependencyProperty;
        /// <summary>
        /// Gets the transform that is applied to all child controls.
        /// </summary>
        public TransformGroup AppliedTransform
        {
            get => (TransformGroup)GetValue(AppliedTransformProperty);
            internal set => SetValue(AppliedTransformPropertyKey, value);
        }

        /// <summary>
        /// Gets or sets whether Auto-Panning is disabled.
        /// Default is enabled.
        /// </summary>
        public static DependencyProperty DisableAutoPanningProperty = DependencyProperty.Register(nameof(DisableAutoPanning), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(true, OnDisableAutoPanningChanged));
        /// <summary>
        /// Gets or sets whether Auto-Panning is disabled.
        /// Default is enabled.
        /// </summary>
        public bool DisableAutoPanning
        {
            get => (bool)GetValue(DisableAutoPanningProperty);
            set => SetValue(DisableAutoPanningProperty, value);
        }

        /// <summary>
        /// Gets or sets <see cref="DispatcherTimer"/> interval value.
        /// Default is 1.
        /// </summary>
        public static DependencyProperty AutoPanTickRateProperty = DependencyProperty.Register(nameof(AutoPanTickRate), typeof(float), typeof(RichItemsControl), new FrameworkPropertyMetadata(1f, OnAutoPanTickRateChanged));
        /// <summary>
        /// Gets or sets <see cref="DispatcherTimer"/> interval value.
        /// Default is 1.
        /// </summary>
        public float AutoPanTickRate
        {
            get => (float)GetValue(AutoPanTickRateProperty);
            set => SetValue(AutoPanTickRateProperty, value);
        }

        /// <summary>
        /// Gets or sets the <see cref="RichItemsControl.ItemsHost"/> translate speed.
        /// Default is 1.
        /// </summary>
        public static DependencyProperty AutoPanSpeedProperty = DependencyProperty.Register(nameof(AutoPanSpeed), typeof(float), typeof(RichItemsControl), new FrameworkPropertyMetadata(1f));
        /// <summary>
        /// Gets or sets the <see cref="RichItemsControl.ItemsHost"/> translate speed.
        /// Default is 1.
        /// </summary>
        public float AutoPanSpeed
        {
            get => (float)GetValue(AutoPanSpeedProperty);
            set => SetValue(AutoPanSpeedProperty, value);
        }

        /// <summary>
        /// Gets or sets whether Grid Drawing is enabled on <see cref="RichItemsControl.ItemsHost"/> background.
        /// Default is disabled.
        /// </summary>
        public static DependencyProperty EnableGridProperty = DependencyProperty.Register(nameof(EnableGrid), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(false));
        /// <summary>
        /// Gets or sets whether Grid Drawing is enabled on <see cref="RichItemsControl.ItemsHost"/> background.
        /// Default is disabled.
        /// </summary>
        public bool EnableGrid
        {
            get => (bool)GetValue(EnableGridProperty);
            set => SetValue(EnableGridProperty, value);
        }

        /// <summary>
        /// Gets or sets grid drawing viewport size.
        /// Default is 10.
        /// </summary>
        public static DependencyProperty GridSpacingProperty = DependencyProperty.Register(nameof(GridSpacing), typeof(float), typeof(RichItemsControl), new FrameworkPropertyMetadata(10f));
        /// <summary>
        /// Gets or sets grid drawing viewport size.
        /// Default is 10.
        /// </summary>
        public float GridSpacing
        {
            get => (float)GetValue(GridSpacingProperty);
            set => SetValue(GridSpacingProperty, value);
        }

        internal static readonly DependencyPropertyKey ViewportRectPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ViewportRect), typeof(Rect), typeof(RichItemsControl), new FrameworkPropertyMetadata(Rect.Empty));
        /// <summary>
        /// Gets current viewport rectangle.
        /// </summary>
        public static readonly DependencyProperty ViewportRectProperty = ViewportRectPropertyKey.DependencyProperty;
        /// <summary>
        /// Gets current viewport rectangle.
        /// </summary>
        public Rect ViewportRect
        {
            get => (Rect)GetValue(ViewportRectProperty);
            internal set => SetValue(ViewportRectPropertyKey, value);
        }

        /// <summary>
        /// Gets or sets current <see cref="RichItemsControl.TranslateTransform"/>.
        /// </summary>
        public static DependencyProperty TranslateOffsetProperty = DependencyProperty.Register(nameof(TranslateOffset), typeof(Point), typeof(RichItemsControl), new FrameworkPropertyMetadata(default(Point), OnOffsetChanged));
        /// <summary>
        /// Gets or sets current <see cref="RichItemsControl.TranslateTransform"/>.
        /// </summary>
        public Point TranslateOffset
        {
            get => (Point)GetValue(TranslateOffsetProperty);
            set => SetValue(TranslateOffsetProperty, value);
        }

        /// <summary>
        /// Gets or sets whether grid snap correction on <see cref="RichItemContainer"/> is applied.
        /// Default is disabled.
        /// </summary>
        public static DependencyProperty EnableSnappingProperty = DependencyProperty.Register(nameof(EnableSnapping), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(false));
        /// <summary>
        /// Gets or sets whether grid snap correction on <see cref="RichItemContainer"/> is applied.
        /// Default is disabled.
        /// </summary>
        public bool EnableSnapping
        {
            get => (bool)GetValue(EnableSnappingProperty);
            set => SetValue(EnableSnappingProperty, value);
        }

        /// <summary>
        /// Gets or sets the background grid style.
        /// </summary>
        public static DependencyProperty GridStyleProperty = DependencyProperty.Register(nameof(GridStyle), typeof(System.Windows.Media.Drawing), typeof(RichItemsControl));
        /// <summary>
        /// Gets or sets the background grid style.
        /// </summary>
        public System.Windows.Media.Drawing GridStyle
        {
            get => (System.Windows.Media.Drawing)GetValue(GridStyleProperty);
            set => SetValue(GridStyleProperty, value);
        }

        /// <summary>
        /// Gets or sets selection <see cref="Rectangle"/> style.
        /// </summary>
        public static DependencyProperty SelectionRectangleStyleProperty = DependencyProperty.Register(nameof(SelectionRectangleStyle), typeof(Style), typeof(RichItemsControl));
        /// <summary>
        /// Gets or sets selection <see cref="Rectangle"/> style.
        /// </summary>
        public Style SelectionRectangleStyle
        {
            get => (Style)GetValue(SelectionRectangleStyleProperty);
            set => SetValue(SelectionRectangleStyleProperty, value);
        }

        /// <summary>
        /// Gets or sets the scrolling factor applied when scrolling.
        /// Default is 10.
        /// </summary>
        public static DependencyProperty ScrollFactorProperty = DependencyProperty.Register(nameof(ScrollFactor), typeof(double), typeof(RichItemsControl), new FrameworkPropertyMetadata(10d, null, CoerceScrollFactor));
        /// <summary>
        /// Gets or sets the scrolling factor applied when scrolling.
        /// Default is 10.
        /// </summary>
        public double ScrollFactor
        {
            get => (double)GetValue(ScrollFactorProperty);
            set => SetValue(ScrollFactorProperty, value);
        }

        /// <summary>
        /// Gets or sets the factor used to change <see cref="RichItemsControl.ScaleTransform"/> on zoom.
        /// Default is 1.1d.
        /// </summary>
        public static DependencyProperty ScaleFactorProperty = DependencyProperty.Register(nameof(ScaleFactor), typeof(double), typeof(RichItemsControl), new FrameworkPropertyMetadata(1.1d, null, CoerceScaleFactor));
        /// <summary>
        /// Gets or sets the factor used to change <see cref="RichItemsControl.ScaleTransform"/> on zoom.
        /// Default is 1.1d.
        /// </summary>
        public double ScaleFactor
        {
            get => (double)GetValue(ScaleFactorProperty);
            set => SetValue(ScaleFactorProperty, value);
        }

        /// <summary>
        /// Gets or sets whether scrolling operation is disabled.
        /// Default is enabled.f
        /// </summary>
        public static DependencyProperty DisableScrollProperty = DependencyProperty.Register(nameof(DisableScroll), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(false, OnDisableScrollChanged));
        /// <summary>
        /// Gets or sets whether scrolling operation is disabled.
        /// Default is enabled.f
        /// </summary>
        public bool DisableScroll
        {
            get => (bool)GetValue(DisableScrollProperty);
            set => SetValue(DisableScrollProperty, value);
        }

        /// <summary>
        /// Gets or sets whether zooming operation is disabled.
        /// Default is enabled.
        /// </summary>
        public static DependencyProperty DisableZoomProperty = DependencyProperty.Register(nameof(DisableZoom), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(false));
        /// <summary>
        /// Gets or sets whether zooming operation is disabled.
        /// Default is enabled.
        /// </summary>
        public bool DisableZoom
        {
            get => (bool)GetValue(DisableZoomProperty);
            set => SetValue(DisableZoomProperty, value);
        }

        /// <summary>
        /// Gets or sets maximum scale for <see cref="RichItemsControl.ScaleTransform"/>.
        /// Default is 2.
        /// </summary>
        public static DependencyProperty MaxScaleProperty = DependencyProperty.Register(nameof(MaxScale), typeof(double), typeof(RichItemsControl), new FrameworkPropertyMetadata(2d, OnMaxScaleChanged, CoerceMaxScale));
        /// <summary>
        /// Gets or sets maximum scale for <see cref="RichItemsControl.ScaleTransform"/>.
        /// Default is 2.
        /// </summary>
        public double MaxScale
        {
            get => (double)GetValue(MaxScaleProperty);
            set => SetValue(MaxScaleProperty, value);
        }

        /// <summary>
        /// Gets or sets minimum scale for <see cref="RichItemsControl.ScaleTransform"/>.
        /// Default is 0.1d.
        /// </summary>
        public static DependencyProperty MinScaleProperty = DependencyProperty.Register(nameof(MinScale), typeof(double), typeof(RichItemsControl), new FrameworkPropertyMetadata(0.1d, OnMinimumScaleChanged, CoerceMinimumScale));
        /// <summary>
        /// Gets or sets minimum scale for <see cref="RichItemsControl.ScaleTransform"/>.
        /// Default is 0.1d.
        /// </summary>
        public double MinScale
        {
            get => (double)GetValue(MinScaleProperty);
            set => SetValue(MinScaleProperty, value);
        }

        /// <summary>
        /// Gets or sets the current <see cref="RichItemsControl.ScaleTransform"/> value.
        /// Default is 1.
        /// </summary>
        public static DependencyProperty ScaleProperty = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(RichItemsControl), new FrameworkPropertyMetadata(1d, OnScaleChanged, ConstarainScaleToRange));
        /// <summary>
        /// Gets or sets the current <see cref="RichItemsControl.ScaleTransform"/> value.
        /// Default is 1.
        /// </summary>
        public double Scale
        {
            get => (double)GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        /// <summary>
        /// Gets or sets the items in the <see cref="RichItemsControl"/> that are selected.
        /// </summary>
        public static DependencyProperty SelectedItemsProperty = DependencyProperty.Register(nameof(SelectedItems), typeof(IList), typeof(RichItemsControl), new FrameworkPropertyMetadata(default(IList), OnSelectedItemsSourceChanged));
        /// <summary>
        /// Gets or sets the items in the <see cref="RichItemsControl"/> that are selected.
        /// </summary>
        public new IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        /// <summary>
        /// Occurs whenever <see cref="RichItemsControl.OnMouseLeftButtonUp(MouseButtonEventArgs)"/> is triggered and the drawing operation finished.
        /// </summary>
        public static readonly RoutedEvent DrawingEndedEvent = EventManager.RegisterRoutedEvent(nameof(DrawingEnded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RichItemsControl));
        /// <summary>
        /// Occurs whenever <see cref="RichItemsControl.OnMouseLeftButtonUp(MouseButtonEventArgs)"/> is triggered and the drawing operation finished.
        /// </summary>
        public event RoutedEventHandler DrawingEnded
        {
            add { AddHandler(DrawingEndedEvent, value); }
            remove { RemoveHandler(DrawingEndedEvent, value); }
        }

        /// <summary>
        /// Occurs whenever <see cref="RichItemsControl.TranslateTransform"/> changes.
        /// </summary>
        public static readonly RoutedEvent ScrollingEvent = EventManager.RegisterRoutedEvent(nameof(Scrolling), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RichItemsControl));
        /// <summary>
        /// Occurs whenever <see cref="RichItemsControl.TranslateTransform"/> changes.
        /// </summary>
        public event RoutedEventHandler Scrolling
        {
            add { AddHandler(ScrollingEvent, value); }
            remove { RemoveHandler(ScrollingEvent, value); }
        }

        /// <summary>
        /// Occurs whenever <see cref="RichItemsControl.OnMouseLeftButtonUp(MouseButtonEventArgs)"/> is triggered and the drawing operation finished.
        /// </summary>
        public static readonly RoutedEvent DraggingEvent = EventManager.RegisterRoutedEvent(nameof(Dragging), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RichItemsControl));
        /// <summary>
        /// Occurs whenever <see cref="RichItemsControl.OnMouseLeftButtonUp(MouseButtonEventArgs)"/> is triggered and the drawing operation finished.
        /// </summary>
        public event RoutedEventHandler Dragging {
            add { AddHandler(DraggingEvent, value); }
            remove { RemoveHandler(DraggingEvent, value); }
        }

        /// <summary>
        /// Gets or sets whether caching is disabled.
        /// Default is <see langword="true"/>.
        /// </summary>
        public static DependencyProperty DisableCacheProperty = DependencyProperty.Register(nameof(DisableCache), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(true, OnDisableCacheChanged));
        /// <summary>
        /// Gets or sets whether caching is disabled.
        /// Default is <see langword="true"/>.
        /// </summary>
        public bool DisableCache
        {
            get => (bool)GetValue(DisableCacheProperty);
            set => SetValue(DisableCacheProperty, value);
        }

        /// <summary>
        /// Get only key of <see cref="IsDraggingProperty"/>
        /// </summary>
        protected static readonly DependencyPropertyKey IsDraggingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDragging), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(false));
        /// <summary>
        /// Gets whether the operation in progress is dragging.
        /// </summary>
        public static readonly DependencyProperty IsDraggingProperty = IsDraggingPropertyKey.DependencyProperty;
        /// <summary>
        /// Gets whether the operation in progress is dragging.
        /// </summary>
        public bool IsDragging
        {
            get => (bool)GetValue(IsDraggingProperty);
            internal set => SetValue(IsDraggingPropertyKey, value);
        }

        /// <summary>
        /// Occurs whenever <see cref="RichItemsControl.ScaleTransform"/> changes.
        /// </summary>
        public static readonly RoutedEvent ZoomingEvent = EventManager.RegisterRoutedEvent(nameof(Zooming), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RichItemsControl));
        /// <summary>
        /// Occurs whenever <see cref="RichItemsControl.ScaleTransform"/> changes.
        /// </summary>
        public event RoutedEventHandler Zooming
        {
            add { AddHandler(ZoomingEvent, value); }
            remove { RemoveHandler(ZoomingEvent, value); }
        }

        /// <summary>
        /// Gets or sets whether real-time selection is enabled.
        /// Default is <see langword="false"/>.
        /// </summary>
        public static DependencyProperty RealTimeSelectionEnabledProperty = DependencyProperty.Register(nameof(RealTimeSelectionEnabled), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(false));
        /// <summary>
        /// Gets or sets whether real-time selection is enabled.
        /// Default is <see langword="false"/>.
        /// </summary>
        public bool RealTimeSelectionEnabled
        {
            get => (bool)GetValue(RealTimeSelectionEnabledProperty);
            set => SetValue(RealTimeSelectionEnabledProperty, value);
        }

        /// <summary>
        /// Gets or sets whether real-time selection is enabled.
        /// Default is <see langword="false"/>.
        /// </summary>
        public static DependencyProperty RealTimeDraggingEnabledProperty = DependencyProperty.Register(nameof(RealTimeDraggingEnabled), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(false));
        /// <summary>
        /// Gets or sets whether real-time selection is enabled.
        /// Default is <see langword="false"/>.
        /// </summary>
        public bool RealTimeDraggingEnabled
        {
            get => (bool)GetValue(RealTimeDraggingEnabledProperty);
            set => SetValue(RealTimeDraggingEnabledProperty, value);
        }

        /// <summary>
        /// Gets or sets scroll Extent maximum size. Controls maximum offset of scroll.
        /// Default is <see cref="Size.Empty"/>.
        /// </summary>
        public static DependencyProperty ExtentSizeProperty = DependencyProperty.Register(nameof(ExtentSize), typeof(Size), typeof(RichItemsControl), new FrameworkPropertyMetadata(Size.Empty));
        /// <summary>
        /// Gets or sets scroll Extent maximum size. Controls maximum offset of scroll.
        /// Default is <see cref="Size.Empty"/>.
        /// </summary>
        public Size ExtentSize
        {
            get => (Size)GetValue(ExtentSizeProperty);
            set => SetValue(ExtentSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets whether <see cref="RichCanvas"/> has negative scrolling and panning.
        /// Default is <see langword="true"/>.
        /// </summary>
        public static DependencyProperty EnableNegativeScrollingProperty = DependencyProperty.Register(nameof(EnableNegativeScrolling), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(true));
        /// <summary>
        /// Gets or sets whether <see cref="RichCanvas"/> has negative scrolling and panning.
        /// Default is <see langword="true"/>.
        /// </summary>
        public bool EnableNegativeScrolling
        {
            get => (bool)GetValue(EnableNegativeScrollingProperty);
            set => SetValue(EnableNegativeScrollingProperty, value);
        }

        /// <summary>
        /// Gets or sets whether you can select multiple elements or not.
        /// Default is <see langword="true"/>.
        /// </summary>
        public static DependencyProperty CanSelectMultipleItemsProperty = DependencyProperty.Register(nameof(CanSelectMultipleItems), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(true, OnCanSelectMultipleItemsChanged));

        /// <summary>
        /// Gets or sets whether you can select multiple elements or not.
        /// Default is <see langword="true"/>.
        /// </summary>
        public new bool CanSelectMultipleItems
        {
            get => (bool)GetValue(CanSelectMultipleItemsProperty);
            set => SetValue(CanSelectMultipleItemsProperty, value);
        }

        /// <summary>
        /// Gets or sets whether <see cref="RichCanvas"/> has selection enabled.
        /// Default is <see langword="true"/>.
        /// </summary>
        public static DependencyProperty SelectionEnabledProperty = DependencyProperty.Register(nameof(SelectionEnabled), typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(true));
        /// <summary>
        /// Gets or sets whether <see cref="RichCanvas"/> has selection enabled.
        /// Default is <see langword="true"/>.
        /// </summary>
        public bool SelectionEnabled
        {
            get => (bool)GetValue(SelectionEnabledProperty);
            set => SetValue(SelectionEnabledProperty, value);
        }

        /// <summary>
        /// Gets or sets whether <see cref="PanningGrid.ScrollOwner"/> vertical scrollbar visibility.
        /// Default is <see cref="ScrollBarVisibility.Visible"/>.
        /// </summary>
        public static DependencyProperty VerticalScrollBarVisibilityProperty = DependencyProperty.Register(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(RichItemsControl), new FrameworkPropertyMetadata(ScrollBarVisibility.Visible, OnVerticalScrollBarVisiblityChanged));
        /// <summary>
        /// Gets or sets whether <see cref="PanningGrid.ScrollOwner"/> vertical scrollbar visibility.
        /// Default is <see cref="ScrollBarVisibility.Visible"/>.
        /// </summary>
        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
            set => SetValue(VerticalScrollBarVisibilityProperty, value);
        }

        /// <summary>
        /// Gets or sets whether <see cref="PanningGrid.ScrollOwner"/> horizontal scrollbar visibility.
        /// Default is <see cref="ScrollBarVisibility.Visible"/>.
        /// </summary>
        public static DependencyProperty HorizontalScrollBarVisibilityProperty = DependencyProperty.Register(nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(RichItemsControl), new FrameworkPropertyMetadata(ScrollBarVisibility.Visible, OnHorizontalScrollBarVisiblityChanged));
        /// <summary>
        /// Gets or sets whether <see cref="PanningGrid.ScrollOwner"/> horizontal scrollbar visibility.
        /// Default is <see cref="ScrollBarVisibility.Visible"/>.
        /// </summary>
        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
            set => SetValue(HorizontalScrollBarVisibilityProperty, value);
        }

        #endregion

        #region Internal Properties
        internal RichCanvas? ItemsHost => MainPanel;
        internal TransformGroup? SelectionRectangleTransform { get; private set; }
        internal bool IsPanning { get; set; } = false;
        internal bool IsZooming { get; set; } = true;
        internal bool IsDrawing => _isDrawing;
        internal RichItemContainer CurrentDrawingItem => _drawingGesture.CurrentItem;
        internal bool HasCustomBehavior { get; set; }
        internal IList BaseSelectedItems => base.SelectedItems;
        internal bool InitializedScrollBarVisiblity { get; private set; }

        #endregion

        #region Constructors

        static RichItemsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RichItemsControl), new FrameworkPropertyMetadata(typeof(RichItemsControl)));
        }
        /// <summary>
        /// Creates a new instance of <see cref="RichItemsControl"/>
        /// </summary>
        public RichItemsControl()
        {
            AppliedTransform = new TransformGroup()
            {
                Children = new TransformCollection
                {
                    ScaleTransform, TranslateTransform
                }
            };
            DragBehavior.ItemsControl = this;
            _selectingGesture = new Selecting(this);
            _drawingGesture = new Gestures.Drawing(this);
        }

        #endregion

        #region Override Methods

        /// <inheritdoc/>
        public override void OnApplyTemplate()
        {
            var selectionRectangle = (Rectangle)GetTemplateChild(SelectionRectangleName);
            selectionRectangle.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    new ScaleTransform()
                }
            };
            SelectionRectangleTransform = (TransformGroup)selectionRectangle.RenderTransform;

            MainPanel = (RichCanvas)GetTemplateChild(DrawingPanelName);
            MainPanel.ItemsOwner = this;
            SetCachingMode(DisableCache);

            _canvasContainer = (PanningGrid)GetTemplateChild(CanvasContainerName);
            _canvasContainer.Initialize(this);

            TranslateTransform.Changed += OnTranslateChanged;
            ScaleTransform.Changed += OnScaleChanged;
        }

        /// <inheritdoc/>
        protected override bool IsItemItsOwnContainerOverride(object item) => item is RichItemContainer;

        /// <inheritdoc/>
        protected override DependencyObject GetContainerForItemOverride() => new RichItemContainer
        {
            RenderTransform = new TransformGroup
            {
                Children = new TransformCollection(new Transform[] { new ScaleTransform(), new TranslateTransform() })
            }
        };

        /// <inheritdoc/>
        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e) {
            RightClickMousePosition = e.GetPosition(ItemsHost);
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (IsPanning)
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                Point position = e.GetPosition(ItemsHost);
                if (!VisualHelper.HasScrollBarParent((DependencyObject)e.OriginalSource))
                {
                    if (_currentDrawingIndexes.Count > 0)
                    {
                        for (int i = 0; i < _currentDrawingIndexes.Count; i++)
                        {
                            var container = (RichItemContainer)ItemContainerGenerator.ContainerFromIndex(_currentDrawingIndexes[i]);
                            if (container != null)
                            {
                                if (container.IsValid())
                                {
                                    container.IsDrawn = true;

                                    _currentDrawingIndexes.Remove(_currentDrawingIndexes[i]);
                                }
                                else
                                {
                                    CaptureMouse();
                                    _isDrawing = true;
                                    _drawingGesture.OnMouseDown(container, position);
                                    _currentDrawingIndexes.Remove(_currentDrawingIndexes[i]);
                                    break;
                                }
                            }
                        }
                    }

                    if (SelectionEnabled && !_isDrawing && !IsDragging && !HasCustomBehavior)
                    {
                        IsSelecting = true;
                        _selectingGesture.OnMouseDown(position);
                        CaptureMouse();
                    }

                    if (!SelectionEnabled && (base.SelectedItems.Count > 0 || SelectedItem != null))
                    {
                        UnselectAll();
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (base.SelectedItems.Count > 0 || SelectedItem != null)
            {
                UnselectAll();
            }
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            MousePosition = new Point(e.GetPosition(MainPanel).X, e.GetPosition(MainPanel).Y);

            if (_isDrawing)
            {
                _drawingGesture.OnMouseMove(MousePosition);
            }
            else if (IsSelecting)
            {
                _selectingGesture.OnMouseMove(MousePosition);

                if (RealTimeSelectionEnabled || !CanSelectMultipleItems)
                {
                    SelectBySelectionRectangle();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isDrawing)
            {
                _isDrawing = false;
                var drawnItem = _drawingGesture.OnMouseUp();

                RaiseDrawEndedEvent(drawnItem.DataContext);
                _drawingGesture.Dispose();

                ItemsHost?.InvalidateMeasure();
            }
            else if (!IsDragging && IsSelecting)
            {
                IsSelecting = false;

                if (!RealTimeSelectionEnabled && CanSelectMultipleItems)
                {
                    SelectBySelectionRectangle();

                    IList selected = SelectedItems;

                    if (selected != null)
                    {
                        IList added = base.SelectedItems;
                        for (var i = 0; i < added.Count; i++)
                        {
                            // Ensure no duplicates are added
                            if (!selected.Contains(added[i]))
                            {
                                selected.Add(added[i]);
                            }
                        }
                    }
                }
                else if (!RealTimeSelectionEnabled && !CanSelectMultipleItems)
                {
                    if (_selectedContainer != null)
                    {
                        SelectedItem = _selectedContainer.DataContext;
                    }
                    else
                    {
                        SelectedItem = null;
                    }
                }

            }
            if (IsPanning)
            {
                Cursor = Cursors.Arrow;
            }
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
            Focus();
        }

        /// <inheritdoc/>
        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            if (e.NewStartingIndex != -1 && e.Action == NotifyCollectionChangedAction.Add)
            {
                var container = (RichItemContainer)ItemContainerGenerator.ContainerFromIndex(e.NewStartingIndex);
                if (!container.IsValid())
                {
                    _currentDrawingIndexes.Add(e.NewStartingIndex);
                }
            }
        }

        #endregion

        #region Properties Callbacks
        private static void OnDisableCacheChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RichItemsControl)d).SetCachingMode((bool)e.NewValue);

        private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RichItemsControl)d).OverrideTranslate((Point)e.NewValue);

        private static void OnDisableScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RichItemsControl)d).OnDisableScrollChanged((bool)e.NewValue);

        private void OnDisableScrollChanged(bool disabled)
        {
            if (!disabled)
            {
                ScrollContainer?.SetCurrentScroll();
            }
            if (ScrollContainer != null && ScrollContainer.ScrollOwner != null)
            {
                var scrollBarVisibllity = disabled ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
                ScrollContainer.ScrollOwner.HorizontalScrollBarVisibility = scrollBarVisibllity;
                ScrollContainer.ScrollOwner.VerticalScrollBarVisibility = scrollBarVisibllity;
                ScrollContainer.ScrollOwner.InvalidateScrollInfo();
            }
        }

        private static object ConstarainScaleToRange(DependencyObject d, object value)
        {
            var itemsControl = (RichItemsControl)d;

            if (itemsControl.DisableZoom)
            {
                return itemsControl.Scale;
            }

            double num = (double)value;
            double minimum = itemsControl.MinScale;
            if (num < minimum)
            {
                return minimum;
            }

            double maximum = itemsControl.MaxScale;
            if (num > maximum)
            {
                return maximum;
            }

            return value;
        }
        private static void OnScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RichItemsControl)d).OverrideScale((double)e.NewValue);

        private static object CoerceMinimumScale(DependencyObject d, object value)
            => (double)value > 0 ? value : 0.1;

        private static void OnMinimumScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var zoom = (RichItemsControl)d;
            zoom.CoerceValue(MaxScaleProperty);
        }

        private static void OnMaxScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var zoom = (RichItemsControl)d;
            zoom.CoerceValue(MinScaleProperty);
        }

        private static object CoerceMaxScale(DependencyObject d, object value)
        {
            var zoom = (RichItemsControl)d;
            var min = zoom.MinScale;

            return (double)value < min ? 2d : value;
        }

        private static void OnDisableAutoPanningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((RichItemsControl)d).OnDisableAutoPanningChanged((bool)e.NewValue);

        private static void OnAutoPanTickRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RichItemsControl)d).UpdateTimerInterval();

        private static object CoerceScrollFactor(DependencyObject d, object value)
            => (double)value == 0 ? 10d : value;

        private static object CoerceScaleFactor(DependencyObject d, object value)
            => (double)value == 0 ? 1.1d : value;

        private static void OnCanSelectMultipleItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RichItemsControl)d).CanSelectMultipleItemsUpdated((bool)e.NewValue);

        private static void OnVerticalScrollBarVisiblityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RichItemsControl)d).OnScrollBarVisiblityChanged((ScrollBarVisibility)e.NewValue, true);

        private static void OnHorizontalScrollBarVisiblityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((RichItemsControl)d).OnScrollBarVisiblityChanged((ScrollBarVisibility)e.NewValue);

        #endregion

        #region Selection

        /// <summary>
        /// Selects all elements inside <see cref="SelectionRectangle"/>
        /// </summary>
        public void SelectBySelectionRectangle()
        {
            RectangleGeometry geom = GetSelectionRectangleCurrentGeometry();

            if (SelectedItems?.Count > 0 && CanSelectMultipleItems)
            {
                SelectedItems?.Clear();
            }

            if (SelectedItems is null && base.SelectedItems.Count > 0 && CanSelectMultipleItems)
            {
                UnselectAll();
            }

            if (CanSelectMultipleItems)
            {
                BeginUpdateSelectedItems();
            }

            VisualTreeHelper.HitTest(MainPanel, null,
                new HitTestResultCallback(OnHitTestResultCallback),
                new GeometryHitTestParameters(geom));

            if (CanSelectMultipleItems) {
                EndUpdateSelectedItems();
            }

            if (!CanSelectMultipleItems && RealTimeSelectionEnabled)
            {
                if (SelectedItems != null && !SelectedItems.Contains(SelectedItem) && SelectedItem != null)
                {
                    SelectedItem = null;
                    if (_selectedContainer != null)
                    {
                        _selectedContainer.IsSelected = false;
                        _selectedContainer = null;
                    }
                }
                SelectedItems?.Clear();
            }
            else if (!CanSelectMultipleItems && !RealTimeSelectionEnabled)
            {
                if (_selectedContainer != null)
                {
                    _selectedContainer = null;
                }
            }
        }

        /// <summary>
        /// Returns the elements that intersect with <paramref name="area"/>
        /// </summary>
        /// <param name="area"></param>
        /// <returns></returns>
        public List<object> GetElementsInArea(Rect area)
        {
            var intersectedElements = new List<object>();
            var rectangleGeometry = new RectangleGeometry(area);
            VisualTreeHelper.HitTest(MainPanel, null,
                new HitTestResultCallback((HitTestResult result) =>
                {
                    var geometryHitTestResult = (GeometryHitTestResult)result;
                    if (geometryHitTestResult.IntersectionDetail != IntersectionDetail.Empty)
                    {
                        var container = VisualHelper.GetParentContainer(geometryHitTestResult.VisualHit);
                        intersectedElements.Add(container.DataContext);
                    }
                    return HitTestResultBehavior.Continue;
                }),
                new GeometryHitTestParameters(rectangleGeometry));
            return intersectedElements;
        }

        /// <inheritdoc/>
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            if (!IsSelecting && CanSelectMultipleItems)
            {
                IList selected = SelectedItems;

                if (selected != null)
                {
                    IList added = e.AddedItems;
                    IList removed = e.RemovedItems;
                    for (var i = 0; i < added.Count; i++)
                    {
                        // Ensure no duplicates are added
                        if (!selected.Contains(added[i]))
                        {
                            selected.Add(added[i]);
                        }
                    }

                    for (var i = 0; i < removed.Count; i++)
                    {
                        selected.Remove(removed[i]);
                    }
                }
            }
            else if (!IsSelecting && !CanSelectMultipleItems)
            {
                var added = e.AddedItems;
                if (added.Count == 1)
                {
                    if (_selectedContainer != null && added[0] != _selectedContainer.DataContext)
                    {
                        _selectedContainer.IsSelected = false;
                    }
                }
            }
        }

        private static void OnSelectedItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((RichItemsControl)d).OnSelectedItemsSourceChanged((IList)e.OldValue, (IList)e.NewValue);

        private void OnSelectedItemsSourceChanged(IList oldValue, IList newValue)
        {
            if (oldValue is INotifyCollectionChanged oc)
            {
                oc.CollectionChanged -= OnSelectedItemsChanged;
            }

            if (newValue is INotifyCollectionChanged nc)
            {
                nc.CollectionChanged += OnSelectedItemsChanged;
            }

            if (CanSelectMultipleItems)
            {
                IList selectedItems = base.SelectedItems;

                BeginUpdateSelectedItems();
                selectedItems.Clear();
                if (newValue != null)
                {
                    for (var i = 0; i < newValue.Count; i++)
                    {
                        selectedItems.Add(newValue[i]);
                    }
                }
                EndUpdateSelectedItems();
            }
        }

        private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    if (CanSelectMultipleItems)
                    {
                        base.SelectedItems.Clear();
                    }
                    break;

                case NotifyCollectionChangedAction.Add:
                    if (CanSelectMultipleItems)
                    {
                        IList? newItems = e.NewItems;
                        if (newItems != null)
                        {
                            IList selectedItems = base.SelectedItems;
                            for (var i = 0; i < newItems.Count; i++)
                            {
                                selectedItems.Add(newItems[i]);
                            }
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (CanSelectMultipleItems)
                    {
                        IList? oldItems = e.OldItems;
                        if (oldItems != null)
                        {
                            IList selectedItems = base.SelectedItems;
                            for (var i = 0; i < oldItems.Count; i++)
                            {
                                selectedItems.Remove(oldItems[i]);
                            }
                        }
                    }
                    break;
            }
        }

        private HitTestResultBehavior OnHitTestResultCallback(HitTestResult result)
        {
            var geometryHitTestResult = (GeometryHitTestResult)result;
            if (geometryHitTestResult.VisualHit.DependencyObjectType.SystemType != typeof(RichItemContainer) && geometryHitTestResult.IntersectionDetail != IntersectionDetail.Empty)
            {
                var container = VisualHelper.GetParentContainer(geometryHitTestResult.VisualHit);
                if (container != null && container.IsSelectable)
                {
                    if (CanSelectMultipleItems)
                    {
                        if (SelectedItems != null && !SelectedItems.Contains(container.DataContext)) {
                            SelectedItems.Add(container.DataContext);
                        }
                        container.IsSelected = true;
                    }
                    else
                    {
                        if (SelectedItem == null && RealTimeSelectionEnabled)
                        {
                            container.IsSelected = true;
                            _selectedContainer = container;
                        }
                        if (RealTimeSelectionEnabled)
                        {
                            SelectedItems?.Add(container.DataContext);
                        }
                        else
                        {
                            if (_selectedContainer == null)
                            {
                                _selectedContainer = container;
                            }
                        }
                    }
                }
            }
            return HitTestResultBehavior.Continue;
        }

        private RectangleGeometry GetSelectionRectangleCurrentGeometry()
        {
            var scaleTransform = (ScaleTransform?)SelectionRectangleTransform?.Children[0];
            if (scaleTransform != null)
            {
                var currentSelectionTop = scaleTransform.ScaleY < 0 ? SelectionRectangle.Top - SelectionRectangle.Height : SelectionRectangle.Top;
                var currentSelectionLeft = scaleTransform.ScaleX < 0 ? SelectionRectangle.Left - SelectionRectangle.Width : SelectionRectangle.Left;
                return new RectangleGeometry(new Rect(currentSelectionLeft, currentSelectionTop, SelectionRectangle.Width, SelectionRectangle.Height));
            }
            return new RectangleGeometry(Rect.Empty);
        }

        internal void UpdateSelectedItem(RichItemContainer container)
        {
            if (_selectedContainer != null)
            {
                _selectedContainer.IsSelected = false;
            }
            _selectedContainer = container;
        }

        #endregion

        #region Handlers And Private Methods

        private void CanSelectMultipleItemsUpdated(bool value) => base.CanSelectMultipleItems = value;

        private void OnScaleChanged(object? sender, EventArgs e)
        {
            _fromEvent = true;
            Scale = ScaleTransform.ScaleX;
            RoutedEventArgs newEventArgs = new RoutedEventArgs(ZoomingEvent, new Point(ScaleTransform.ScaleX, ScaleTransform.ScaleY));
            RaiseEvent(newEventArgs);
            _fromEvent = false;
        }

        private void OnTranslateChanged(object? sender, EventArgs e)
        {
            _fromEvent = true;
            TranslateOffset = new Point(TranslateTransform.X, TranslateTransform.Y);
            RaiseScrollingEvent(e);
            _fromEvent = false;
        }

        private void RaiseScrollingEvent (object context) {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(ScrollingEvent, context);
            RaiseEvent(newEventArgs);
        }

        public void RaiseDraggingEvent (object context, Point currentPosition) {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(DraggingEvent, new {
                Container = this,
                Target = context,
                Position = currentPosition
            });
            RaiseEvent(newEventArgs);
        }

        private void SetCachingMode(bool disable)
        {
            if (MainPanel != null)
            {
                if (!disable)
                {
                    MainPanel.CacheMode = new BitmapCache()
                    {
                        EnableClearType = false,
                        SnapsToDevicePixels = false,
                        RenderAtScale = Scale
                    };
                }
                else
                {
                    MainPanel.CacheMode = null;
                }
            }
        }

        private void OverrideTranslate(Point newValue)
        {
            if (!_fromEvent)
            {
                TranslateTransform.X = newValue.X;
                TranslateTransform.Y = newValue.Y;
                ScrollContainer?.SetCurrentScroll();
            }
        }

        private void OverrideScale(double newValue)
        {
            if (!_fromEvent)
            {
                ScaleTransform.ScaleX = newValue;
                ScaleTransform.ScaleY = newValue;
                CoerceValue(ScaleProperty);
                ScrollContainer?.SetCurrentScroll();
            }
        }

        private void OnDisableAutoPanningChanged(bool shouldDisable)
        {
            if (!shouldDisable)
            {
                if (_autoPanTimer == null)
                {
                    _autoPanTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(AutoPanTickRate), DispatcherPriority.Background, new EventHandler(HandleAutoPanning), Dispatcher);
                    _autoPanTimer.Start();
                }
                else
                {
                    _autoPanTimer.Interval = TimeSpan.FromMilliseconds(AutoPanTickRate);
                    _autoPanTimer.Start();
                }
            }
            else
            {
                if (_autoPanTimer != null)
                {
                    _autoPanTimer.Stop();
                }
            }
        }

        private void HandleAutoPanning(object? sender, EventArgs e)
        {
            if (IsMouseOver && Mouse.LeftButton == MouseButtonState.Pressed && Mouse.Captured != null && !IsMouseCapturedByScrollBar() && !IsPanning && ScrollContainer != null)
            {
                var mousePosition = Mouse.GetPosition(ScrollContainer);
                var transformedPosition = Mouse.GetPosition(ItemsHost);

                if (mousePosition.Y <= 0)
                {
                    if (_isDrawing)
                    {
                        CurrentDrawingItem.Height = Math.Abs(transformedPosition.Y - CurrentDrawingItem.Top);
                    }

                    ScrollContainer.PanVertically(-AutoPanSpeed);
                }
                else if (mousePosition.Y >= ScrollContainer.ViewportHeight)
                {
                    if (_isDrawing)
                    {
                        CurrentDrawingItem.Height = Math.Abs(transformedPosition.Y - CurrentDrawingItem.Top);
                    }
                    ScrollContainer.PanVertically(AutoPanSpeed);
                }

                if (mousePosition.X <= 0)
                {
                    if (_isDrawing)
                    {
                        CurrentDrawingItem.Width = Math.Abs(transformedPosition.X - CurrentDrawingItem.Left);
                    }
                    ScrollContainer.PanHorizontally(-AutoPanSpeed);
                }
                else if (mousePosition.X >= ScrollContainer.ViewportWidth)
                {
                    if (_isDrawing)
                    {
                        CurrentDrawingItem.Width = Math.Abs(transformedPosition.X - CurrentDrawingItem.Left);
                    }
                    ScrollContainer.PanHorizontally(AutoPanSpeed);
                }

                if (IsSelecting)
                {
                    _selectingGesture.Update(transformedPosition);
                }
            }
        }

        private static bool IsMouseCapturedByScrollBar()
        {
            return Mouse.Captured.GetType() == typeof(Thumb) || Mouse.Captured.GetType() == typeof(RepeatButton);
        }

        private void UpdateTimerInterval()
        {
            if (_autoPanTimer != null)
            {
                _autoPanTimer.Interval = TimeSpan.FromMilliseconds(AutoPanTickRate);
            }
        }
        private void RaiseDrawEndedEvent(object context)
        {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(DrawingEndedEvent, context);
            RaiseEvent(newEventArgs);
        }

        internal void OnScrollBarVisiblityChanged(ScrollBarVisibility scrollBarVisibility, bool isVertical = false, bool initalized = false)
        {
            if (initalized)
            {
                InitializedScrollBarVisiblity = true;
            }

            if (ScrollContainer != null && ScrollContainer.ScrollOwner != null)
            {
                if (isVertical)
                {
                    ScrollContainer.ScrollOwner.VerticalScrollBarVisibility = scrollBarVisibility;
                }
                else
                {
                    ScrollContainer.ScrollOwner.HorizontalScrollBarVisibility = scrollBarVisibility;
                }
                ScrollContainer.ScrollOwner.InvalidateScrollInfo();
            }
        }

        #endregion
    }
}
