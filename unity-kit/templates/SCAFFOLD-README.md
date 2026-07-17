# unity-kit project scaffold

You dropped this into an **existing Unity project folder**. It contains:

- `CLAUDE.md` — project instructions for Claude Code. **Fill the `{{PLACEHOLDERS}}`** (project name, Unity version, 2D/3D, pipeline) or ask Claude to fill them from the project.
- `.claude/settings.json` — permission guard rails (blocks edits to `Library/`, `Temp/`, etc.)
- `.gitignore` / `.gitattributes` — Unity-aware git configuration
- `Docs/DESIGN.md` — design doc template; make it the source of truth for your game

Two manual steps:

1. Add MCP for Unity to `Packages/manifest.json` dependencies:
   ```json
   "com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"
   ```
2. Install the unity-kit plugin (once, user-wide) for the skills/agents/hooks:
   ```
   claude plugin marketplace add Benjamin-Curlier/unity-kit
   claude plugin install unity-kit@bencu-plugins
   ```

Then open the project in Unity (the MCP server auto-starts) and start Claude Code.
