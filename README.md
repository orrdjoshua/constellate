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

Troubleshooting
- Legacy Constellate.slnx in repo root:
  - If a non-standard Constellate.slnx exists, the setup script now backs it up to Constellate.slnx.bak.TIMESTAMP and creates a proper Constellate.sln used by dotnet and VS Code tasks.
  - If running commands manually, rename Constellate.slnx to a .bak before creating the solution.
- Template “Project capabilities: No project was found at the path …” messages:
  - These are safe warnings that appear while installing/updating Avalonia templates. They do not affect solution/project creation.
- net10.0 template compatibility:
  - If your local template/runtime combo resists net10.0, re-run the setup script with -Framework net8.0 and proceed. We can bump TFMs later.


How to set relevant environment variables:

$env:CONSTELLATE_GL_DIAG="1"
$env:CONSTELLATE_GL_SELFTEST="1"