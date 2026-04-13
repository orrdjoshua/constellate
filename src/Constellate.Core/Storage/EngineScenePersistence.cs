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
    /// This slice focuses on:
    /// - DB → EngineScene hydration for Nodes, Links, Groups, and GroupMembers.
    /// - EngineScene → DB persistence for Nodes, Links, Groups, and GroupMembers.
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

        /// <summary>
        /// Persist the current EngineScene into engine.db.
        ///
        /// Behavior (v0.1):
        /// - Validates arguments and opens the SQLite DB at <paramref name="databasePath"/>.
        /// - Starts a transaction.
        /// - Truncates Nodes, Links, Groups, and GroupMembers.
        /// - Re-inserts nodes (including NodeAppearance inline columns),
        ///   groups and memberships, and links (with LinkAppearance defaults).
        ///
        /// Bookmarks, pane attachments, and EngineState are not yet persisted.
        /// </summary>
        /// <param name="databasePath">Path to engine.db for the current project.</param>
        /// <param name="scene">EngineScene instance whose snapshot should be saved.</param>
        public static void SaveToDatabase(string databasePath, EngineScene scene)
        {
            if (scene is null)
                throw new ArgumentNullException(nameof(scene));
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path must be non-empty.", nameof(databasePath));

            var snapshot = scene.GetSnapshot();

            var connectionString = $"Data Source={databasePath}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            // Truncate child tables first to keep constraints (if added later) happy.
            using (var truncate = connection.CreateCommand())
            {
                truncate.Transaction = transaction;
                truncate.CommandText = @"
DELETE FROM GroupMembers;
DELETE FROM Groups;
DELETE FROM Links;
DELETE FROM Nodes;";
                truncate.ExecuteNonQuery();
            }

            InsertNodes(connection, transaction, snapshot.Nodes);
            InsertGroups(connection, transaction, snapshot.Groups);
            InsertLinks(connection, transaction, snapshot.Links);

            transaction.Commit();

            // Future CT-002 slices will extend this method to persist:
            // - Bookmarks,
            // - PanelAttachments,
            // - EngineState (ActiveGroupId, InteractionMode, EnteredNodeId, last view),
            // using the SPEC-020 tables.
        }

        private static void InsertNodes(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<SceneNode> nodes)
        {
            if (nodes is null || nodes.Count == 0)
            {
                return;
            }

            const string sql = @"
INSERT INTO Nodes (
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
) VALUES (
    $id,
    $label,
    $posX,
    $posY,
    $posZ,
    $rotX,
    $rotY,
    $rotZ,
    $scaleX,
    $scaleY,
    $scaleZ,
    $visualScale,
    $phase,
    $primitive,
    $fillColor,
    $outlineColor,
    $opacity
);";

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            var idParam = command.CreateParameter();
            idParam.ParameterName = "$id";
            command.Parameters.Add(idParam);

            var labelParam = command.CreateParameter();
            labelParam.ParameterName = "$label";
            command.Parameters.Add(labelParam);

            var posXParam = command.CreateParameter();
            posXParam.ParameterName = "$posX";
            command.Parameters.Add(posXParam);

            var posYParam = command.CreateParameter();
            posYParam.ParameterName = "$posY";
            command.Parameters.Add(posYParam);

            var posZParam = command.CreateParameter();
            posZParam.ParameterName = "$posZ";
            command.Parameters.Add(posZParam);

            var rotXParam = command.CreateParameter();
            rotXParam.ParameterName = "$rotX";
            command.Parameters.Add(rotXParam);

            var rotYParam = command.CreateParameter();
            rotYParam.ParameterName = "$rotY";
            command.Parameters.Add(rotYParam);

            var rotZParam = command.CreateParameter();
            rotZParam.ParameterName = "$rotZ";
            command.Parameters.Add(rotZParam);

            var scaleXParam = command.CreateParameter();
            scaleXParam.ParameterName = "$scaleX";
            command.Parameters.Add(scaleXParam);

            var scaleYParam = command.CreateParameter();
            scaleYParam.ParameterName = "$scaleY";
            command.Parameters.Add(scaleYParam);

            var scaleZParam = command.CreateParameter();
            scaleZParam.ParameterName = "$scaleZ";
            command.Parameters.Add(scaleZParam);

            var visualScaleParam = command.CreateParameter();
            visualScaleParam.ParameterName = "$visualScale";
            command.Parameters.Add(visualScaleParam);

            var phaseParam = command.CreateParameter();
            phaseParam.ParameterName = "$phase";
            command.Parameters.Add(phaseParam);

            var primitiveParam = command.CreateParameter();
            primitiveParam.ParameterName = "$primitive";
            command.Parameters.Add(primitiveParam);

            var fillColorParam = command.CreateParameter();
            fillColorParam.ParameterName = "$fillColor";
            command.Parameters.Add(fillColorParam);

            var outlineColorParam = command.CreateParameter();
            outlineColorParam.ParameterName = "$outlineColor";
            command.Parameters.Add(outlineColorParam);

            var opacityParam = command.CreateParameter();
            opacityParam.ParameterName = "$opacity";
            command.Parameters.Add(opacityParam);

            foreach (var node in nodes)
            {
                idParam.Value = node.Id.ToString();
                labelParam.Value = node.Label ?? "Node";

                posXParam.Value = node.Transform.Position.X;
                posYParam.Value = node.Transform.Position.Y;
                posZParam.Value = node.Transform.Position.Z;

                rotXParam.Value = node.Transform.RotationEuler.X;
                rotYParam.Value = node.Transform.RotationEuler.Y;
                rotZParam.Value = node.Transform.RotationEuler.Z;

                scaleXParam.Value = node.Transform.Scale.X;
                scaleYParam.Value = node.Transform.Scale.Y;
                scaleZParam.Value = node.Transform.Scale.Z;

                visualScaleParam.Value = node.VisualScale;
                phaseParam.Value = node.Phase;

                primitiveParam.Value = string.IsNullOrWhiteSpace(node.Appearance.Primitive)
                    ? NodeAppearance.Default.Primitive
                    : node.Appearance.Primitive;

                fillColorParam.Value = string.IsNullOrWhiteSpace(node.Appearance.FillColor)
                    ? NodeAppearance.Default.FillColor
                    : node.Appearance.FillColor;

                outlineColorParam.Value = string.IsNullOrWhiteSpace(node.Appearance.OutlineColor)
                    ? NodeAppearance.Default.OutlineColor
                    : node.Appearance.OutlineColor;

                opacityParam.Value = Math.Clamp(node.Appearance.Opacity, 0.1f, 1.0f);

                command.ExecuteNonQuery();
            }
        }

        private static void InsertGroups(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<SceneGroup>? groups)
        {
            if (groups is null || groups.Count == 0)
            {
                return;
            }

            const string insertGroupSql = @"
INSERT INTO Groups (Id, Label)
VALUES ($id, $label);";

            const string insertMemberSql = @"
INSERT INTO GroupMembers (GroupId, NodeId)
VALUES ($groupId, $nodeId);";

            using var groupCommand = connection.CreateCommand();
            groupCommand.Transaction = transaction;
            groupCommand.CommandText = insertGroupSql;

            var groupIdParam = groupCommand.CreateParameter();
            groupIdParam.ParameterName = "$id";
            groupCommand.Parameters.Add(groupIdParam);

            var groupLabelParam = groupCommand.CreateParameter();
            groupLabelParam.ParameterName = "$label";
            groupCommand.Parameters.Add(groupLabelParam);

            using var memberCommand = connection.CreateCommand();
            memberCommand.Transaction = transaction;
            memberCommand.CommandText = insertMemberSql;

            var memberGroupIdParam = memberCommand.CreateParameter();
            memberGroupIdParam.ParameterName = "$groupId";
            memberCommand.Parameters.Add(memberGroupIdParam);

            var memberNodeIdParam = memberCommand.CreateParameter();
            memberNodeIdParam.ParameterName = "$nodeId";
            memberCommand.Parameters.Add(memberNodeIdParam);

            foreach (var group in groups)
            {
                groupIdParam.Value = group.Id;
                groupLabelParam.Value = group.Label ?? $"Group {group.Id}";
                groupCommand.ExecuteNonQuery();

                if (group.NodeIds is null || group.NodeIds.Count == 0)
                {
                    continue;
                }

                foreach (var nodeId in group.NodeIds.Distinct())
                {
                    memberGroupIdParam.Value = group.Id;
                    memberNodeIdParam.Value = nodeId.ToString();
                    memberCommand.ExecuteNonQuery();
                }
            }
        }

        private static void InsertLinks(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<SceneLink>? links)
        {
            if (links is null || links.Count == 0)
            {
                return;
            }

            const string sql = @"
INSERT INTO Links (
    Id,
    SourceId,
    TargetId,
    Kind,
    Weight,
    StrokeColor,
    StrokeThickness,
    Opacity
) VALUES (
    $id,
    $sourceId,
    $targetId,
    $kind,
    $weight,
    $strokeColor,
    $strokeThickness,
    $opacity
);";

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            var idParam = command.CreateParameter();
            idParam.ParameterName = "$id";
            command.Parameters.Add(idParam);

            var sourceParam = command.CreateParameter();
            sourceParam.ParameterName = "$sourceId";
            command.Parameters.Add(sourceParam);

            var targetParam = command.CreateParameter();
            targetParam.ParameterName = "$targetId";
            command.Parameters.Add(targetParam);

            var kindParam = command.CreateParameter();
            kindParam.ParameterName = "$kind";
            command.Parameters.Add(kindParam);

            var weightParam = command.CreateParameter();
            weightParam.ParameterName = "$weight";
            command.Parameters.Add(weightParam);

            var strokeColorParam = command.CreateParameter();
            strokeColorParam.ParameterName = "$strokeColor";
            command.Parameters.Add(strokeColorParam);

            var strokeThicknessParam = command.CreateParameter();
            strokeThicknessParam.ParameterName = "$strokeThickness";
            command.Parameters.Add(strokeThicknessParam);

            var opacityParam = command.CreateParameter();
            opacityParam.ParameterName = "$opacity";
            command.Parameters.Add(opacityParam);

            foreach (var link in links)
            {
                idParam.Value = link.Id;
                sourceParam.Value = link.SourceId.ToString();
                targetParam.Value = link.TargetId.ToString();

                kindParam.Value = string.IsNullOrWhiteSpace(link.Kind)
                    ? "related"
                    : link.Kind;

                var weight = link.Weight <= 0f ? 1.0f : link.Weight;
                weightParam.Value = weight;

                // v0.1: no per-link appearance on SceneLink; use LinkAppearance.Default.
                strokeColorParam.Value = LinkAppearance.Default.StrokeColor;
                strokeThicknessParam.Value = LinkAppearance.Default.StrokeThickness;
                opacityParam.Value = Math.Clamp(LinkAppearance.Default.Opacity, 0.1f, 1.0f);

                command.ExecuteNonQuery();
            }
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
