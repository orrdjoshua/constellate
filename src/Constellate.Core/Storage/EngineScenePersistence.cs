using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Constellate.Core.Scene;
using Microsoft.Data.Sqlite;

namespace Constellate.Core.Storage
{
    /// <summary>
    /// v0.1 engine.db ↔ EngineScene bridge.
    ///
    /// This first slice focuses on DB → EngineScene hydration for:
    /// - Nodes and Links (per SPEC-020 v0.1),
    /// - Groups and GroupMembers (new in this pass).
    ///
    /// Bookmarks, pane attachments, and EngineState are intentionally
    /// deferred to later CT-002 slices.
    /// </summary>
    public static class EngineScenePersistence
    {
        /// <summary>
        /// Load the current contents of engine.db into the provided EngineScene.
        ///
        /// Behavior (v0.1):
        /// - Validates arguments and opens the SQLite DB at <paramref name="databasePath"/>.
        /// - Reads all rows from Nodes, Links, Groups, and GroupMembers.
        /// - Clears existing nodes/selection/links in <paramref name="scene"/> via public APIs.
        /// - Re-creates nodes, links, and groups in the scene.
        ///
        /// Bookmarks, pane attachments, and EngineState are not yet hydrated.
        /// </summary>
        /// <param name="databasePath">Path to engine.db for the current project.</param>
        /// <param name="scene">EngineScene instance to populate.</param>
        public static void LoadFromDatabase(string databasePath, EngineScene scene)
        {
            if (scene is null)
                throw new ArgumentNullException(nameof(scene));
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path must be non-empty.", nameof(databasePath));

            var connectionString = $"Data Source={databasePath}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var nodes = LoadNodes(connection);
            var links = LoadLinks(connection);
            var groups = LoadGroups(connection, nodes);

            // Reset existing scene state (nodes + selection + links) via public APIs.
            var snapshot = scene.GetSnapshot();
            foreach (var node in snapshot.Nodes)
            {
                scene.Remove(node.Id);
            }

            scene.ClearSelection();
            scene.ClearLinks();

            // Upsert nodes from DB.
            foreach (var node in nodes)
            {
                scene.Upsert(node);
            }

            // Hydrate groups (pure scene/world structure, no side effects on selection).
            if (groups.Count > 0)
            {
                scene.SetGroups(groups);
            }

            // Re-create links via the command-style API.
            foreach (var link in links)
            {
                scene.TryConnect(link.SourceId, link.TargetId, link.Kind, link.Weight);
            }

            // Bookmarks, pane attachments, and EngineState
            // will be hydrated in later CT-002 slices.
        }

