# Constellate

Hybrid 2D/3D creator engine with a semantics layer.

Quickstart (VS Code)
- Prereqs: .NET 10 SDK, C# + Avalonia extensions installed.
- Build: Run the "build" task.
- Launch: F5 with ".NET Launch (Constellate.App)".
- Dev loop: Use "watch-app" task for hot reload on src/Constellate.App.

Structure
- src/Constellate.App — Avalonia desktop app
- src/Constellate.Core — core contracts (entities, commands/events)
- src/Constellate.Renderer.OpenTK — OpenGL renderer adapter
- src/Constellate.SDK — out-of-proc envelope POCOs and helpers

Notes
- Target Framework: net10.0
- Initial GL surface wiring will land in subsequent commits.
