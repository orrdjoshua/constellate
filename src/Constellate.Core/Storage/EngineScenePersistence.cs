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
    /// - DB → EngineScene hydration for Nodes, Links, Groups, GroupMembers, and Bookmarks.
    /// - EngineScene → DB persistence for Nodes, Links, Groups, GroupMembers, and Bookmarks.
    ///
    /// Pane attachments and EngineState are intentionally deferred to later CT-002 slices.
    /// </summary>
    public static class EngineScenePersistence
    {
        /// <summary>
        /// Load the current contents of engine.db into the provided EngineScene.
        ///
        /// Behavior (v0.1):
        /// - Validates arguments and opens the SQLite DB at <paramref name="databasePath"/>.
        /// - Reads all rows from Nodes, Links, Groups, GroupMembers, and Bookmarks tables.
        /// - Clears existing nodes/selection/links in <paramref name="scene"/> via public APIs.
        /// - Re-creates nodes, links, groups, and bookmarks in the scene.
        ///
        /// Pane attachments and EngineState are not yet hydrated.
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
            var bookmarks = LoadBookmarks(connection, nodes);

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

            // Hydrate bookmarks directly into the scene snapshot surface.
            if (bookmarks.Count > 0)
            {
                scene.SetBookmarks(bookmarks);
            }

            // Pane attachments and EngineState will be hydrated in later CT-002 slices.
        }

        /// <summary>
        /// Persist the current EngineScene into engine.db.
        ///
        /// Behavior (v0.1):
        /// - Validates arguments and opens the SQLite DB at <paramref name="databasePath"/>.
        /// - Starts a transaction.
        /// - Truncates Nodes, Links, Groups, GroupMembers, Bookmarks, BookmarkSelectedNodes, BookmarkSelectedPanels.
        /// - Re-inserts nodes (including NodeAppearance inline columns),
        ///   groups and memberships,
        ///   links (with LinkAppearance defaults),
        ///   and bookmarks (including focused/selected nodes/panels + view).
        ///
        /// Pane attachments and EngineState are not yet persisted.
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
DELETE FROM BookmarkSelectedPanels;
DELETE FROM BookmarkSelectedNodes;
DELETE FROM Bookmarks;
DELETE FROM GroupMembers;
DELETE FROM Groups;
DELETE FROM Links;
DELETE FROM Nodes;";
                truncate.ExecuteNonQuery();
            }

            InsertNodes(connection, transaction, snapshot.Nodes);
            InsertGroups(connection, transaction, snapshot.Groups);
            InsertLinks(connection, transaction, snapshot.Links);
            InsertBookmarks(connection, transaction, snapshot.Bookmarks);

            transaction.Commit();

            // Future CT-002 slices will extend this method to persist:
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

        private static void InsertBookmarks(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<SceneBookmark>? bookmarks)
        {
            if (bookmarks is null || bookmarks.Count == 0)
            {
                return;
            }

            const string insertBookmarkSql = @"
INSERT INTO Bookmarks (
    Name,
    FocusedNodeId,
    FocusedPanelNodeId,
    FocusedPanelViewRef,
    ViewYaw,
    ViewPitch,
    ViewDistance,
    ViewTargetX,
    ViewTargetY,
    ViewTargetZ
) VALUES (
    $name,
    $focusedNodeId,
    $focusedPanelNodeId,
    $focusedPanelViewRef,
    $viewYaw,
    $viewPitch,
    $viewDistance,
    $viewTargetX,
    $viewTargetY,
    $viewTargetZ
);";

            const string insertSelectedNodeSql = @"
INSERT INTO BookmarkSelectedNodes (
    BookmarkName,
    NodeId
) VALUES (
    $bookmarkName,
    $nodeId
);";

            const string insertSelectedPanelSql = @"
INSERT INTO BookmarkSelectedPanels (
    BookmarkName,
    NodeId,
    ViewRef
) VALUES (
    $bookmarkName,
    $nodeId,
    $viewRef
);";

            using var bookmarkCommand = connection.CreateCommand();
            bookmarkCommand.Transaction = transaction;
            bookmarkCommand.CommandText = insertBookmarkSql;

            var nameParam = bookmarkCommand.CreateParameter();
            nameParam.ParameterName = "$name";
            bookmarkCommand.Parameters.Add(nameParam);

            var focusedNodeIdParam = bookmarkCommand.CreateParameter();
            focusedNodeIdParam.ParameterName = "$focusedNodeId";
            bookmarkCommand.Parameters.Add(focusedNodeIdParam);

            var focusedPanelNodeIdParam = bookmarkCommand.CreateParameter();
            focusedPanelNodeIdParam.ParameterName = "$focusedPanelNodeId";
            bookmarkCommand.Parameters.Add(focusedPanelNodeIdParam);

            var focusedPanelViewRefParam = bookmarkCommand.CreateParameter();
            focusedPanelViewRefParam.ParameterName = "$focusedPanelViewRef";
            bookmarkCommand.Parameters.Add(focusedPanelViewRefParam);

            var viewYawParam = bookmarkCommand.CreateParameter();
            viewYawParam.ParameterName = "$viewYaw";
            bookmarkCommand.Parameters.Add(viewYawParam);

            var viewPitchParam = bookmarkCommand.CreateParameter();
            viewPitchParam.ParameterName = "$viewPitch";
            bookmarkCommand.Parameters.Add(viewPitchParam);

            var viewDistanceParam = bookmarkCommand.CreateParameter();
            viewDistanceParam.ParameterName = "$viewDistance";
            bookmarkCommand.Parameters.Add(viewDistanceParam);

            var viewTargetXParam = bookmarkCommand.CreateParameter();
            viewTargetXParam.ParameterName = "$viewTargetX";
            bookmarkCommand.Parameters.Add(viewTargetXParam);

            var viewTargetYParam = bookmarkCommand.CreateParameter();
            viewTargetYParam.ParameterName = "$viewTargetY";
            bookmarkCommand.Parameters.Add(viewTargetYParam);

            var viewTargetZParam = bookmarkCommand.CreateParameter();
            viewTargetZParam.ParameterName = "$viewTargetZ";
            bookmarkCommand.Parameters.Add(viewTargetZParam);

            using var selectedNodeCommand = connection.CreateCommand();
            selectedNodeCommand.Transaction = transaction;
            selectedNodeCommand.CommandText = insertSelectedNodeSql;

            var snBookmarkNameParam = selectedNodeCommand.CreateParameter();
            snBookmarkNameParam.ParameterName = "$bookmarkName";
            selectedNodeCommand.Parameters.Add(snBookmarkNameParam);

            var snNodeIdParam = selectedNodeCommand.CreateParameter();
            snNodeIdParam.ParameterName = "$nodeId";
            selectedNodeCommand.Parameters.Add(snNodeIdParam);

            using var selectedPanelCommand = connection.CreateCommand();
            selectedPanelCommand.Transaction = transaction;
            selectedPanelCommand.CommandText = insertSelectedPanelSql;

            var spBookmarkNameParam = selectedPanelCommand.CreateParameter();
            spBookmarkNameParam.ParameterName = "$bookmarkName";
            selectedPanelCommand.Parameters.Add(spBookmarkNameParam);

            var spNodeIdParam = selectedPanelCommand.CreateParameter();
            spNodeIdParam.ParameterName = "$nodeId";
            selectedPanelCommand.Parameters.Add(spNodeIdParam);

            var spViewRefParam = selectedPanelCommand.CreateParameter();
            spViewRefParam.ParameterName = "$viewRef";
            selectedPanelCommand.Parameters.Add(spViewRefParam);

            foreach (var bookmark in bookmarks)
            {
                nameParam.Value = bookmark.Name;

                focusedNodeIdParam.Value = bookmark.FocusedNodeId is null
                    ? (object?)DBNull.Value
                    : bookmark.FocusedNodeId.Value.ToString();

                if (bookmark.FocusedPanel is { } focusedPanel)
                {
                    focusedPanelNodeIdParam.Value = focusedPanel.NodeId.ToString();
                    focusedPanelViewRefParam.Value = focusedPanel.ViewRef ?? (object?)DBNull.Value ?? DBNull.Value;
                }
                else
                {
                    focusedPanelNodeIdParam.Value = DBNull.Value;
                    focusedPanelViewRefParam.Value = DBNull.Value;
                }

                if (bookmark.View is { } view)
                {
                    viewYawParam.Value = view.Yaw;
                    viewPitchParam.Value = view.Pitch;
                    viewDistanceParam.Value = view.Distance;
                    viewTargetXParam.Value = view.Target.X;
                    viewTargetYParam.Value = view.Target.Y;
                    viewTargetZParam.Value = view.Target.Z;
                }
                else
                {
                    viewYawParam.Value = DBNull.Value;
                    viewPitchParam.Value = DBNull.Value;
                    viewDistanceParam.Value = DBNull.Value;
                    viewTargetXParam.Value = DBNull.Value;
                    viewTargetYParam.Value = DBNull.Value;
                    viewTargetZParam.Value = DBNull.Value;
                }

                bookmarkCommand.ExecuteNonQuery();

                if (bookmark.SelectedNodeIds is { Count: > 0 })
                {
                    foreach (var nodeId in bookmark.SelectedNodeIds.Distinct())
                    {
                        snBookmarkNameParam.Value = bookmark.Name;
                        snNodeIdParam.Value = nodeId.ToString();
                        selectedNodeCommand.ExecuteNonQuery();
                    }
                }

                if (bookmark.SelectedPanels is { Count: > 0 })
                {
                    foreach (var panel in bookmark.SelectedPanels.Distinct())
                    {
                        spBookmarkNameParam.Value = bookmark.Name;
                        spNodeIdParam.Value = panel.NodeId.ToString();
                        spViewRefParam.Value = panel.ViewRef ?? (object?)DBNull.Value ?? DBNull.Value;
                        selectedPanelCommand.ExecuteNonQuery();
                    }
                }
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

        private static List<SceneBookmark> LoadBookmarks(SqliteConnection connection, IReadOnlyList<SceneNode> nodes)
        {
            var result = new List<SceneBookmark>();
            if (nodes.Count == 0)
            {
                return result;
            }

            var validNodeIds = new HashSet<NodeId>(nodes.Select(n => n.Id));

            const string bookmarksSql = @"
SELECT
    Name,
    FocusedNodeId,
    FocusedPanelNodeId,
    FocusedPanelViewRef,
    ViewYaw,
    ViewPitch,
    ViewDistance,
    ViewTargetX,
    ViewTargetY,
    ViewTargetZ
FROM Bookmarks;";

            const string selectedNodesSql = @"
SELECT
    BookmarkName,
    NodeId
FROM BookmarkSelectedNodes;";

            const string selectedPanelsSql = @"
SELECT
    BookmarkName,
    NodeId,
    ViewRef
FROM BookmarkSelectedPanels;";

            // First, load bookmark rows (names + focus + view).
            var bookmarkCore = new Dictionary<string, (NodeId? FocusedNodeId, PanelTarget? FocusedPanel, ViewParams? View)>(StringComparer.Ordinal);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = bookmarksSql;
                using var reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    var nameOrdinal = reader.GetOrdinal("Name");
                    var focusedNodeIdOrdinal = reader.GetOrdinal("FocusedNodeId");
                    var focusedPanelNodeIdOrdinal = reader.GetOrdinal("FocusedPanelNodeId");
                    var focusedPanelViewRefOrdinal = reader.GetOrdinal("FocusedPanelViewRef");
                    var viewYawOrdinal = reader.GetOrdinal("ViewYaw");
                    var viewPitchOrdinal = reader.GetOrdinal("ViewPitch");
                    var viewDistanceOrdinal = reader.GetOrdinal("ViewDistance");
                    var viewTargetXOrdinal = reader.GetOrdinal("ViewTargetX");
                    var viewTargetYOrdinal = reader.GetOrdinal("ViewTargetY");
                    var viewTargetZOrdinal = reader.GetOrdinal("ViewTargetZ");

                    while (reader.Read())
                    {
                        var name = reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal);
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        NodeId? focusedNodeId = null;
                        if (!reader.IsDBNull(focusedNodeIdOrdinal))
                        {
                            var rawNodeId = reader.GetString(focusedNodeIdOrdinal);
                            if (TryParseNodeId(rawNodeId, out var parsed) && validNodeIds.Contains(parsed))
                            {
                                focusedNodeId = parsed;
                            }
                        }

                        PanelTarget? focusedPanel = null;
                        if (!reader.IsDBNull(focusedPanelNodeIdOrdinal))
                        {
                            var rawPanelNodeId = reader.GetString(focusedPanelNodeIdOrdinal);
                            if (TryParseNodeId(rawPanelNodeId, out var panelNodeId) && validNodeIds.Contains(panelNodeId))
                            {
                                var viewRef = reader.IsDBNull(focusedPanelViewRefOrdinal)
                                    ? null
                                    : reader.GetString(focusedPanelViewRefOrdinal);

                                if (!string.IsNullOrWhiteSpace(viewRef))
                                {
                                    focusedPanel = new PanelTarget(panelNodeId, viewRef!);
                                }
                            }
                        }

                        ViewParams? view = null;
                        if (!reader.IsDBNull(viewYawOrdinal) &&
                            !reader.IsDBNull(viewPitchOrdinal) &&
                            !reader.IsDBNull(viewDistanceOrdinal) &&
                            !reader.IsDBNull(viewTargetXOrdinal) &&
                            !reader.IsDBNull(viewTargetYOrdinal) &&
                            !reader.IsDBNull(viewTargetZOrdinal))
                        {
                            var yaw = (float)reader.GetDouble(viewYawOrdinal);
                            var pitch = (float)reader.GetDouble(viewPitchOrdinal);
                            var distance = (float)reader.GetDouble(viewDistanceOrdinal);
                            var target = new Vector3(
                                x: (float)reader.GetDouble(viewTargetXOrdinal),
                                y: (float)reader.GetDouble(viewTargetYOrdinal),
                                z: (float)reader.GetDouble(viewTargetZOrdinal));

                            view = new ViewParams(yaw, pitch, distance, target);
                        }

                        bookmarkCore[name] = (focusedNodeId, focusedPanel, view);
                    }
                }
            }

            if (bookmarkCore.Count == 0)
            {
                return result;
            }

            // Next, load selected nodes grouped by bookmark.
            var selectedNodes = new Dictionary<string, List<NodeId>>(StringComparer.Ordinal);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = selectedNodesSql;
                using var reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    var bookmarkNameOrdinal = reader.GetOrdinal("BookmarkName");
                    var nodeIdOrdinal = reader.GetOrdinal("NodeId");

                    while (reader.Read())
                    {
                        var name = reader.IsDBNull(bookmarkNameOrdinal) ? null : reader.GetString(bookmarkNameOrdinal);
                        if (string.IsNullOrWhiteSpace(name) || !bookmarkCore.ContainsKey(name))
                        {
                            continue;
                        }

                        var rawNodeId = reader.IsDBNull(nodeIdOrdinal) ? null : reader.GetString(nodeIdOrdinal);
                        if (!TryParseNodeId(rawNodeId, out var nodeId) || !validNodeIds.Contains(nodeId))
                        {
                            continue;
                        }

                        if (!selectedNodes.TryGetValue(name, out var list))
                        {
                            list = new List<NodeId>();
                            selectedNodes[name] = list;
                        }

                        list.Add(nodeId);
                    }
                }
            }

            // Then, load selected panels grouped by bookmark.
            var selectedPanels = new Dictionary<string, List<PanelTarget>>(StringComparer.Ordinal);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = selectedPanelsSql;
                using var reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    var bookmarkNameOrdinal = reader.GetOrdinal("BookmarkName");
                    var nodeIdOrdinal = reader.GetOrdinal("NodeId");
                    var viewRefOrdinal = reader.GetOrdinal("ViewRef");

                    while (reader.Read())
                    {
                        var name = reader.IsDBNull(bookmarkNameOrdinal) ? null : reader.GetString(bookmarkNameOrdinal);
                        if (string.IsNullOrWhiteSpace(name) || !bookmarkCore.ContainsKey(name))
                        {
                            continue;
                        }

                        var rawNodeId = reader.IsDBNull(nodeIdOrdinal) ? null : reader.GetString(nodeIdOrdinal);
                        if (!TryParseNodeId(rawNodeId, out var nodeId) || !validNodeIds.Contains(nodeId))
                        {
                            continue;
                        }

                        var viewRef = reader.IsDBNull(viewRefOrdinal) ? null : reader.GetString(viewRefOrdinal);
                        if (string.IsNullOrWhiteSpace(viewRef))
                        {
                            continue;
                        }

                        if (!selectedPanels.TryGetValue(name, out var list))
                        {
                            list = new List<PanelTarget>();
                            selectedPanels[name] = list;
                        }

                        list.Add(new PanelTarget(nodeId, viewRef!));
                    }
                }
            }

            // Finally, build SceneBookmark records.
            foreach (var (name, core) in bookmarkCore)
            {
                var nodeList = selectedNodes.TryGetValue(name, out var nodesForBookmark)
                    ? nodesForBookmark.Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray()
                    : Array.Empty<NodeId>();

                var panelList = selectedPanels.TryGetValue(name, out var panelsForBookmark)
                    ? panelsForBookmark
                        .Distinct()
                        .OrderBy(p => p.NodeId.ToString(), StringComparer.Ordinal)
                        .ThenBy(p => p.ViewRef, StringComparer.Ordinal)
                        .ToArray()
                    : Array.Empty<PanelTarget>();

                var bookmark = new SceneBookmark(
                    Name: name,
                    FocusedNodeId: core.FocusedNodeId,
                    SelectedNodeIds: nodeList,
                    FocusedPanel: core.FocusedPanel,
                    SelectedPanels: panelList,
                    View: core.View);

                result.Add(bookmark);
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