        private static List<SceneNode> LoadNodes(SqliteConnection connection)
        {
            var result = new List<SceneNode>();

            const string sql = @"
SELECT
    Id,
    Label,
    PosX,
    PosY,
    PosZ,
    RotX,
    RotY,
    RotZ,
    ScaleX,
    ScaleY,
    ScaleZ,
    VisualScale,
    Phase,
    Primitive,
    FillColor,
    OutlineColor,
    Opacity
FROM Nodes;";

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = command.ExecuteReader();
            if (!reader.HasRows)
            {
                return result;
            }

            var idOrdinal = reader.GetOrdinal("Id");
            var labelOrdinal = reader.GetOrdinal("Label");
            var posXOrdinal = reader.GetOrdinal("PosX");
            var posYOrdinal = reader.GetOrdinal("PosY");
            var posZOrdinal = reader.GetOrdinal("PosZ");
            var rotXOrdinal = reader.GetOrdinal("RotX");
            var rotYOrdinal = reader.GetOrdinal("RotY");
            var rotZOrdinal = reader.GetOrdinal("RotZ");
            var scaleXOrdinal = reader.GetOrdinal("ScaleX");
            var scaleYOrdinal = reader.GetOrdinal("ScaleY");
            var scaleZOrdinal = reader.GetOrdinal("ScaleZ");
            var visualScaleOrdinal = reader.GetOrdinal("VisualScale");
            var phaseOrdinal = reader.GetOrdinal("Phase");
            var primitiveOrdinal = reader.GetOrdinal("Primitive");
            var fillColorOrdinal = reader.GetOrdinal("FillColor");
            var outlineColorOrdinal = reader.GetOrdinal("OutlineColor");
            var opacityOrdinal = reader.GetOrdinal("Opacity");

            while (reader.Read())
            {
                var rawId = reader.IsDBNull(idOrdinal) ? null : reader.GetString(idOrdinal);
                if (!TryParseNodeId(rawId, out var nodeId))
                {
                    continue;
                }

                var label = reader.IsDBNull(labelOrdinal) ? "Node" : reader.GetString(labelOrdinal);

                var position = new Vector3(
                    x: reader.IsDBNull(posXOrdinal) ? 0f : (float)reader.GetDouble(posXOrdinal),
                    y: reader.IsDBNull(posYOrdinal) ? 0f : (float)reader.GetDouble(posYOrdinal),
                    z: reader.IsDBNull(posZOrdinal) ? 0f : (float)reader.GetDouble(posZOrdinal));

                var rotationEuler = new Vector3(
                    x: reader.IsDBNull(rotXOrdinal) ? 0f : (float)reader.GetDouble(rotXOrdinal),
                    y: reader.IsDBNull(rotYOrdinal) ? 0f : (float)reader.GetDouble(rotYOrdinal),
                    z: reader.IsDBNull(rotZOrdinal) ? 0f : (float)reader.GetDouble(rotZOrdinal));

                var scale = new Vector3(
                    x: reader.IsDBNull(scaleXOrdinal) ? 1f : (float)reader.GetDouble(scaleXOrdinal),
                    y: reader.IsDBNull(scaleYOrdinal) ? 1f : (float)reader.GetDouble(scaleYOrdinal),
                    z: reader.IsDBNull(scaleZOrdinal) ? 1f : (float)reader.GetDouble(scaleZOrdinal));

                var visualScale = reader.IsDBNull(visualScaleOrdinal)
                    ? MathF.Max(0.0001f, scale.X)
                    : (float)reader.GetDouble(visualScaleOrdinal);

                var phase = reader.IsDBNull(phaseOrdinal) ? 0f : (float)reader.GetDouble(phaseOrdinal);

                var primitive = reader.IsDBNull(primitiveOrdinal)
                    ? NodeAppearance.Default.Primitive
                    : (reader.GetString(primitiveOrdinal) ?? NodeAppearance.Default.Primitive);

                var fillColor = reader.IsDBNull(fillColorOrdinal)
                    ? NodeAppearance.Default.FillColor
                    : (reader.GetString(fillColorOrdinal) ?? NodeAppearance.Default.FillColor);

                var outlineColor = reader.IsDBNull(outlineColorOrdinal)
                    ? NodeAppearance.Default.OutlineColor
                    : (reader.GetString(outlineColorOrdinal) ?? NodeAppearance.Default.OutlineColor);

                var opacity = reader.IsDBNull(opacityOrdinal)
                    ? NodeAppearance.Default.Opacity
                    : (float)reader.GetDouble(opacityOrdinal);

                var appearance = new NodeAppearance(
                    primitive,
                    fillColor,
                    outlineColor,
                    Math.Clamp(opacity, 0.1f, 1.0f));

                var node = new SceneNode(
                    nodeId,
                    label,
                    new Transform(position, rotationEuler, scale),
                    visualScale,
                    phase,
                    appearance);

                result.Add(node);
            }

            return result;
        }

        private sealed record LinkRecord(NodeId SourceId, NodeId TargetId, string Kind, float Weight);

