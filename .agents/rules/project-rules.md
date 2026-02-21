---
description: Core project rules, initialization protocol, and coding guidelines
---

# Project Rules

## Core Guidelines
- **UI language**: All user-facing text (labels, buttons, window titles, tooltips, message boxes) must be in English.
- **Preserve user's existing choices**: When editing existing UI or code, keep properties and settings the user has already set. Do not overwrite them with the agent's own defaults or preferences unless the user explicitly asks to change them.
- **No hardcoding**: Use configuration, constants, or data instead of magic values.
- **Build and Test Allowed, NO Open App**: You are allowed to build and test the code to verify your changes, but explicitly NEVER launch or open the application window, as the user will handle that manually. Provide the code changes and verify they compile/test successfully.
- **Locating Files**: If you have a hard time locating a specific file or resource, do not run exhaustive or long-running search scripts. Instead, immediately prompt the user for help or clarification on where the file might be located.
- **Cleanup Temporary Files**: If you generate any temporary files, compilation logs, or debug output (such as `build_warnings.txt`) while performing a task, you MUST delete them immediately after the task finishes to keep the workspace clean.
