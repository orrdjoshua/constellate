using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Constellate.App.Controls.Panes;
using Constellate.Core.Capabilities.Panes;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

namespace Constellate.App.Controls
{
    public partial class ChildPaneView : UserControl
    {
        private PaneChrome? _root;
        private ScrollViewer? _headerScroll;
        private ChildPaneDescriptor? _model;
        private MainWindowViewModel? _windowViewModel;
        private TextBlock? _emptyChildPaneBodyText;
        private Border? _boundResourceContextReadout;
        private ContentControl? _realizedPaneDefinitionHost;
        private TextBlock? _boundResourceContextTitleText;
        private TextBlock? _boundResourceContextSubtitleText;
        private ComboBox? _paneDefinitionPicker;
        private TextBox? _inlineRenameEditor;

        public ChildPaneView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _root = this.FindControl<PaneChrome>("ChildChrome");
            _headerScroll = _root?.FindControl<ScrollViewer>("PART_HeaderScroll");
            _emptyChildPaneBodyText = this.FindControl<TextBlock>("EmptyChildPaneBodyText");
            _boundResourceContextReadout = this.FindControl<Border>("BoundResourceContextReadout");
            _realizedPaneDefinitionHost = this.FindControl<ContentControl>("RealizedPaneDefinitionHost");
            _boundResourceContextTitleText = this.FindControl<TextBlock>("BoundResourceContextTitleText");
            _boundResourceContextSubtitleText = this.FindControl<TextBlock>("BoundResourceContextSubtitleText");
            _paneDefinitionPicker = this.FindControl<ComboBox>("PaneDefinitionPicker");
            _inlineRenameEditor = this.FindControl<TextBox>("InlineRenameEditor");
            Debug.WriteLine($"[HeaderScroll][Child][Wire] init: chrome={_root is not null} headerScroll={_headerScroll is not null}");

            // React to item replacement in the ItemsControl:
            // ChildPaneDescriptor is a record; VM replaces the instance in the collection,
            // which triggers DataContextChanged for this view.
            DataContextChanged += OnDataContextChanged;
            // Track header-height to enforce min size: header must always remain fully visible.
            if (_root?.HeaderBorder is { } hdr)
                hdr.PropertyChanged += OnHeaderMetricChanged;

            // Also run on initial attach (in case DataContext was available before this view attached).
            AttachedToVisualTree += (_, __) => Dispatcher.UIThread.Post(() =>
            {
                RewireWindowViewModel();
                ApplyFloatingMinimizedWidthIfNeeded();
                QueueBoundResourceFallbackRefresh();
                QueueInlineRenameRefresh();
                QueuePaneDefinitionRealizationRefresh();
            }, DispatcherPriority.Background);
            DetachedFromVisualTree += (_, __) => UnwireWindowViewModel();
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            _model = DataContext as ChildPaneDescriptor;
            RewireWindowViewModel();
            // Defer to ensure region measurements are valid before computing desired width.
            Dispatcher.UIThread.Post(ApplyFloatingMinimizedWidthIfNeeded, DispatcherPriority.Background);
            // Apply min-height rule initially after data context update
            Dispatcher.UIThread.Post(ApplyMinHeightFromHeader, DispatcherPriority.Background);
            QueueInlineRenameRefresh();
            QueuePaneDefinitionRealizationRefresh();
            QueueBoundResourceFallbackRefresh();
        }

        private void QueueBoundResourceFallbackRefresh()
        {
            Dispatcher.UIThread.Post(SyncBoundResourceContextReadout, DispatcherPriority.Background);
        }

        private void QueueInlineRenameRefresh()
        {
            Dispatcher.UIThread.Post(SyncInlineRenameEditor, DispatcherPriority.Input);
        }

        private void QueuePaneDefinitionRealizationRefresh()
        {
            Dispatcher.UIThread.Post(SyncRealizedPaneDefinitionHost, DispatcherPriority.Background);
        }