        private static List<LinkRecord> LoadLinks(SqliteConnection connection)
        {
            var result = new List<LinkRecord>();

            const string sql = @"
SELECT
    SourceId,
    TargetId,
    Kind,
    Weight
FROM Links;";

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = command.ExecuteReader();
            if (!reader.HasRows)
            {
                return result;
            }

            var sourceOrdinal = reader.GetOrdinal("SourceId");
            var targetOrdinal = reader.GetOrdinal("TargetId");
            var kindOrdinal = reader.GetOrdinal("Kind");
            var weightOrdinal = reader.GetOrdinal("Weight");

            while (reader.Read())
            {
                var rawSourceId = reader.IsDBNull(sourceOrdinal) ? null : reader.GetString(sourceOrdinal);
                var rawTargetId = reader.IsDBNull(targetOrdinal) ? null : reader.GetString(targetOrdinal);

                if (!TryParseNodeId(rawSourceId, out var sourceId) ||
                    !TryParseNodeId(rawTargetId, out var targetId))
                {
                    continue;
                }

                var kind = reader.IsDBNull(kindOrdinal)
                    ? "related"
                    : (reader.GetString(kindOrdinal) ?? "related");

                var weight = reader.IsDBNull(weightOrdinal)
                    ? 1.0f
                    : (float)reader.GetDouble(weightOrdinal);

                if (weight <= 0f)
                {
                    weight = 1.0f;
                }

                result.Add(new LinkRecord(sourceId, targetId, kind, weight));
            }

            return result;
        }

        private static List<SceneGroup> LoadGroups(SqliteConnection connection, IReadOnlyList<SceneNode> nodes)
        {
            var result = new List<SceneGroup>();
            if (nodes.Count == 0)
            {
                return result;
            }

            // Build a fast lookup so we only include memberships for nodes that actually exist.
            var validNodeIds = new HashSet<NodeId>(nodes.Select(n => n.Id));

            const string groupsSql = @"
SELECT
    Id,
    Label
FROM Groups;";

            const string membersSql = @"
SELECT
    GroupId,
    NodeId
FROM GroupMembers;";

            // Load all groups first.
            var groupLabels = new Dictionary<string, string>(StringComparer.Ordinal);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = groupsSql;
                using var reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    var idOrdinal = reader.GetOrdinal("Id");
                    var labelOrdinal = reader.GetOrdinal("Label");

                    while (reader.Read())
                    {
                        var groupId = reader.IsDBNull(idOrdinal) ? null : reader.GetString(idOrdinal);
                        if (string.IsNullOrWhiteSpace(groupId))
                        {
                            continue;
                        }

                        var label = reader.IsDBNull(labelOrdinal)
                            ? $"Group {groupLabels.Count + 1}"
                            : (reader.GetString(labelOrdinal) ?? $"Group {groupLabels.Count + 1}");

                        groupLabels[groupId] = label;
                    }
                }
            }

            if (groupLabels.Count == 0)
            {
                return result;
            }

            // Load all memberships in one pass.
            var memberships = new Dictionary<string, List<NodeId>>(StringComparer.Ordinal);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = membersSql;
                using var reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    var groupIdOrdinal = reader.GetOrdinal("GroupId");
                    var nodeIdOrdinal = reader.GetOrdinal("NodeId");

                    while (reader.Read())
                    {
                        var rawGroupId = reader.IsDBNull(groupIdOrdinal)
                            ? null
                            : reader.GetString(groupIdOrdinal);
                        var rawNodeId = reader.IsDBNull(nodeIdOrdinal)
                            ? null
                            : reader.GetString(nodeIdOrdinal);

                        if (string.IsNullOrWhiteSpace(rawGroupId) ||
                            !groupLabels.ContainsKey(rawGroupId) ||
                            !TryParseNodeId(rawNodeId, out var nodeId) ||
                            !validNodeIds.Contains(nodeId))
                        {
                            continue;
                        }

                        if (!memberships.TryGetValue(rawGroupId, out var list))
                        {
                            list = new List<NodeId>();
                            memberships[rawGroupId] = list;
                        }

                        list.Add(nodeId);
                    }
                }
            }

            // Construct SceneGroup records; drop groups with no valid members.
            foreach (var (groupId, label) in groupLabels)
            {
                if (!memberships.TryGetValue(groupId, out var members) || members.Count == 0)
                {
                    continue;
                }

                var orderedMembers = members
                    .Distinct()
                    .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                    .ToArray();

                if (orderedMembers.Length == 0)
                {
                    continue;
                }

                result.Add(new SceneGroup(groupId, label, orderedMembers));
            }

            return result;
        }

        private static bool TryParseNodeId(string? rawId, out NodeId nodeId)
        {
            if (!string.IsNullOrWhiteSpace(rawId) && Guid.TryParse(rawId, out var guid))
            {
                nodeId = new NodeId(guid);
                return true;
            }

            nodeId = default;
            return false;
        }
    }
}
