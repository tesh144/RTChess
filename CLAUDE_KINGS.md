# King's Renewal — Claude Config

> **Purpose:** Project-specific config for the King's Renewal repo. This is the entry point for any agent working on that project. It defines which files to read at session start, project IDs (Trello, Sheets), and KR-specific task routing rules. Read this instead of the RTChess CLAUDE.md when working on King's Renewal. Rules here override general preferences in CLAUDE_USER_JAI.md where they conflict.

Read all of these files at the start of every session:

1. **CLAUDE_USER_JAI.md** — Jai's portable working preferences, communication style, and Trello workflow
2. **JAI_AI_SYNC.md** — AI agent sync log. Read before starting work, update when making changes.
3. **kings/Documents/KINGSRENEWAL.md** — Project-specific documentation (architecture, systems, code reference)

## Project Config

- **Repo CLAUDE.md:** `kings/CLAUDE.md` — maintained by Ceck_ (lead developer). Read this too.
- **Handoff doc:** `COWORK_SESSION_2_HANDOFF.md` (same folder as this file)
- **AI sync log:** `JAI_AI_SYNC.md` (same folder as this file)
- **Project docs:** `kings/Documents/KINGSRENEWAL.md`, `kings/Documents/kings-renewal-gdd.md`
- **Trello:** King's Renewal board
- **Google Sheets:** `KINGS RENEWAL NEW - BALANCING`
- **Notion task list:** page ID `31b39a10-1010-8132-96bc-e1ddd362190e`

## Trello Task Routing

- **Cards assigned to Jai** → Claude (this agent) should work on these.
- **Cards assigned to Ceck_** → Skip. Ceck_ uses his own agents.
- If a card has no member assigned, check with Jai before starting work on it.

**Label hygiene:** Every card in Suggestions / Tasks must have at least one category label. Never leave a card with no labels.
