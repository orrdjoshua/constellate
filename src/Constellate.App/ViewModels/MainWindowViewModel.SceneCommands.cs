using System;
using System.Linq;
using System.Numerics;
using Constellate.Core.Messaging;
using Constellate.Core.Scene;
using Constellate.SDK;

namespace Constellate.App
{
    /// <summary>
    /// Phase B extraction — command helper methods (copy-only).
    /// </summary>
    public sealed partial class MainWindowViewModel
    {
        private RelayCommand CreateSelectionOrFocusTransformCommand(Vector3 positionDelta, float scaleMultiplier)
        {
            return new RelayCommand(
                _ =>
                {
                    var targetNodes = GetSelectionOrFocusTargetNodes();
                    if (targetNodes.Length == 0)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.UpdateEntities,
                        new UpdateEntitiesPayload(
                            targetNodes
                                .Select(node =>
                                {
                                    var currentScale = node.Transform.Scale;
                                    var nextScale = new Vector3(
                                        Math.Clamp(currentScale.X * scaleMultiplier, 0.15f, 2.5f),
                                        Math.Clamp(currentScale.Y * scaleMultiplier, 0.15f, 2.5f),
                                        Math.Clamp(currentScale.Z * scaleMultiplier, 0.15f, 2.5f));
                                    var nextVisualScale = Math.Clamp(node.VisualScale * scaleMultiplier, 0.15f, 2.5f);

                                    return new UpdateEntityPayload(
                                        node.Id.ToString(),
                                        node.Label,
                                        node.Transform.Position + positionDelta,
                                        node.Transform.RotationEuler,
                                        nextScale,
                                        nextVisualScale,
                                        node.Phase);
                                })
                                .ToArray()));
                },
                _ => GetSelectionOrFocusTargetNodes().Length > 0);
        }

        private RelayCommand CreateSelectionOrFocusAppearanceCommand(string? fillColor = null, float opacityDelta = 0f, string? primitive = null)
        {
            return new RelayCommand(
                _ =>
                {
                    var targetNodes = GetSelectionOrFocusTargetNodes();
                    if (targetNodes.Length == 0)
                    {
                        return;
                    }

                    SendCommand(
                        CommandNames.UpdateEntities,
                        new UpdateEntitiesPayload(
                            targetNodes
                                .Select(node =>
                                {
                                    var currentAppearance = node.Appearance ?? NodeAppearance.Default;
                                    var nextOpacity = Math.Clamp(currentAppearance.Opacity + opacityDelta, 0.15f, 1.0f);

                                    return new UpdateEntityPayload(
                                        node.Id.ToString(),
                                        node.Label,
                                        node.Transform.Position,
                                        node.Transform.RotationEuler,
                                        node.Transform.Scale,
                                        node.VisualScale,
                                        node.Phase,
                                        new NodeAppearancePayload(
                                            Primitive: string.IsNullOrWhiteSpace(primitive) ? null : primitive,
                                            FillColor: string.IsNullOrWhiteSpace(fillColor) ? null : fillColor,
                                            Opacity: MathF.Abs(nextOpacity - currentAppearance.Opacity) > 0.0001f ? nextOpacity : null));
                                })
                                .ToArray()));
                },
                _ => GetSelectionOrFocusTargetNodes().Length > 0);
        }

        private SceneNode[] GetSelectionOrFocusTargetNodes()
        {
            var snapshot = _shellScene.GetSnapshot();
            var selectedNodeIds = snapshot.SelectedNodeIds?.ToHashSet() ?? [];

            if (selectedNodeIds.Count > 0)
            {
                return snapshot.Nodes
                    .Where(node => selectedNodeIds.Contains(node.Id))
                    .OrderBy(node => node.Label, StringComparer.Ordinal)
                    .ThenBy(node => node.Id.ToString(), StringComparer.Ordinal)
                    .ToArray();
            }

            if (snapshot.FocusedNodeId is { } focusedNodeId)
            {
                return snapshot.Nodes.Where(node => node.Id == focusedNodeId).ToArray();
            }

            return [];
        }
    }
}
