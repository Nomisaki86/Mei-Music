# Agent Routing

- Before broad exploration, read `docs/agent-map.md` to identify likely ownership and entry points.
- Start with likely entry files first; use targeted searches instead of repository-wide scans.
- First pass default: about 6 files and 2-3 targeted searches, then reassess.
- If still unclear, ask one focused clarification question or expand incrementally when required.
- Avoid repeating identical reads/searches when recent output is still valid.
- For small edits in known files, skip routing exploration and implement directly.
- If routing/ownership changes because of your edits, update `docs/agent-map.md` in the same task.
- In the final response, include one line: `Agent map update: updated|not needed`.

# Project Rules

## Product and UX
- **UI language**: All user-facing text (labels, buttons, window titles, tooltips, message boxes) must be in English.
- **Preserve user's existing choices**: When editing existing UI or code, keep properties and settings the user has already set. Do not overwrite them with the agent's own defaults or preferences unless the user explicitly asks to change them.

## Code and maintainability
- **Single Source of Truth**: For multiple things with the same functionality or template, hold a single reusable template that can be used to avoid duplicate coding.
- **No hardcoding**: Use configuration, constants, or data instead of magic values.
- **Comments should be concise and useful**: Add comments only where logic is non-obvious, and keep them intent-focused.
- **Avoid cosmetic-only churn**: Do not perform broad formatting or style-only refactors unless explicitly requested.
- **File moves are user-driven by default**: Do not move/rename files automatically unless explicitly requested. When reorganization is needed, provide the move list for the user to do manually, then update namespaces/usings/references afterward.

## Execution safety
- **Build/test allowed, no auto-launch**: Running build/test is allowed, but NEVER launch the application window automatically.
- **Locating Files**: If a specific file/resource location is unclear, ask for clarification before broad searching.
- **Cleanup Temporary Files**: Remove temporary logs and scratch artifacts before finishing.

## Delivery format
- **Changed files with line ranges**: When files are edited, report each changed file(in the format that is click to jump to, relative path)  with what changed and the affected line range(s).

- **Readable per-file layout**: Use bullet points; put the clickable path on one line and the explanation on the next line. Include line numbers for each change.

# Token Budget Defaults

- Be concise by default; expand only on request.
- Keep progress updates brief for simple tasks; use periodic updates for longer tasks.
- Prefer targeted searches/reads and avoid repeating the same lookups.
- Read focused file ranges first, then expand only if needed.
- Batch independent read/search steps when possible.
- Ask before expensive operations (full builds/tests, broad scans, network-heavy steps) unless the user requested them.
- For small, low-risk edits, skip full build/test by default and state what was not run.
- If scope is unclear, ask one focused question before broad exploration.
- If the user explicitly requests deep review/validation, prioritize completeness over token savings.
