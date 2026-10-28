using System;
using System.IO;
using Constellate.Core.Capabilities;
using Constellate.Core.Capabilities.Panes;
using Constellate.Core.Scene;
using Constellate.Core.Storage;

namespace Constellate.Core.Messaging
{
    /// <summary>
    /// Represents one concrete engine/runtime world session: a scene, shell scene state,
    /// command/event bus pair, pane catalog, capabilities, engine settings, and persistence scope.
    /// This is the unit that EngineHost will create, activate, and dispose when switching
    /// between worlds (database-backed projects).
    ///
    /// Phase 5 intent:
    /// - One EngineWorldSession instance corresponds to one project/world database file.
    /// - All world-scoped runtime state (scene, shell-scene, undo history, etc.) should
    ///   eventually be rooted here rather than in static EngineServices fields.
    /// - EngineHost will own the lifecycle of these sessions and coordinate world switching.
    /// </summary>
    public sealed class EngineWorldSession : IDisposable
    {
        public EngineWorldSession(
            IEnginePersistenceScope? persistenceScope,
            ICommandBus commandBus,
            IEventBus eventBus,
            EngineScene scene,
            ShellSceneState shellScene,
            IPaneCatalog paneCatalog,
            ICapabilityRegistry capabilities,
            EngineSettings settings)
        {
            PersistenceScope = persistenceScope;
            CommandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
            EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            ShellScene = shellScene ?? throw new ArgumentNullException(nameof(shellScene));
            PaneCatalog = paneCatalog ?? throw new ArgumentNullException(nameof(paneCatalog));
            Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            var bootstrap = persistenceScope?.BootstrapResult;
            if (bootstrap is not null)
            {
                WorldDatabasePath = bootstrap.DatabasePath;
                IsNewWorld = bootstrap.DatabaseFileCreated;

                var fileName = Path.GetFileNameWithoutExtension(bootstrap.DatabasePath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    WorldId = bootstrap.DatabasePath;
                    WorldDisplayName = "World";
                }
                else
                {
                    WorldId = fileName;
                    WorldDisplayName = fileName;
                }
            }
            else
            {
                WorldDatabasePath = null;
                WorldId = "world:in-memory";
                WorldDisplayName = "In-memory World";
                IsNewWorld = false;
            }
        }

        /// <summary>
        /// Persistence scope backing this world session. In the mature design this is
        /// the face of one world database: durable truth for scene, pane definitions,
        /// workspaces, projection bindings, resource registry, native records, etc.
        /// </summary>
        public IEnginePersistenceScope? PersistenceScope { get; }

        /// <summary>
        /// Stable world identifier derived from the backing database. In this initial
        /// pass it is based on the database file name so App/UI layers can distinguish
        /// worlds before a fuller EngineProjectRecord-backed identity is wired in.
        /// </summary>
        public string WorldId { get; }

        /// <summary>
        /// User-facing world name derived from the database file name (or a simple
        /// placeholder for in-memory/seed-only sessions).
        /// </summary>
        public string WorldDisplayName { get; }

        /// <summary>
        /// Full path to the backing database file for this world, when available.
        /// </summary>
        public string? WorldDatabasePath { get; }

        /// <summary>
        /// Indicates whether this session is backed by a database file that was just
        /// created for this run (new world) versus an existing file (existing world).
        /// </summary>
        public bool IsNewWorld { get; }

        /// <summary>
        /// World-scoped command bus instance.
        /// </summary>
        public ICommandBus CommandBus { get; }

        /// <summary>
        /// World-scoped event bus instance.
        /// </summary>
        public IEventBus EventBus { get; }

        /// <summary>
        /// The engine scene for this world: nodes, links, groups, bookmarks, and related state.
        /// </summary>
        public EngineScene Scene { get; }

        /// <summary>
        /// Shell-scene overlay state for this world: selection/focus summaries, view history,
        /// and other UI-facing projections of engine/world state.
        /// </summary>
        public ShellSceneState ShellScene { get; }

        /// <summary>
        /// Pane catalog in effect for this world.
        /// In early stages this remains largely seeded; as durability increases it may
        /// be enriched from database-backed definitions.
        /// </summary>
        public IPaneCatalog PaneCatalog { get; }

        /// <summary>
        /// Capability registry for this world.
        /// </summary>
        public ICapabilityRegistry Capabilities { get; }

        /// <summary>
        /// Engine settings currently in effect for this world.
        /// </summary>
        public EngineSettings Settings { get; }

        /// <summary>
        /// Hook for world-session level initialization (e.g., command/event registration)
        /// once the session is constructed. For now this is a placeholder; existing
        /// initialization remains inside EngineServices until the EngineHost refactor
        /// migrates that logic here.
        /// </summary>
        public void Initialize()
        {
            // Intentionally left as a no-op for the first scaffolding pass.
            // Command/event registration, seed scene creation, and other setup will
            // be moved here from EngineServices during the EngineHost refactor.
        }

        /// <summary>
        /// Dispose any world-scoped resources owned by this session.
        /// In the first scaffolding pass we do not yet track per-session disposables;
        /// EngineHost will grow the responsibility to tear down handlers and other
        /// resources during world switching and call this method accordingly.
        /// </summary>
        public void Dispose()
        {
            // Placeholder for now. As we move more ownership (subscriptions, timers,
            // persistence batching, etc.) into EngineWorldSession, they should be
            // released here so that switching worlds can safely free the previous one.
        }
    }
}