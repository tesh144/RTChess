# ClockworkCraft — Claude Config

> **Purpose:** Project-specific config for the ClockworkCraft / RTChess repo. This is the entry point for any agent working on this project. It defines which files to read at session start, project IDs (Trello, Sheets), available skills, and RTChess-specific task routing rules. Rules here override general preferences in CLAUDE_USER_JAI.md where they conflict.

Read all of these files at the start of every session:

1. **CLAUDE_USER_JAI.md** — Jai's portable working preferences, communication style, and Trello workflow
2. **JAI_AI_SYNC.md** — AI agent sync log. Read before starting work, update when making changes.
3. **RTChess.md** — Project-specific documentation (architecture, systems, code reference, standing rules)

## Project Config

- **AI sync log:** `JAI_AI_SYNC.md` (repo root)
- **Project docs:** `RTChess.md` (repo root)
- **Trello:** Auto RTS board — board ID `69bd0b7483af459744b7a24c`
- **Google Sheets:** ID `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0`
## Skills Available

Key skills installed in `.claude/skills/`. Trigger by describing the task — no slash command needed.

| Skill | When to use |
|---|---|
| `trelloworkflow` | Session start, before any Trello task, after completing code changes |
| `brainstorming` | Before any new feature, component, or architectural change |
| `architecture-decision` | Before systemic changes — generates an ADR |
| `code-review` | Before touching any file over 500 lines |
| `tech-debt` | Periodic audit — file sizes, TODOs, debt register |
| `reverse-document` | When a system has no design doc and you need to map it first |
| `scope-check` | When a task feels bigger than the card describes |
| `design-review` | Before finalising a UI or system design |
| `bug-report` | When logging a reproducible bug to Trello |
| `hotfix` | Urgent fixes that need to bypass normal flow |
| `sprint-plan` | Planning a focused work sprint |
| `retrospective` | End-of-sprint or post-milestone review |

## Trello Task Routing

When reading tasks from the Trello board:

- **Cards assigned to Jai** → Claude (this agent) should work on these.
- **Cards assigned to Will (DMoT)** → Skip. Will uses his own agents to handle his tasks.
- If a card has no member assigned, check with Jai before starting work on it.

**Label hygiene:** Every card in Suggestions / Tasks must have at least one category label (Behavior, Feature, System, Visual, etc.). Never leave a card with no labels.
