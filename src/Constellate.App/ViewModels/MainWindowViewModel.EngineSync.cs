using System;
using System.Linq;
using System.Text.Json;
using Avalonia.Threading;
using Constellate.Core.Messaging;
using Constellate.Core.Resources;
using Constellate.SDK;
using Constellate.Core.Scene;

namespace Constellate.App
{
    /// <summary>
    /// Phase B extraction — EngineSync and messaging helpers.
    /// Copy-only; originals remain in MainWindow.axaml.cs until final overwrite.
    /// </summary>
    public sealed partial class MainWindowViewModel : IDisposable
    {
        private IDisposable SubscribeRefresh(string eventName, string activityLabel)
        {
            return EngineServices.EventBus.Subscribe(eventName, envelope =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateLastActivity(eventName, activityLabel, envelope);
                    TrySyncActiveResourceDetailSurfaceFromPersistence();
                    RefreshFromEngineState();
                });

                return true;
            });
        }

        private IDisposable SubscribePanelInteraction()
        {
            return EngineServices.EventBus.Subscribe(EventNames.PanelInteraction, envelope =>
            {
                if (envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!TryGetString(payload, "action", out var action) ||
                    !string.Equals(action, "promote_to_pane", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    var minimizedParent = ParentPaneModels.FirstOrDefault(parent => parent.IsMinimized);
                    if (minimizedParent is null)
                    {
                        return;
                    }

                    minimizedParent.IsMinimized = false;
                    RaiseParentPaneLayoutChanged(includeChildRefresh: true);
                });

                return true;
            });
        }

        private IDisposable SubscribeResourceSurfaceBinding()
        {
            return EngineServices.EventBus.Subscribe(EventNames.ResourceSurfaceBindingChanged, envelope =>
            {
                if (!TryDeserialize(envelope.Payload, out ResourceSurfaceBindingChangedPayload? payload) || payload is null)
                {
                    return false;
                }

                var resolvedPayload = payload;

                Dispatcher.UIThread.Post(() =>
                {
                    UpdateLastActivity(EventNames.ResourceSurfaceBindingChanged, "resource surface binding changed", envelope);
                    TryTrackResourceSurfaceBinding(resolvedPayload);
                    TrySyncActiveResourceDetailSurfaceFromPersistence();
                    RefreshFromEngineState();
                });

                return true;
            });
        }

        private void TryTrackResourceSurfaceBinding(ResourceSurfaceBindingChangedPayload payload)
        {
            if (!payload.IsActive ||
                !string.Equals(payload.Binding.ProjectionMode, ResourceSurfaceBindingPayload.ProjectionModeDetail, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(payload.Binding.TargetSurfaceKind, ResourceSurfaceBindingPayload.TargetSurfaceKindChildPaneBody, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SetActiveResourceDetailSurface(payload.Binding.SurfaceRole, payload.Binding.ViewRef, payload.ResourceDisplayLabel, payload.ResourceTitle);
            UpsertResourceBoundDetailChildPane(payload.Binding.SurfaceRole, payload.Binding.ViewRef, payload.ResourceDisplayLabel, payload.ResourceTitle);
        }

        private void TrySyncActiveResourceDetailSurfaceFromPersistence()
        {
            var detailBindings = EngineServices
                .ListResolvedResourceSurfaceBindings(
                    projectionMode: ResourceSurfaceBindingPayload.ProjectionModeDetail,
                    targetSurfaceKind: ResourceSurfaceBindingPayload.TargetSurfaceKindChildPaneBody,
                    activeOnly: true)
                .ToArray();

            var expectedPaneIds = detailBindings
                .Select(detailBinding => BuildResourceBoundChildPaneId(detailBinding.SurfaceRole, detailBinding.ViewRef))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            ReconcileResourceBoundDetailChildPanes(expectedPaneIds);

            if (detailBindings.Length == 0)
            {
                SetActiveResourceDetailSurface(null, null, null, null);
                return;
            }

            foreach (var detailBinding in detailBindings)
            {
                UpsertResourceBoundDetailChildPane(
                    detailBinding.SurfaceRole,
                    detailBinding.ViewRef,
                    detailBinding.ResourceDisplayLabel,
                    detailBinding.ResourceTitle);
            }

            var activeBindingKey = ActiveResourceDetailSurfaceBindingKey;
            var activeDetailBinding = detailBindings.FirstOrDefault(detailBinding =>
                    !string.IsNullOrWhiteSpace(activeBindingKey) &&
                    string.Equals(detailBinding.BindingKey, activeBindingKey, StringComparison.Ordinal))
                ?? detailBindings[0];

            SetActiveResourceDetailSurface(
                activeDetailBinding.SurfaceRole,
                activeDetailBinding.ViewRef,
                activeDetailBinding.ResourceDisplayLabel,
                activeDetailBinding.ResourceTitle);
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

        private static void PublishFocusOrigin(string origin)
        {
            try
            {
                var envelope = new Envelope
                {
                    V = "1.0",
                    Id = Guid.NewGuid(),
                    Ts = DateTimeOffset.UtcNow,
                    Type = EnvelopeType.Event,
                    Name = EventNames.FocusOriginChanged,
                    Payload = JsonSerializer.SerializeToElement(new { origin }, JsonOptions),
                    CorrelationId = null
                };

                EngineServices.EventBus.Publish(envelope);
            }
            catch
            {
            }
        }

        private void UpdateLastActivity(string eventName, string activityLabel, Envelope envelope)
        {
            AddHistoryEntry(eventName, envelope);
            TryTrackCompatibilityDetailSurfaceFallback(eventName, envelope);
            var detail = TryDescribeActivity(envelope);
            _lastActivitySummary = string.IsNullOrWhiteSpace(detail)
                ? $"Last Activity: {activityLabel} ({eventName}) @ {envelope.Ts:HH:mm:ss}"
                : $"Last Activity: {activityLabel} ({eventName}: {detail}) @ {envelope.Ts:HH:mm:ss}";
            OnPropertyChanged(nameof(LastActivitySummary));
        }

        private void TryTrackCompatibilityDetailSurfaceFallback(string eventName, Envelope envelope)
        {
            if (string.Equals(eventName, EventNames.ResourceSurfaceBindingChanged, StringComparison.Ordinal) ||
                envelope.Payload is not JsonElement payload ||
                payload.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!TryGetString(payload, "viewRef", out var viewRef))
            {
                return;
            }

            if (TryResolveExplicitDetailSurfaceRole(payload, out var explicitSurfaceRole))
            {
                TrackActiveResourceDetailSurfaceFromPayload(explicitSurfaceRole, viewRef, payload);
                return;
            }

            if (!TryInferLegacyMarkdownDetailSurfaceRole(viewRef, out var legacySurfaceRole))
            {
                return;
            }

            TrackActiveResourceDetailSurfaceFromPayload(legacySurfaceRole, viewRef, payload);
        }

        private void TrackActiveResourceDetailSurfaceFromPayload(string surfaceRole, string viewRef, JsonElement payload)
        {
            var hasResourceDisplayLabel = TryGetString(payload, "resourceDisplayLabel", out var resourceDisplayLabel);
            var hasResourceTitle = TryGetString(payload, "resourceTitle", out var resourceTitle);

            var resolvedResourceDisplayLabel = hasResourceDisplayLabel ? resourceDisplayLabel : null;
            var resolvedResourceTitle = hasResourceTitle ? resourceTitle : null;

            SetActiveResourceDetailSurface(
                surfaceRole,
                viewRef,
                resolvedResourceDisplayLabel,
                resolvedResourceTitle);
            UpsertResourceBoundDetailChildPane(
                surfaceRole,
                viewRef,
                resolvedResourceDisplayLabel,
                resolvedResourceTitle);
        }

        private static bool TryResolveExplicitDetailSurfaceRole(JsonElement payload, out string surfaceRole)
        {
            surfaceRole = string.Empty;

            if (!TryGetString(payload, "surfaceRole", out var explicitSurfaceRole) ||
                !IsDetailSurfaceRole(explicitSurfaceRole))
            {
                return false;
            }

            surfaceRole = explicitSurfaceRole;
            return true;
        }

        private static bool TryInferLegacyMarkdownDetailSurfaceRole(string viewRef, out string surfaceRole)
        {
            surfaceRole = string.Empty;

            if (!viewRef.StartsWith($"{MarkdownRecordResourceDescriptor.DefaultDetailViewRefPrefix}:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            surfaceRole = MarkdownRecordResourceDescriptor.DefaultDetailSurfaceRole;
            return true;
        }

        private static bool IsDetailSurfaceRole(string surfaceRole)
        {
            return string.Equals(surfaceRole, "detail", StringComparison.OrdinalIgnoreCase) ||
                   surfaceRole.Contains("detail", StringComparison.OrdinalIgnoreCase);
        }

        private void AddHistoryEntry(string eventName, Envelope envelope)
        {
            if (!string.Equals(eventName, EventNames.CommandInvoked, StringComparison.Ordinal))
            {
                return;
            }

            if (envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!TryGetString(payload, "commandName", out var commandName))
            {
                return;
            }

            var domain = ClassifyCommandDomain(commandName);
            var label = $"{domain}: {commandName}";

            const int maxHistoryEntries = 10;
            if (_commandHistory.Count >= maxHistoryEntries)
            {
                _commandHistory.Dequeue();
            }

            _commandHistory.Enqueue(label);
            OnPropertyChanged(nameof(CommandHistorySummary));
        }

        private static string ClassifyCommandDomain(string commandName)
        {
            if (commandName is
                CommandNames.CreateEntity or
                CommandNames.UpdateEntity or
                CommandNames.UpdateEntities or
                CommandNames.Delete or
                CommandNames.DeleteEntities or
                CommandNames.SetTransform or
                CommandNames.Connect or
                CommandNames.Unlink or
                CommandNames.ClearLinks or
                CommandNames.GroupSelection or
                CommandNames.AddSelectionToGroup or
                CommandNames.RemoveSelectionFromGroup or
                CommandNames.DeleteGroup or
                CommandNames.AttachPanel or
                CommandNames.ClearPanelAttachment or
                CommandNames.Focus or
                CommandNames.FocusPanel or
                CommandNames.Select or
                CommandNames.SelectPanel or
                CommandNames.ClearSelection)
            {
                return "world";
            }

            if (commandName is
                CommandNames.HomeView or
                CommandNames.CenterOnNode or
                CommandNames.FrameSelection or
                CommandNames.BookmarkSave or
                CommandNames.BookmarkRestore or
                CommandNames.SetInteractionMode)
            {
                return "navigation";
            }

            if (commandName is
                CommandNames.SemanticsIndex or
                CommandNames.SemanticsQuerySimilar or
                CommandNames.SemanticsExplain)
            {
                return "semantics";
            }

            return "other";
        }

        private static string? TryDescribeActivity(Envelope envelope)
        {
            if (envelope.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
            {
                return envelope.Name;
            }

            if (string.Equals(envelope.Name, EventNames.CommandInvoked, StringComparison.Ordinal))
            {
                if (TryGetString(payload, "commandName", out var commandName))
                {
                    return commandName;
                }

                return envelope.Name;
            }

            if (TryGetString(payload, "reason", out var reason))
            {
                return reason;
            }

            if (TryGetString(payload, "label", out var label))
            {
                return label;
            }

            if (TryGetString(payload, "bookmarkName", out var bookmarkName))
            {
                return bookmarkName;
            }

            if (TryGetString(payload, "focusedNodeId", out var focusedNodeId))
            {
                return focusedNodeId;
            }

            if (TryGetString(payload, "viewRef", out var viewRef))
            {
                return viewRef;
            }

            if (TryGetString(payload, "mode", out var mode))
            {
                return mode;
            }

            return envelope.Name;
        }

        private static bool TryDeserialize<T>(JsonElement? payload, out T? value)
        {
            value = default;
            if (payload is null)
            {
                return false;
            }

            try
            {
                value = payload.Value.Deserialize<T>(JsonOptions);
                return value is not null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;

            if (!element.TryGetProperty(propertyName, out var propertyValue))
            {
                return false;
            }

            if (propertyValue.ValueKind == JsonValueKind.String)
            {
                var text = propertyValue.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    value = text;
                    return true;
                }
            }

            return false;
        }

        private void RaiseSceneStateChanged()
        {
            OnPropertyChanged(nameof(FocusSummary));
            OnPropertyChanged(nameof(FocusOriginSummary));
            OnPropertyChanged(nameof(EnteredNodeSummary));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(BookmarkSummary));
            OnPropertyChanged(nameof(ViewSummary));
            OnPropertyChanged(nameof(ViewDetails));
            OnPropertyChanged(nameof(FocusedTransformSummary));
            OnPropertyChanged(nameof(FocusedTransformDetails));
            OnPropertyChanged(nameof(InteractionModeSummary));
            OnPropertyChanged(nameof(BookmarkDetails));
            OnPropertyChanged(nameof(GroupSummary));
            OnPropertyChanged(nameof(GroupDetails));
            OnPropertyChanged(nameof(LinkSummary));
            OnPropertyChanged(nameof(InteractionSemanticsSummary));
            OnPropertyChanged(nameof(LinkDetails));
            OnPropertyChanged(nameof(PanelSummary));
            OnPropertyChanged(nameof(ActionReadinessSummary));
            OnPropertyChanged(nameof(LastActivitySummary));
            OnPropertyChanged(nameof(NavigationHistorySummary));
            OnPropertyChanged(nameof(PanelDetails));
        }

        private void RaiseCommandCanExecuteChanged()
        {
            _focusFirstNodeCommand.RaiseCanExecuteChanged();
            _activateNavigateModeCommand.RaiseCanExecuteChanged();
            _activateMoveModeCommand.RaiseCanExecuteChanged();
            _activateMarqueeModeCommand.RaiseCanExecuteChanged();
            _selectFirstNodeCommand.RaiseCanExecuteChanged();
            _focusFirstPanelCommand.RaiseCanExecuteChanged();
            _selectFirstPanelCommand.RaiseCanExecuteChanged();
            _createDemoNodeCommand.RaiseCanExecuteChanged();
            _nudgeFocusedLeftCommand.RaiseCanExecuteChanged();
            _nudgeFocusedRightCommand.RaiseCanExecuteChanged();
            _nudgeFocusedUpCommand.RaiseCanExecuteChanged();
            _nudgeFocusedDownCommand.RaiseCanExecuteChanged();
            _nudgeFocusedForwardCommand.RaiseCanExecuteChanged();
            _nudgeFocusedBackCommand.RaiseCanExecuteChanged();
            _growFocusedNodeCommand.RaiseCanExecuteChanged();
            _shrinkFocusedNodeCommand.RaiseCanExecuteChanged();
            _applyTrianglePrimitiveCommand.RaiseCanExecuteChanged();
            _applySquarePrimitiveCommand.RaiseCanExecuteChanged();
            _applyDiamondPrimitiveCommand.RaiseCanExecuteChanged();
            _applyPentagonPrimitiveCommand.RaiseCanExecuteChanged();
            _applyHexagonPrimitiveCommand.RaiseCanExecuteChanged();
            _applyCubePrimitiveCommand.RaiseCanExecuteChanged();
            _applyTetrahedronPrimitiveCommand.RaiseCanExecuteChanged();
            _applySpherePrimitiveCommand.RaiseCanExecuteChanged();
            _applyBoxPrimitiveCommand.RaiseCanExecuteChanged();
            _applyBlueAppearanceCommand.RaiseCanExecuteChanged();
            _applyVioletAppearanceCommand.RaiseCanExecuteChanged();
            _applyGreenAppearanceCommand.RaiseCanExecuteChanged();
            _increaseOpacityCommand.RaiseCanExecuteChanged();
            _decreaseOpacityCommand.RaiseCanExecuteChanged();
            _connectFocusedNodeCommand.RaiseCanExecuteChanged();
            _unlinkFocusedNodeCommand.RaiseCanExecuteChanged();
            _groupSelectionCommand.RaiseCanExecuteChanged();
            _addSelectionToActiveGroupCommand.RaiseCanExecuteChanged();
            _removeSelectionFromActiveGroupCommand.RaiseCanExecuteChanged();
            _deleteActiveGroupCommand.RaiseCanExecuteChanged();
            _saveBookmarkCommand.RaiseCanExecuteChanged();
            _restoreLatestBookmarkCommand.RaiseCanExecuteChanged();
            _undoLastCommand.RaiseCanExecuteChanged();
            _homeViewCommand.RaiseCanExecuteChanged();
            _centerFocusedNodeCommand.RaiseCanExecuteChanged();
            _frameSelectionCommand.RaiseCanExecuteChanged();
            _deleteFocusedNodeCommand.RaiseCanExecuteChanged();
            _attachDemoPanelCommand.RaiseCanExecuteChanged();
            _attachLabelPaneletteCommand.RaiseCanExecuteChanged();
            _attachDetailMetadataPaneletteCommand.RaiseCanExecuteChanged();
            _clearLinksCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
            _minimizeShellPaneCommand.RaiseCanExecuteChanged();
            _restoreShellPaneCommand.RaiseCanExecuteChanged();
            _moveChildPaneUpCommand.RaiseCanExecuteChanged();
            _moveChildPaneDownCommand.RaiseCanExecuteChanged();
            _createOrRestoreParentPaneCommand.RaiseCanExecuteChanged();
            _destroyParentPaneCommand.RaiseCanExecuteChanged();
            _setLeftPaneSplitCommand.RaiseCanExecuteChanged();
            _setTopPaneSplitCommand.RaiseCanExecuteChanged();
            _setRightPaneSplitCommand.RaiseCanExecuteChanged();
            _setBottomPaneSplitCommand.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            foreach (var subscription in _eventSubscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
