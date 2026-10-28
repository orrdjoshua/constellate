using System;
using Constellate.Core.Storage;

namespace Constellate.Core.Messaging
{
    /// <summary>
    /// Global coordinator for the current EngineWorldSession.
    ///
    /// Phase 5 intent:
    /// - Provide a single place where the engine/runtime knows "which world is active".
    /// - Own creation, activation, switching, and shutdown of world sessions without
    ///   requiring the entire application to restart.
    /// - Allow App/UI layers (world selector, Escape overlay) to request world switches
    ///   explicitly, while preserving the existing EngineServices facade for callers.
    ///
    /// This first scaffolding pass defines the surface and concurrency model but leaves
    /// the session-creation/switching bodies unimplemented. The following refactor will
    /// move the logic from EngineServices.EnsureInitialized into this type.
    /// </summary>
    public static class EngineHost
    {
        private static readonly object Gate = new();

        private static IEnginePersistenceScope? _configuredPersistenceScope;
        private static EngineWorldSession? _current;

        /// <summary>
        /// The currently active world session, or null if none has been created yet.
        /// </summary>
        public static EngineWorldSession? Current
        {
            get
            {
                lock (Gate)
                {
                    return _current;
                }
            }
        }

        /// <summary>
        /// Identifier for the currently active world, when available. In this initial
        /// Phase-5 pass the id is derived from the backing database file name; a later
        /// EngineProjectRecord-backed store may override or enrich this identity, but
        /// callers should always prefer accessing it through this façade.
        /// </summary>
        public static string? CurrentWorldId
        {
            get
            {
                lock (Gate)
                {
                    return _current?.WorldId;
                }
            }
        }

        /// <summary>
        /// User-facing name for the currently active world, when available. Today this
        /// is derived from the database file name (or a simple placeholder for in-
        /// memory/seed-only sessions). Future world-selection and overlay UX should read
        /// through this property rather than binding directly to EngineWorldSession.
        /// </summary>
        public static string? CurrentWorldDisplayName
        {
            get
            {
                lock (Gate)
                {
                    return _current?.WorldDisplayName;
                }
            }
        }

        /// <summary>
        /// Full path to the database file backing the currently active world, when
        /// available. This is intended for diagnostics and world-selection/provenance
        /// UIs rather than as a persistence hook for callers.
        /// </summary>
        public static string? CurrentWorldDatabasePath
        {
            get
            {
                lock (Gate)
                {
                    return _current?.WorldDatabasePath;
                }
            }
        }

        /// <summary>
        /// Indicates whether the current world session is backed by a database file
        /// that was created for this run (new world) versus an existing file (existing
        /// world). This is the façade callers should consult when deciding between
        /// first-run seeding and migrate-only behavior.
        /// </summary>
        public static bool CurrentWorldIsNewWorld
        {
            get
            {
                lock (Gate)
                {
                    return _current?.IsNewWorld ?? false;
                }
            }
        }

        /// <summary>
        /// Configure the persistence scope that should be used when creating the first
        /// world session for this process. Program/App startup is expected to call this
        /// once per process, before any EngineServices/EngineHost initialization occurs.
        ///
        /// In the mature design this scope will represent the backing for a specific
        /// world database file chosen by the user at startup (world selection UI).
        /// </summary>
        public static void ConfigurePersistenceScope(IEnginePersistenceScope persistenceScope)
        {
            if (persistenceScope is null) throw new ArgumentNullException(nameof(persistenceScope));

            lock (Gate)
            {
                _configuredPersistenceScope = persistenceScope;
            }
        }

        /// <summary>
        /// Ensure that there is an active world session, creating one if necessary.
        ///
        /// In this initial scaffolding pass, world-session creation logic still lives
        /// in EngineServices.EnsureInitialized; this method will be wired up in a
        /// subsequent refactor and then become the single place that constructs
        /// EngineWorldSession instances from the configured persistence scope.
        /// </summary>
        public static EngineWorldSession EnsureWorldSession()
        {
            lock (Gate)
            {
                if (_current is not null)
                {
                    return _current;
                }

                var session = EngineServices.CreateWorldSession(_configuredPersistenceScope);
                _current = session;
                return session;
            }
        }

        internal static void AdoptSession(EngineWorldSession session)
        {
            lock (Gate)
            {
                _current = session;
            }
        }

        /// <summary>
        /// Switch to a new world session using the supplied persistence scope.
        ///
        /// In the mature multi-world design, this will:
        /// - persist any dirty state in the current session (according to global save
        ///   behavior),
        /// - dispose the current EngineWorldSession,
        /// - construct a new session bound to the supplied scope (new world DB),
        /// - set it as EngineHost.Current,
        /// - and notify interested parties (renderer, App shell) that the session
        ///   has changed.
        ///
        /// For this initial scaffolding pass, the body is intentionally left as a
        /// NotImplementedException so that the shape of the API is clear while we
        /// still rely on the existing EngineServices static kernel.
        /// </summary>
        public static EngineWorldSession SwitchWorld(IEnginePersistenceScope persistenceScope)
        {
            if (persistenceScope is null) throw new ArgumentNullException(nameof(persistenceScope));

            lock (Gate)
            {
                var current = _current;
                _current = null;

                if (current is not null)
                {
                    current.Dispose();
                }

                _configuredPersistenceScope = persistenceScope;
                var session = EngineServices.CreateWorldSession(persistenceScope);
                _current = session;
                return session;
            }
        }

        /// <summary>
        /// Shut down the current world session without creating a new one. This is the
        /// engine-level counterpart of closing the last world; App-level code may then
        /// either exit or present the world-selection experience again.
        ///
        /// In the initial scaffolding pass, this only calls Dispose on the current
        /// session (if any) and clears the reference. Later refactors may also flush
        /// any pending persistence buffers here according to global save behavior.
        /// </summary>
        public static void ShutdownWorld()
        {
            lock (Gate)
            {
                if (_current is null)
                {
                    return;
                }

                try
                {
                    _current.Dispose();
                }
                finally
                {
                    _current = null;
                }
            }
        }
    }
}