        private void OnHeaderMetricChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty ||
                e.Property?.Name == "Bounds" ||
                e.Property == BoundsProperty)
            {
                ApplyMinHeightFromHeader();
            }
        }

        private void ApplyMinHeightFromHeader()
        {
            if (_root?.HeaderBorder is null) return;
            var headerH = _root.HeaderBorder.Bounds.Height;
            // Provide a small cushion for borders/padding; header must remain visible.
            MinHeight = Math.Max(1.0, headerH + 2.0);
        }

        private void ApplyFloatingMinimizedWidthIfNeeded()
        {
            if (_root is null || _model is null) return;

            // Only apply to floating minimized children (ParentId == null && IsMinimized)
            if (_model.ParentId is not null || !_model.IsMinimized) return;

            try
            {
                var desired = ComputeMinimizedHeaderDesiredWidth(_root);
                const double headerOnlyHeight = 56.0;
                desired = Math.Max(120.0, desired);
                var vm = PaneChromeInputHelper.ResolveMainWindowViewModel(this);
                vm?.SetFloatingChildGeometry(_model.Id, _model.FloatingX, _model.FloatingY, desired, headerOnlyHeight);
                Debug.WriteLine($"[ChildPaneView] minimized floating child width set -> {desired:0.0} (id={_model.Id})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChildPaneView] minimized width compute error: {ex.Message}");
            }
        }

        private void RewireWindowViewModel()
        {
            var nextWindowViewModel = PaneChromeInputHelper.ResolveMainWindowViewModel(this);
            if (ReferenceEquals(_windowViewModel, nextWindowViewModel))
            {
                QueuePaneDefinitionRealizationRefresh();
                QueueBoundResourceFallbackRefresh();
                return;
            }

            UnwireWindowViewModel();

            _windowViewModel = nextWindowViewModel;
            if (_windowViewModel is not null)
            {
                _windowViewModel.PropertyChanged += OnWindowViewModelPropertyChanged;
                _windowViewModel.SeededPaneDefinitions.CollectionChanged += OnSeededPaneCatalogCollectionChanged;
                _windowViewModel.SeededPaneWorkspaces.CollectionChanged += OnSeededPaneCatalogCollectionChanged;
            }

            QueuePaneDefinitionRealizationRefresh();
            QueueBoundResourceFallbackRefresh();
        }

        private void UnwireWindowViewModel()
        {
            if (_windowViewModel is not null)
            {
                _windowViewModel.PropertyChanged -= OnWindowViewModelPropertyChanged;
                _windowViewModel.SeededPaneDefinitions.CollectionChanged -= OnSeededPaneCatalogCollectionChanged;
                _windowViewModel.SeededPaneWorkspaces.CollectionChanged -= OnSeededPaneCatalogCollectionChanged;
                _windowViewModel = null;
            }
        }

        private void OnSeededPaneCatalogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            QueuePaneDefinitionRealizationRefresh();
        }

        private PaneDefinitionDescriptor? ResolvePaneDefinition()
        {
            if (_model is null || string.IsNullOrWhiteSpace(_model.DefinitionId))
            {
                return null;
            }

            var definition = _windowViewModel?.SeededPaneDefinitions.FirstOrDefault(candidate =>
                string.Equals(candidate.PaneDefinitionId, _model.DefinitionId, StringComparison.Ordinal));
            if (definition is not null)
            {
                return definition;
            }

            return new SeededPaneCatalog().FindPaneDefinition(_model.DefinitionId!);
        }

        private void SyncRealizedPaneDefinitionHost()
        {
            if (_realizedPaneDefinitionHost is null || _model is null)
            {
                return;
            }

            var definition = ResolvePaneDefinition();
            if (definition is not null)
            {
                _realizedPaneDefinitionHost.Content = PaneDefinitionRuntimeBuilder.Build(_model, definition, _windowViewModel);
                _realizedPaneDefinitionHost.IsVisible = true;
                return;
            }

            if (_model.IsAuthorMode)
            {
                _realizedPaneDefinitionHost.Content = PaneDefinitionRuntimeBuilder.BuildBlankAuthorModeCanvasSurface(_model, _windowViewModel);
                _realizedPaneDefinitionHost.IsVisible = true;
                return;
            }

            _realizedPaneDefinitionHost.Content = null;
            _realizedPaneDefinitionHost.IsVisible = false;
        }

        private void SyncInlineRenameEditor()
        {
            if (_inlineRenameEditor is null || _model is null || !_model.IsInlineRenaming)
            {
                return;
            }

            _inlineRenameEditor.Text = _model.Title;
            _inlineRenameEditor.Focus();
            _inlineRenameEditor.SelectAll();
        }

        private void CommitInlineRename()
        {
            if (_model is null || _windowViewModel is null)
            {
                return;
            }

            _windowViewModel.TryCommitPaneRename(_model.Id, _inlineRenameEditor?.Text);
        }

        private void OnWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName) &&
                e.PropertyName != nameof(MainWindowViewModel.HasActiveResourceDetailSurface) &&
                e.PropertyName != nameof(MainWindowViewModel.ActiveResourceDetailPrimaryPaneId) &&
                e.PropertyName != nameof(MainWindowViewModel.ActiveResourceDetailSurfaceTitle) &&
                e.PropertyName != nameof(MainWindowViewModel.ActiveResourceDetailSurfaceSubtitle))
            {
                return;
            }

            QueueBoundResourceFallbackRefresh();
        }

        private void SyncBoundResourceContextReadout()
        {
            if (_emptyChildPaneBodyText is null ||
                _boundResourceContextReadout is null ||
                _boundResourceContextTitleText is null ||
                _boundResourceContextSubtitleText is null)
            {
                return;
            }

            if (_model?.HasDirectBoundResourceContextPresentation == true)
            {
                _boundResourceContextReadout.IsVisible = false;
                _boundResourceContextTitleText.Text = string.Empty;
                _boundResourceContextSubtitleText.Text = string.Empty;
                _emptyChildPaneBodyText.Text = _model.PaneDefaultEmptyBodyText;
                return;
            }

            var isCurrentShellPane = string.Equals(_model?.Id, "shell.current", StringComparison.Ordinal);
            var hasActiveResourceDetailSurface = _windowViewModel?.HasActiveResourceDetailSurface == true;
            var primaryPaneId = _windowViewModel?.ActiveResourceDetailPrimaryPaneId ?? string.Empty;
            var isPrimaryConsumerPane = !string.IsNullOrWhiteSpace(_model?.Id) &&
                                        string.Equals(_model!.Id, primaryPaneId, StringComparison.Ordinal);

            _boundResourceContextReadout.IsVisible = hasActiveResourceDetailSurface &&
                                                    isPrimaryConsumerPane &&
                                                    isCurrentShellPane;
            _boundResourceContextTitleText.Text = _boundResourceContextReadout.IsVisible
                ? _windowViewModel?.ActiveResourceDetailSurfaceTitle ?? "Resource Detail"
                : string.Empty;
            _boundResourceContextSubtitleText.Text = _boundResourceContextReadout.IsVisible
                ? _windowViewModel?.ActiveResourceDetailSurfaceSubtitle ?? string.Empty
                : string.Empty;
            
            if (isCurrentShellPane && hasActiveResourceDetailSurface && isPrimaryConsumerPane)
            {
                _emptyChildPaneBodyText.Text = "Current shell pane is consuming the active resource detail binding.";
            }
            else if (isCurrentShellPane && hasActiveResourceDetailSurface)
            {
                _emptyChildPaneBodyText.Text = "Active resource detail binding is currently consumed by another pane.";
            }
            else if (isCurrentShellPane)
            {
                _emptyChildPaneBodyText.Text = "Waiting for an active resource detail binding.";
            }
            else
            {
                _emptyChildPaneBodyText.Text = "(empty child pane)";
            }
        }

        private async void OnLoadExistingPaneClick(object? sender, RoutedEventArgs e)
        {
            if (_model is null ||
                _windowViewModel is null ||
                _paneDefinitionPicker?.SelectedItem is not PaneDefinitionDescriptor definition)
            {
                return;
            }

            var hostWindow = ResolveHostWindow();
            if (hostWindow is null)
            {
                return;
            }

            var loadDecision = await PaneDefinitionConfirmationDialog.ShowLoadWarningAsync(
                hostWindow,
                _model,
                definition,
                canUpdateCurrentDefinition: CanUpdateCurrentDefinition(_model));

            switch (loadDecision)
            {
                case PaneDefinitionLoadWarningDecision.Cancel:
                    return;

                case PaneDefinitionLoadWarningDecision.SaveAsNewThenLoad:
                    if (!_windowViewModel.TrySaveChildPaneAsNewDefinition(_model.Id))
                    {
                        return;
                    }
                    break;

                case PaneDefinitionLoadWarningDecision.UpdateCurrentDefinitionThenLoad:
                    if (!_windowViewModel.TryUpdateChildPaneCurrentDefinition(_model.Id))
                    {
                        return;
                    }
                    break;
            }

            if (_windowViewModel.TryApplyPaneDefinitionToChildPane(_model.Id, definition.PaneDefinitionId))
            {
                QueueBoundResourceFallbackRefresh();
            }
        }

        private void OnCreateNewPaneClick(object? sender, RoutedEventArgs e)
        {
            if (_model is null || _windowViewModel is null)
            {
                return;
            }

            if (_windowViewModel.TryResetChildPaneToLocalNew(_model.Id))
            {
                QueueBoundResourceFallbackRefresh();
            }
        }

        private void OnToggleAuthorModeClick(object? sender, RoutedEventArgs e)
        {
            if (_model is null || _windowViewModel is null)
            {
                return;
            }

            _windowViewModel.TryToggleChildPaneAuthorMode(_model.Id);
        }

        private void OnAuthorModeBadgePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_model is null ||
                _windowViewModel is null ||
                !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _windowViewModel.TryToggleChildPaneAuthorMode(_model.Id);
            e.Handled = true;
        }

        private void OnTogglePaneDefinitionPanelClick(object? sender, RoutedEventArgs e)
        {
            if (_model is null || _windowViewModel is null)
            {
                return;
            }

            _windowViewModel.TryToggleChildPaneDefinitionPanelVisibility(_model.Id);
        }

        private void OnAddCanvasTextBlockElementClick(object? sender, RoutedEventArgs e)
        {
            TryAddCanvasElement(PaneElementKind.TextBlock);
        }

        private void OnAddCanvasButtonElementClick(object? sender, RoutedEventArgs e)
        {
            TryAddCanvasElement(PaneElementKind.Button);
        }

        private void OnAddCanvasLabelValueElementClick(object? sender, RoutedEventArgs e)
        {
            TryAddCanvasElement(PaneElementKind.LabelValueField);
        }

        private void OnAddCanvasTextEditorElementClick(object? sender, RoutedEventArgs e)
        {
            TryAddCanvasElement(PaneElementKind.TextEditor);
        }

        private void OnAddCanvasCommandBarElementClick(object? sender, RoutedEventArgs e)
        {
            TryAddCanvasElement(PaneElementKind.CommandBar);
        }

        private void OnAddCanvasStatusBadgeElementClick(object? sender, RoutedEventArgs e)
        {
            TryAddCanvasElement(PaneElementKind.StatusBadge);
        }

        private void TryAddCanvasElement(PaneElementKind elementKind)
        {
            if (_model is null || _windowViewModel is null)
            {
                return;
            }

            _windowViewModel.TryAddChildPaneLocalCanvasElement(_model.Id, elementKind);
        }

        private void OnResetCanvasViewportClick(object? sender, RoutedEventArgs e)
        {
            if (_model is null || _windowViewModel is null)
            {
                return;
            }

            _windowViewModel.TryResetChildPaneCanvasViewport(_model.Id);
        }

        private void OnInlineRenameKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitInlineRename();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && _model is not null && _windowViewModel is not null)
            {
                _windowViewModel.TryCancelPaneRename(_model.Id);
                e.Handled = true;
            }
        }

        private async void OnSaveAsNewDefinitionClick(object? sender, RoutedEventArgs e)
        {
            if (_model is null || _windowViewModel is null)
            {
                return;
            }

            if (!ShouldWarnAboutDefinitionOverwrite(_model))
            {
                _windowViewModel.TrySaveChildPaneAsNewDefinition(_model.Id);
                return;
            }

            var hostWindow = ResolveHostWindow();
            var currentDefinition = ResolveCurrentPaneDefinition();
            if (hostWindow is null || currentDefinition is null)
            {
                return;
            }

            var saveDecision = await PaneDefinitionConfirmationDialog.ShowOverwriteWarningAsync(
                hostWindow,
                _model,
                currentDefinition);

            switch (saveDecision)
            {
                case PaneDefinitionSaveWarningDecision.Cancel:
                    return;

                case PaneDefinitionSaveWarningDecision.UpdateCurrentDefinition:
                    _windowViewModel.TryUpdateChildPaneCurrentDefinition(_model.Id);
                    return;

                case PaneDefinitionSaveWarningDecision.SaveAsNewDefinition:
                    _windowViewModel.TrySaveChildPaneAsNewDefinition(_model.Id);
                    return;
            }
        }

        private void OnInlineRenameLostFocus(object? sender, RoutedEventArgs e)
        {
            if (_model?.IsInlineRenaming == true)
            {
                CommitInlineRename();
            }
        }

        private static double ComputeMinimizedHeaderDesiredWidth(PaneChrome chrome)
        {
            // Measure: Label + CommandBar + trailing spacer (8), plus header/root padding and border thickness.
            var labelW = chrome.LabelRegionControl?.Bounds.Width ?? 0.0;
            var cmdW = chrome.CommandBarRegionControl?.Bounds.Width ?? 0.0;
            const double trailing = 8.0;

            var headerPad = chrome.HeaderBorder?.Padding ?? new Thickness(0);
            var rootPad = chrome.RootBorder?.Padding ?? new Thickness(0);
            var rootBorder = chrome.RootBorder?.BorderThickness ?? new Thickness(0);

            var sum = labelW + cmdW + trailing
                      + headerPad.Left + headerPad.Right
                      + rootPad.Left + rootPad.Right
                      + rootBorder.Left + rootBorder.Right;
            return sum;
        }

        private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (PaneChromeInputHelper.TryBeginPressedPaneDrag(this, sender, e))
            {
                e.Handled = true;
            }
        }

        private void EmptyHeader_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            PaneChromeInputHelper.TryHandleEmptyHeaderDoubleTap(this, DataContext, e);
        }

        private void Body_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, sender))
            {
                return;
            }

            if (_model?.IsAuthorMode == true)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (PaneChromeInputHelper.TryBeginPressedPaneDrag(this, sender, e))
            {
                e.Handled = true;
            }
        }

        private void OnBodyPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_model is null || _windowViewModel is null || !_model.IsAuthorMode)
            {
                return;
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var zoomDelta = e.Delta.Y > 0 ? 0.10 : -0.10;
                if (_windowViewModel.TryZoomChildPaneCanvasViewport(_model.Id, zoomDelta))
                {
                    e.Handled = true;
                }

                return;
            }
        }

        private Window? ResolveHostWindow()
        {
            return TopLevel.GetTopLevel(this) as Window;
        }

        private PaneDefinitionDescriptor? ResolveCurrentPaneDefinition()
        {
            if (_model is null || string.IsNullOrWhiteSpace(_model.DefinitionId))
            {
                return null;
            }

            var definition = _windowViewModel?.SeededPaneDefinitions.FirstOrDefault(candidate =>
                string.Equals(candidate.PaneDefinitionId, _model.DefinitionId, StringComparison.Ordinal));
            if (definition is not null)
            {
                return definition;
            }

            return new SeededPaneCatalog().FindPaneDefinition(_model.DefinitionId!);
        }

        private static bool CanUpdateCurrentDefinition(ChildPaneDescriptor pane)
        {
            return pane.IsDefinitionBacked && !string.IsNullOrWhiteSpace(pane.DefinitionId);
        }

        private static bool ShouldWarnAboutDefinitionOverwrite(ChildPaneDescriptor pane)
        {
            if (!CanUpdateCurrentDefinition(pane))
            {
                return false;
            }

            return string.Equals(
                pane.Title.Trim(),
                pane.EffectiveDefinitionLabel.Trim(),
                StringComparison.Ordinal);
        }

        private void Header_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, true);
        }

        private void Header_OnPointerExited(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, false);
        }

        private void Body_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, true);
        }

        private void Body_OnPointerExited(object? sender, PointerEventArgs e)
        {
            PaneChromeInputHelper.SetPaneDragHover(_root, sender, false);
        }

        private void OnPaneChromePointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_root is null || _headerScroll is null)
            {
                Debug.WriteLine($"[HeaderScroll][Child] wheel: missing refs chrome={_root is not null} headerScroll={_headerScroll is not null}");
                return;
            }

            // Ignore if pointer is over body region; header-only behavior.
            var srcVisual = (e.Source as Visual) ?? (sender as Visual);
            if (IsWithinBodyRegion(srcVisual))
            {
                Debug.WriteLine($"[HeaderScroll][Child] wheel: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) region=Body ignored");
                return;
            }

            // Prefer native horizontal (Delta.X), else map vertical (Delta.Y) to horizontal.
            double dx;
            if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
            {
                dx = e.Delta.X;
                Debug.WriteLine($"[HeaderScroll][Child] dxFrom=X dx={dx:0.00}");
            }
            else
            {
                dx = -e.Delta.Y;
                Debug.WriteLine($"[HeaderScroll][Child] dxFrom=Y dx={dx:0.00}");
            }

            if (Math.Abs(dx) < 0.01)
            {
                Debug.WriteLine($"[HeaderScroll][Child] wheel: negligible dx; ignored");
                return;
            }

            var current = _headerScroll.Offset;
            var factor = 20.0; // lower sensitivity
            var extent = _headerScroll.Extent;
            var viewport = _headerScroll.Viewport;
            var maxX = Math.Max(0.0, extent.Width - viewport.Width);
            var nextX = Math.Clamp(current.X + dx * factor, 0.0, maxX);

            if (Math.Abs(nextX - current.X) > 0.5)
            {
                _headerScroll.Offset = new Vector(nextX, current.Y);
                e.Handled = true;
                Debug.WriteLine($"[HeaderScroll][Child] applied: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) currentX={current.X:0.0} maxX={maxX:0.0} nextX={nextX:0.0} factor={factor}");
            }
            else
            {
                Debug.WriteLine($"[HeaderScroll][Child] noop: src={(srcVisual?.GetType().Name ?? "null")} delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00}) currentX={current.X:0.0} maxX={maxX:0.0} nextX={nextX:0.0} (no change)");
            }
        }

        private void OnCommandBarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            OnPaneChromePointerWheelChanged(sender, e);
            Debug.WriteLine($"[HeaderScroll][Child][CmdBar] delegated wheel delta=(X={e.Delta.X:0.00},Y={e.Delta.Y:0.00})");
        }

        private bool IsWithinBodyRegion(Visual? v)
        {
            if (_root?.BodyRegionControl is null)
            {
                Debug.WriteLine($"[HeaderScroll][Child] bodyCheck: bodyRegion=null");
                return false;
            }

            for (var cur = v; cur is not null; cur = cur.GetVisualParent())
            {
                if (ReferenceEquals(cur, _root.BodyRegionControl))
                {
                    Debug.WriteLine($"[HeaderScroll][Child] bodyCheck: hit BodyRegion");
                    return true;
                }
            }
            Debug.WriteLine($"[HeaderScroll][Child] bodyCheck: not in body");
            return false;
        }
    }
}
