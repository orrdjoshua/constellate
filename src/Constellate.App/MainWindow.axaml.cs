using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Constellate.Core.Capabilities;
using Constellate.Core.Messaging;
using Constellate.Core.Scene;
using Constellate.SDK;

namespace Constellate.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IDisposable[] _eventSubscriptions;
        private readonly ShellSceneState _shellScene = EngineServices.ShellScene;

        private readonly RelayCommand _focusFirstNodeCommand;
        private readonly RelayCommand _selectFirstNodeCommand;
        private readonly RelayCommand _focusFirstPanelCommand;
        private readonly RelayCommand _selectFirstPanelCommand;
        private readonly RelayCommand _createDemoNodeCommand;
        private readonly RelayCommand _nudgeFocusedNodeCommand;
        private readonly RelayCommand _deleteFocusedNodeCommand;
        private readonly RelayCommand _attachDemoPanelCommand;
        private readonly RelayCommand _clearSelectionCommand;

        private string _lastActivitySummary = "Last Activity: app started";

        public ObservableCollection<EngineCapability> Capabilities { get; } =
            new(EngineServices.Capabilities.GetAll());

        public ICommand FocusFirstNodeCommand => _focusFirstNodeCommand;
        public ICommand SelectFirstNodeCommand => _selectFirstNodeCommand;
        public ICommand FocusFirstPanelCommand => _focusFirstPanelCommand;
        public ICommand SelectFirstPanelCommand => _selectFirstPanelCommand;
        public ICommand CreateDemoNodeCommand => _createDemoNodeCommand;
        public ICommand NudgeFocusedNodeCommand => _nudgeFocusedNodeCommand;
        public ICommand DeleteFocusedNodeCommand => _deleteFocusedNodeCommand;
        public ICommand AttachDemoPanelCommand => _attachDemoPanelCommand;
        public ICommand ClearSelectionCommand => _clearSelectionCommand;

        public MainWindowViewModel()
        {
            _eventSubscriptions =
            [
                SubscribeRefresh(EventNames.CommandInvoked, "command activity"),
                SubscribeRefresh(EventNames.SceneChanged, "scene changed"),
                SubscribeRefresh(EventNames.FocusChanged, "focus changed"),
                SubscribeRefresh(EventNames.PanelFocusChanged, "panel focus changed"),
                SubscribeRefresh(EventNames.SelectionChanged, "selection changed"),
                SubscribeRefresh(EventNames.PanelAttachmentsChanged, "panel attachments changed")
            ];

            _focusFirstNodeCommand = new RelayCommand(
                _ =>
                {
                    var firstNode = _shellScene.GetNodes().FirstOrDefault();
                    if (firstNode is not null)
                    {
                        SendCommand(
                            CommandNames.Focus,
                            new FocusEntityPayload(firstNode.Id.ToString()));
                    }
                },
                _ => _shellScene.GetNodes().Count > 0);

            _selectFirstNodeCommand = new RelayCommand(
                _ =>
                {
                    var firstNode = _shellScene.GetNodes().FirstOrDefault();
                    if (firstNode is not null)
                    {
                        SendCommand(
                            CommandNames.Select,
                            new SelectEntitiesPayload([firstNode.Id.ToString()]));
                    }
                },
                _ => _shellScene.GetNodes().Count > 0);

            _focusFirstPanelCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFirstPanelTarget() is { } panelTarget)
                    {
                        SendCommand(
                            CommandNames.FocusPanel,
                            new FocusPanelPayload(
                                panelTarget.NodeId.ToString(),
                                panelTarget.ViewRef));
                    }
                },
                _ => _shellScene.GetFirstPanelTarget() is not null);

            _selectFirstPanelCommand = new RelayCommand(
                _ =>
                {
                    if (_shellScene.GetFirstPanelTarget() is { } panelTarget)
                    {
                        SendCommand(
                            CommandNames.SelectPanel,
                            new SelectPanelPayload(
                                panelTarget.NodeId.ToString(),
                                panelTarget.ViewRef));
                    }
                },
                _ => _shellScene.GetFirstPanelTarget() is not null);

            _createDemoNodeCommand = new RelayCommand(_ =>
            {
                var index = _shellScene.GetNodes().Count + 1;
                var angle = (float)(index * 0.85);
                var radius = 0.55f + (0.08f * (index % 3));
                var position = new Vector3(
                    MathF.Cos(angle) * radius,
                    MathF.Sin(angle) * radius,
                    0f);

                SendCommand(
                    CommandNames.CreateEntity,
                    new CreateEntityPayload(
                        Type: "node",
                        Id: null,
                        Label: $"Demo Node {index}",
                        Position: position,
                        RotationEuler: Vector3.Zero,
                        Scale: new Vector3(0.45f, 0.45f, 0.45f),
                        VisualScale: 0.45f,
                        Phase: index * 0.35f));
            });

            _nudgeFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var nextPosition = focusedNode.Transform.Position + new Vector3(0.12f, 0.08f, 0f);
                    SendCommand(
                        CommandNames.UpdateEntity,
                        new UpdateEntityPayload(
                            focusedNode.Id.ToString(),
                            $"{focusedNode.Label} *",
                            nextPosition,
                            focusedNode.Transform.RotationEuler,
                            focusedNode.Transform.Scale,
                            focusedNode.VisualScale,
                            focusedNode.Phase + 0.15f));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _deleteFocusedNodeCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.Delete,
                        new DeleteEntityPayload(focusedNode.Id.ToString()));
                    },
                _ => _shellScene.GetFocusedNode() is not null);

            _attachDemoPanelCommand = new RelayCommand(
                _ =>
                {
                    var focusedNode = _shellScene.GetFocusedNode();
                    if (focusedNode is null)
                    {
                        return;
                    }

                    var attachmentCount = _shellScene.GetPanelAttachments().Count;
                    var viewRef = $"demo.panel.{attachmentCount + 1}";

                    SendCommand(
                        CommandNames.AttachPanel,
                        new AttachPanelPayload(
                            focusedNode.Id.ToString(),
                            viewRef,
                            LocalOffset: new Vector3(0f, 0.18f, 0.15f),
                            Size: new Vector2(1.05f, 0.62f),
                            Anchor: "top",
                            IsVisible: true));
                },
                _ => _shellScene.GetFocusedNode() is not null);

            _clearSelectionCommand = new RelayCommand(
                _ =>
                {
                    SendCommand<object?>(CommandNames.ClearSelection, null);
                },
                _ => _shellScene.GetSelectedNodeIds().Count > 0 || _shellScene.GetSelectedPanels().Count > 0);

            RefreshFromEngineState();
        }

        public string FocusSummary
        {
            get
            {
                if (_shellScene.GetFocusedPanel() is { } focusedPanel)
                {
                    return $"Focused Panel: {focusedPanel.ViewRef} on {focusedPanel.NodeId}";
                }

                var focusedNode = _shellScene.GetFocusedNode();
                return focusedNode is not null
                    ? $"Focused Node: {focusedNode.Id}"
                    : "Focused Node: none";
            }
        }

        public string SelectionSummary
        {
            get
            {
                var nodeCount = _shellScene.GetSelectedNodeIds().Count;
                var panelCount = _shellScene.GetSelectedPanels().Count;

                if (nodeCount == 0 && panelCount == 0)
                {
                    return "Selection: none";
                }

                return $"Selection: nodes={nodeCount}, panels={panelCount}";
            }
        }

        public string PanelSummary
        {
            get
            {
                var count = _shellScene.GetPanelAttachments().Count;
                return count == 0
                    ? "Attached Panels: none"
                    : $"Attached Panels: {count}";
            }
        }

        public string LinkSummary
        {
            get
            {
                var count = _shellScene.GetLinks().Count;
                return count == 0
                    ? "Links: none"
                    : $"Links: {count}";
            }
        }

        public string LinkDetails
        {
            get
            {
                var links = _shellScene.GetLinks();
                if (links.Count == 0)
                {
                    return "No links yet.";
                }

                return string.Join(
                    "\n",
                    links.Select(link =>
                        $"{link.SourceId} -> {link.TargetId} kind={link.Kind} weight={link.Weight:0.##}"));
            }
        }

        public string ActionReadinessSummary
        {
            get
            {
                return string.Join(
                    " • ",
                    [
                        $"focus-node={FormatReady(_focusFirstNodeCommand.CanExecute(null))}",
                        $"select-node={FormatReady(_selectFirstNodeCommand.CanExecute(null))}",
                        $"focus-panel={FormatReady(_focusFirstPanelCommand.CanExecute(null))}",
                        $"select-panel={FormatReady(_selectFirstPanelCommand.CanExecute(null))}",
                        $"create-node={FormatReady(_createDemoNodeCommand.CanExecute(null))}",
                        $"nudge={FormatReady(_nudgeFocusedNodeCommand.CanExecute(null))}",
                        $"delete={FormatReady(_deleteFocusedNodeCommand.CanExecute(null))}",
                        $"attach-panel={FormatReady(_attachDemoPanelCommand.CanExecute(null))}",
                        $"clear={FormatReady(_clearSelectionCommand.CanExecute(null))}"
                    ]);
            }
        }

        public string LastActivitySummary => _lastActivitySummary;

        public string PanelDetails
        {
            get
            {
                var snapshot = _shellScene.GetSnapshot();
                if (snapshot.PanelAttachments is null || snapshot.PanelAttachments.Count == 0)
                {
                    return "No panel attachments yet.";
                }

                var selectedPanels = snapshot.SelectedPanels?
                    .ToHashSet()
                    ?? [];

                return string.Join(
                    "\n",
                    snapshot.PanelAttachments
                        .OrderBy(x => x.Key.ToString(), StringComparer.Ordinal)
                        .Select(x =>
                        {
                            var isFocused = snapshot.FocusedPanel is { } focusedPanel &&
                                            focusedPanel.NodeId == x.Key &&
                                            string.Equals(focusedPanel.ViewRef, x.Value.ViewRef, StringComparison.Ordinal);
                            var isSelected = selectedPanels.Contains(new PanelTarget(x.Key, x.Value.ViewRef));

                            return
                                $"{x.Key} → {x.Value.ViewRef} " +
                                $"anchor={x.Value.Anchor} " +
                                $"offset=({x.Value.LocalOffset.X:0.##},{x.Value.LocalOffset.Y:0.##},{x.Value.LocalOffset.Z:0.##}) " +
                                $"size=({x.Value.Size.X:0.##},{x.Value.Size.Y:0.##}) " +
                                $"visible={x.Value.IsVisible} " +
                                $"focused={isFocused} " +
                                $"selected={isSelected}";
                        }));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Dispose()
        {
            foreach (var subscription in _eventSubscriptions)
            {
                subscription.Dispose();
            }
        }

        private IDisposable SubscribeRefresh(string eventName, string activityLabel)
        {
            return EngineServices.EventBus.Subscribe(eventName, envelope =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateLastActivity(eventName, activityLabel, envelope);
                    RefreshFromEngineState();
                });

                return true;
            });
        }

        private void SendCommand<TPayload>(string commandName, TPayload payload)
        {
            var envelope = new Envelope
            {
                V = "1.0",
                Id = Guid.NewGuid(),
                Ts = DateTimeOffset.UtcNow,
                Type = EnvelopeType.Command,
                Name = commandName,
                Payload = payload is null
                    ? null
                    : JsonSerializer.SerializeToElement(payload, JsonOptions),
                CorrelationId = null
            };

            EngineServices.CommandBus.Send(envelope);
        }

        private void RefreshFromEngineState()
        {
            RefreshCapabilities();
            RaiseSceneStateChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void RefreshCapabilities()
        {
            var latest = EngineServices.Capabilities.GetAll().ToArray();
            Capabilities.Clear();

            foreach (var capability in latest)
            {
                Capabilities.Add(capability);
            }
        }

        private void UpdateLastActivity(string eventName, string activityLabel, Envelope envelope)
        {
            _lastActivitySummary = $"Last Activity: {activityLabel} ({eventName}) @ {envelope.Ts:HH:mm:ss}";
            OnPropertyChanged(nameof(LastActivitySummary));
        }

        private void RaiseSceneStateChanged()
        {
            OnPropertyChanged(nameof(FocusSummary));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(LinkSummary));
            OnPropertyChanged(nameof(LinkDetails));
            OnPropertyChanged(nameof(PanelSummary));
            OnPropertyChanged(nameof(ActionReadinessSummary));
            OnPropertyChanged(nameof(LastActivitySummary));
            OnPropertyChanged(nameof(PanelDetails));
        }

        private void RaiseCommandCanExecuteChanged()
        {
            _focusFirstNodeCommand.RaiseCanExecuteChanged();
            _selectFirstNodeCommand.RaiseCanExecuteChanged();
            _focusFirstPanelCommand.RaiseCanExecuteChanged();
            _selectFirstPanelCommand.RaiseCanExecuteChanged();
            _createDemoNodeCommand.RaiseCanExecuteChanged();
            _nudgeFocusedNodeCommand.RaiseCanExecuteChanged();
            _deleteFocusedNodeCommand.RaiseCanExecuteChanged();
            _attachDemoPanelCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
        }

        private static string FormatReady(bool ready) => ready ? "ready" : "blocked";

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName is not null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;
            private readonly Func<object?, bool>? _canExecute;

            public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

            public void Execute(object? parameter) => _execute(parameter);

            public void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
