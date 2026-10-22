using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Storage;

namespace Constellate.Persistence.Registry
{
    /// <summary>
    /// Phase 2 MVP Sqlite-backed authoring registries (SPEC-047).
    /// - Ensures tables exist (DDL aligned with SPEC-047).
    /// - Seeds initial rows from in-code PaneAuthoringCatalog on first run (so runtime consumes DB truth).
    /// - Exposes ListAuthoringElements/ListAuthoringBundles to EngineServices.
    /// </summary>
    public sealed class SqliteAuthoringRegistryStore : IAuthoringRegistryStore, IAuthoringRegistryWriteStore
    {
        private readonly string _dbPath;

        public SqliteAuthoringRegistryStore(PersistenceBootstrapResult bootstrap)
        {
            if (bootstrap is null) throw new ArgumentNullException(nameof(bootstrap));
            if (string.IsNullOrWhiteSpace(bootstrap.DatabasePath))
                throw new ArgumentException("Persistence bootstrap database path is missing.");

            _dbPath = bootstrap.DatabasePath;
        }

        public void EnsureInitialized()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

            using var conn = Open();
            using var tx = conn.BeginTransaction();

            Exec(conn, "PRAGMA foreign_keys = ON;");

            // Host classes (canonical vocabulary)
            Exec(conn, @"
CREATE TABLE IF NOT EXISTS pane_capability_host_classes(
  host_class_id TEXT PRIMARY KEY
);");

            // Capability entries (placeholder for future phases; useful for FKs)
            Exec(conn, @"
CREATE TABLE IF NOT EXISTS pane_capability_entries(
  capability_id TEXT PRIMARY KEY,
  display_label TEXT NOT NULL,
  capability_kind TEXT NOT NULL,
  source_domain TEXT NOT NULL,
  authority TEXT NOT NULL,
  lifetime TEXT NOT NULL,
  default_context TEXT NOT NULL,
  supported_projection_forms_json TEXT NOT NULL,
  description TEXT NULL,
  owner_kind TEXT NULL,
  binding_target_ref TEXT NULL,
  metadata_json TEXT NULL,
  enabled INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);");

            Exec(conn, @"
CREATE TABLE IF NOT EXISTS pane_capability_entry_host_classes(
  capability_id TEXT NOT NULL REFERENCES pane_capability_entries(capability_id) ON DELETE CASCADE,
  host_class_id TEXT NOT NULL REFERENCES pane_capability_host_classes(host_class_id) ON DELETE RESTRICT,
  PRIMARY KEY (capability_id, host_class_id)
);");

            // Visual element catalog
            Exec(conn, @"
CREATE TABLE IF NOT EXISTS pane_visual_element_types(
  element_type_id TEXT PRIMARY KEY,
  element_kind TEXT NOT NULL,
  display_label TEXT NOT NULL,
  description TEXT NULL,
  default_width REAL NOT NULL CHECK (default_width > 0),
  default_height REAL NOT NULL CHECK (default_height > 0),
  enabled INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);");

            Exec(conn, @"
CREATE TABLE IF NOT EXISTS pane_visual_element_type_host_classes(
  element_type_id TEXT NOT NULL REFERENCES pane_visual_element_types(element_type_id) ON DELETE CASCADE,
  host_class_id TEXT NOT NULL REFERENCES pane_capability_host_classes(host_class_id) ON DELETE RESTRICT,
  PRIMARY KEY (element_type_id, host_class_id)
);");

            // Bundles
            Exec(conn, @"
CREATE TABLE IF NOT EXISTS pane_capability_bundles(
  bundle_id TEXT PRIMARY KEY,
  display_label TEXT NOT NULL,
  description TEXT NULL,
  category TEXT NULL,
  enabled INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);");

            Exec(conn, @"
CREATE TABLE IF NOT EXISTS pane_capability_bundle_items(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  bundle_id TEXT NOT NULL REFERENCES pane_capability_bundles(bundle_id) ON DELETE CASCADE,
  element_type_id TEXT NOT NULL REFERENCES pane_visual_element_types(element_type_id) ON DELETE RESTRICT,
  display_label TEXT NOT NULL,
  description TEXT NULL,
  default_width REAL NOT NULL CHECK (default_width > 0),
  default_height REAL NOT NULL CHECK (default_height > 0),
  default_capability_id TEXT NULL REFERENCES pane_capability_entries(capability_id) ON DELETE SET NULL,
  ordinal INTEGER NOT NULL DEFAULT 0,
  group_key TEXT NULL,
  pos_x REAL NULL,
  pos_y REAL NULL
);");

            Exec(conn, @"
CREATE INDEX IF NOT EXISTS idx_bundle_items_bundle_ordinal
  ON pane_capability_bundle_items (bundle_id, ordinal);");

            // Generation / invalidation
            Exec(conn, @"
CREATE TABLE IF NOT EXISTS registry_generations(
  name TEXT PRIMARY KEY,
  version INTEGER NOT NULL,
  updated_at TEXT NOT NULL,
  updated_by TEXT NULL
);");

            // Seed canonical host classes if empty
            if (!Any(conn, "SELECT 1 FROM pane_capability_host_classes LIMIT 1;"))
            {
                foreach (var id in Enum.GetNames(typeof(PaneCapabilityHostClass)))
                {
                    Exec(conn, "INSERT OR IGNORE INTO pane_capability_host_classes(host_class_id) VALUES ($id);",
                        ("$id", id));
                }
            }

            // Seed visual elements from in-code catalog on first run
            if (!Any(conn, "SELECT 1 FROM pane_visual_element_types LIMIT 1;"))
            {
                var now = UtcNowIso();
                foreach (var e in PaneAuthoringCatalog.GetElementEntries())
                {
                    Exec(conn, @"
INSERT INTO pane_visual_element_types
(element_type_id, element_kind, display_label, description, default_width, default_height, enabled, created_at, updated_at)
VALUES ($id, $kind, $label, $desc, $w, $h, 1, $now, $now);",
                        ("$id", e.ElementTypeId),
                        ("$kind", e.ElementKind.ToString()),
                        ("$label", e.DisplayLabel),
                        ("$desc", e.Description ?? string.Empty),
                        ("$w", e.DefaultWidth),
                        ("$h", e.DefaultHeight),
                        ("$now", now));

                    foreach (var cls in e.DefaultHostClasses ?? Array.Empty<PaneCapabilityHostClass>())
                    {
                        Exec(conn, @"
INSERT OR IGNORE INTO pane_visual_element_type_host_classes (element_type_id, host_class_id)
VALUES ($id, $cls);",
                            ("$id", e.ElementTypeId),
                            ("$cls", cls.ToString()));
                    }
                }
                BumpGeneration(conn, "authoring");
            }

            // Seed bundles from in-code catalog on first run
            if (!Any(conn, "SELECT 1 FROM pane_capability_bundles LIMIT 1;"))
            {
                var now = UtcNowIso();
                foreach (var b in PaneAuthoringCatalog.GetBundleEntries())
                {
                    Exec(conn, @"
INSERT INTO pane_capability_bundles (bundle_id, display_label, description, category, enabled, created_at, updated_at)
VALUES ($id, $label, $desc, $cat, 1, $now, $now);",
                        ("$id", b.BundleId),
                        ("$label", b.DisplayLabel),
                        ("$desc", b.Description ?? string.Empty),
                        ("$cat", (object?)null ?? DBNull.Value),
                        ("$now", now));

                    for (var i = 0; i < b.Elements.Count; i++)
                    {
                        var item = b.Elements[i];
                        Exec(conn, @"
INSERT INTO pane_capability_bundle_items
(bundle_id, element_type_id, display_label, description, default_width, default_height, default_capability_id, ordinal, group_key, pos_x, pos_y)
VALUES ($bid, $etype, $label, $desc, $w, $h, $cap, $ord, $gk, $x, $y);",
                            ("$bid", b.BundleId),
                            ("$etype", item.ElementTypeId),
                            ("$label", item.DisplayLabel),
                            ("$desc", item.Description ?? string.Empty),
                            ("$w", item.DefaultWidth),
                            ("$h", item.DefaultHeight),
                            ("$cap", (object?)item.DefaultCapabilityId ?? DBNull.Value),
                            ("$ord", i),
                            ("$gk", (object?)null ?? DBNull.Value),
                            ("$x", (object?)null ?? DBNull.Value),
                            ("$y", (object?)null ?? DBNull.Value));
                    }
                }
                BumpGeneration(conn, "authoring");
            }

            // Ensure generations row exists
            if (!Any(conn, "SELECT 1 FROM registry_generations WHERE name = 'authoring' LIMIT 1;"))
            {
                Exec(conn, @"
INSERT INTO registry_generations (name, version, updated_at, updated_by)
VALUES ('authoring', 1, $now, 'init');", ("$now", UtcNowIso()));
            }

            tx.Commit();
        }

        public IReadOnlyList<PaneAuthoringElementCatalogEntry> ListAuthoringElements()
        {
            using var conn = Open();

            // Load elements
            var rows = Query(conn, @"
SELECT element_type_id, element_kind, display_label, description, default_width, default_height, enabled
FROM pane_visual_element_types
WHERE enabled = 1
ORDER BY display_label COLLATE NOCASE, element_type_id;");

            // Load host-class mappings once
            var hostMap = Query(conn, @"
SELECT element_type_id, host_class_id
FROM pane_visual_element_type_host_classes;")
                .GroupBy(r => (string)r["element_type_id"])
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => ParseHost((string)r["host_class_id"])).Where(h => h.HasValue).Select(h => h!.Value).ToArray()
                );

            var list = new List<PaneAuthoringElementCatalogEntry>(rows.Count);
            foreach (var row in rows)
            {
                var id = (string)row["element_type_id"];
                var kind = ParseElementKind((string)row["element_kind"]) ?? PaneElementKind.TextBlock;
                var label = (string)row["display_label"];
                var desc = (string?)row["description"] ?? string.Empty;
                var w = Convert.ToDouble(row["default_width"]);
                var h = Convert.ToDouble(row["default_height"]);
                var classes = hostMap.TryGetValue(id, out var cls) ? (IReadOnlyList<PaneCapabilityHostClass>)cls : Array.Empty<PaneCapabilityHostClass>();

                list.Add(new PaneAuthoringElementCatalogEntry(id, kind, label, desc, w, h, classes));
            }

            return list;
        }

        public void UpsertAuthoringElement(PaneAuthoringElementCatalogEntry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            using var conn = Open();
            using var tx = conn.BeginTransaction();
            Exec(conn, "PRAGMA foreign_keys = ON;");
            var now = UtcNowIso();
            // Upsert type row
            Exec(conn, @"
INSERT INTO pane_visual_element_types (element_type_id, element_kind, display_label, description, default_width, default_height, enabled, created_at, updated_at)
VALUES ($id, $kind, $label, $desc, $w, $h, 1, $now, $now)
ON CONFLICT(element_type_id) DO UPDATE SET
  element_kind = excluded.element_kind,
  display_label = excluded.display_label,
  description = excluded.description,
  default_width = excluded.default_width,
  default_height = excluded.default_height,
  updated_at = excluded.updated_at;",
                ("$id", entry.ElementTypeId),
                ("$kind", entry.ElementKind.ToString()),
                ("$label", entry.DisplayLabel ?? string.Empty),
                ("$desc", entry.Description ?? string.Empty),
                ("$w", entry.DefaultWidth),
                ("$h", entry.DefaultHeight),
                ("$now", now));
            // Replace host-class mappings
            Exec(conn, "DELETE FROM pane_visual_element_type_host_classes WHERE element_type_id = $id;", ("$id", entry.ElementTypeId));
            foreach (var cls in entry.DefaultHostClasses ?? Array.Empty<PaneCapabilityHostClass>())
            {
                Exec(conn, @"
INSERT OR IGNORE INTO pane_visual_element_type_host_classes (element_type_id, host_class_id)
VALUES ($id, $cls);",
                    ("$id", entry.ElementTypeId),
                    ("$cls", cls.ToString()));
            }
            BumpGeneration(conn, "authoring");
            tx.Commit();
        }

        public void UpsertAuthoringBundle(PaneAuthoringBundleCatalogEntry bundle)
        {
            if (bundle is null) throw new ArgumentNullException(nameof(bundle));
            using var conn = Open();
            using var tx = conn.BeginTransaction();
            Exec(conn, "PRAGMA foreign_keys = ON;");
            var now = UtcNowIso();
            Exec(conn, @"
INSERT INTO pane_capability_bundles (bundle_id, display_label, description, category, enabled, created_at, updated_at)
VALUES ($id, $label, $desc, $cat, 1, $now, $now)
ON CONFLICT(bundle_id) DO UPDATE SET
  display_label = excluded.display_label,
  description = excluded.description,
  category = excluded.category,
  updated_at = excluded.updated_at;",
                ("$id", bundle.BundleId),
                ("$label", bundle.DisplayLabel ?? string.Empty),
                ("$desc", bundle.Description ?? string.Empty),
                ("$cat", (object?)null ?? DBNull.Value),
                ("$now", now));
            // Replace items
            Exec(conn, "DELETE FROM pane_capability_bundle_items WHERE bundle_id = $id;", ("$id", bundle.BundleId));
            for (var i = 0; i < (bundle.Elements?.Count ?? 0); i++)
            {
                var e = bundle.Elements![i];
                Exec(conn, @"
INSERT INTO pane_capability_bundle_items
(bundle_id, element_type_id, display_label, description, default_width, default_height, default_capability_id, ordinal, group_key, pos_x, pos_y)
VALUES ($bid, $etype, $label, $desc, $w, $h, $cap, $ord, $gk, $x, $y);",
                    ("$bid", bundle.BundleId),
                    ("$etype", e.ElementTypeId),
                    ("$label", e.DisplayLabel ?? string.Empty),
                    ("$desc", e.Description ?? string.Empty),
                    ("$w", e.DefaultWidth),
                    ("$h", e.DefaultHeight),
                    ("$cap", (object?)e.DefaultCapabilityId ?? DBNull.Value),
                    ("$ord", i),
                    ("$gk", (object?)null ?? DBNull.Value),
                    ("$x", (object?)null ?? DBNull.Value),
                    ("$y", (object?)null ?? DBNull.Value));
            }
            BumpGeneration(conn, "authoring");
            tx.Commit();
        }

        public bool TryGetGenerationVersion(string name, out int version)
        {
            version = 0;
            using var conn = Open();
            var rows = Query(conn, @"SELECT version FROM registry_generations WHERE name = $n LIMIT 1;", ("$n", name));
            if (rows.Count == 0) return false;
            var obj = rows[0].TryGetValue("version", out var v) ? v : null;
            if (obj is null) return false;
            version = Convert.ToInt32(obj);
            return true;
        }

        public IReadOnlyList<PaneAuthoringBundleCatalogEntry> ListAuthoringBundles()
        {
            using var conn = Open();

            // Preload element meta and host-class mappings to resolve specs quickly
            var elements = Query(conn, @"
SELECT element_type_id, element_kind, default_width, default_height
FROM pane_visual_element_types;")
                .ToDictionary(
                    r => (string)r["element_type_id"],
                    r => new
                    {
                        Kind = ParseElementKind((string)r["element_kind"]) ?? PaneElementKind.TextBlock,
                        W = Convert.ToDouble(r["default_width"]),
                        H = Convert.ToDouble(r["default_height"])
                    });

            var elementHostMap = Query(conn, @"
SELECT element_type_id, host_class_id FROM pane_visual_element_type_host_classes;")
                .GroupBy(r => (string)r["element_type_id"])
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => ParseHost((string)r["host_class_id"])).Where(h => h.HasValue).Select(h => h!.Value).ToArray()
                );

            var bundlesRaw = Query(conn, @"
SELECT bundle_id, display_label, description, category
FROM pane_capability_bundles
WHERE enabled = 1
ORDER BY display_label COLLATE NOCASE, bundle_id;");

            var list = new List<PaneAuthoringBundleCatalogEntry>(bundlesRaw.Count);

            foreach (var b in bundlesRaw)
            {
                var bundleId = (string)b["bundle_id"];
                var label = (string)b["display_label"];
                var desc = (string?)b["description"] ?? string.Empty;

                var items = Query(conn, @"
SELECT element_type_id, display_label, description, default_width, default_height, default_capability_id, ordinal
FROM pane_capability_bundle_items
WHERE bundle_id = $bid
ORDER BY ordinal, id;", ("$bid", bundleId));

                var specs = new List<PaneAuthoringBundleElementSpec>(items.Count);
                foreach (var it in items)
                {
                    var elementTypeId = (string)it["element_type_id"];
                    var elementMeta = elements.TryGetValue(elementTypeId, out var em) ? em : null;
                    var kind = elementMeta?.Kind ?? PaneElementKind.TextBlock;
                    var itemLabel = (string)it["display_label"];
                    var itemDesc = (string?)it["description"] ?? string.Empty;

                    // Prefer item defaults; fall back to type defaults
                    var w = it["default_width"] is double dw ? dw :
                            it["default_width"] is float fw ? (double)fw :
                            (elementMeta?.W ?? 240.0);

                    var h = it["default_height"] is double dh ? dh :
                            it["default_height"] is float fh ? (double)fh :
                            (elementMeta?.H ?? 72.0);

                    var defaultCap = it["default_capability_id"] is string cap && !string.IsNullOrWhiteSpace(cap) ? cap : null;
                    var classes = elementHostMap.TryGetValue(elementTypeId, out var cls) ? (IReadOnlyList<PaneCapabilityHostClass>)cls : Array.Empty<PaneCapabilityHostClass>();

                    specs.Add(new PaneAuthoringBundleElementSpec(
                        elementTypeId,
                        kind,
                        itemLabel,
                        itemDesc,
                        w,
                        h,
                        classes,
                        defaultCap));
                }

                list.Add(new PaneAuthoringBundleCatalogEntry(bundleId, label, desc, specs));
            }

            return list;
        }

        private SqliteConnection Open()
        {
            var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                ForeignKeys = true
            }.ToString());
            conn.Open();
            return conn;
        }

        private static void Exec(SqliteConnection conn, string sql, params (string name, object value)[] prms)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in prms)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            cmd.ExecuteNonQuery();
        }

        private static List<Dictionary<string, object?>> Query(SqliteConnection conn, string sql, params (string name, object value)[] prms)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in prms)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }

            using var rd = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
            var list = new List<Dictionary<string, object?>>();
            while (rd.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < rd.FieldCount; i++)
                {
                    row[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i);
                }
                list.Add(row);
            }
            return list;
        }

        private static bool Any(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var obj = cmd.ExecuteScalar();
            return obj is not null && obj != DBNull.Value;
        }

        private static void BumpGeneration(SqliteConnection conn, string name)
        {
            var now = UtcNowIso();
            // If row exists, bump; else insert (defensive)
            Exec(conn, @"
INSERT INTO registry_generations (name, version, updated_at, updated_by)
VALUES ($name, 1, $now, 'seed')
ON CONFLICT(name) DO UPDATE SET
  version = registry_generations.version + 1,
  updated_at = $now,
  updated_by = 'seed';",
                ("$name", name),
                ("$now", now));
        }

        private static string UtcNowIso() => DateTimeOffset.UtcNow.ToString("o");

        private static PaneElementKind? ParseElementKind(string raw)
        {
            if (Enum.TryParse<PaneElementKind>(raw, ignoreCase: true, out var k)) return k;
            return null;
        }

        private static PaneCapabilityHostClass? ParseHost(string raw)
        {
            if (Enum.TryParse<PaneCapabilityHostClass>(raw, ignoreCase: true, out var k)) return k;
            return null;
        }
    }
}
