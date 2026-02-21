# Little Cafe - Game Design Documentation

This folder contains all design documentation for the **Little Cafe** game - a restaurant management game built on top of the RTChess grid system.

## 📁 Folder Structure

```
Docs/LittleCafe/
├── README.md              ← You are here
├── GDD/                   ← Game Design Documents
│   └── Little-Cafe-GDD.docx
├── Diagrams/              ← Visual diagrams and layouts
│   └── little-cafe-design-diagram-correct.html
├── Prompts/               ← Implementation guides for Claude Code
│   └── little-cafe-unity-prompts.md
└── References/            ← Original design handoff and requirements
    └── little-cafe-handoff.md
```

## 🎯 How This Works

### For Cowork Claude (Design & Planning)
When working with Cowork mode:
- All new design docs will be saved directly here
- Updates to existing docs happen in place
- No manual copying needed - documents appear instantly in VS Code

### For VS Code Claude (Implementation)
When implementing in Unity:
1. Read the relevant document from this folder
2. Example: `"Read Docs/LittleCafe/Prompts/little-cafe-unity-prompts.md and implement Phase 1"`
3. All context is already in the project - no uploads needed

## 📋 Key Documents

### 1. Unity Implementation Prompts
**File:** `Prompts/little-cafe-unity-prompts.md`
**Purpose:** Step-by-step implementation guide for all game phases
**Start here:** This is your main implementation roadmap

### 2. Design Diagram
**File:** `Diagrams/little-cafe-design-diagram-correct.html`
**Purpose:** Visual reference for the exact cafe layout
**How to view:** Open in any browser to see the interactive grid

### 3. Game Design Document
**File:** `GDD/Little-Cafe-GDD.docx`
**Purpose:** Complete game design specification with all systems documented

### 4. Original Handoff
**File:** `References/little-cafe-handoff.md`
**Purpose:** Original design requirements and decisions

## 🚀 Getting Started

**Phase 1 Priority:** Kitchen Builder

1. Read `Prompts/little-cafe-unity-prompts.md`
2. Reference `Diagrams/little-cafe-design-diagram-correct.html` for exact layout
3. Start implementing the drag-and-drop equipment placement system

**Important:** This extends RTChess - don't modify existing RTChess code, create new LittleCafe scripts that reuse the shared systems (grid, pathfinding, camera).

## 🔄 Workflow

**Design Changes:**
1. Discuss with Cowork Claude
2. Cowork updates documents directly in this folder
3. Documents appear in VS Code immediately
4. VS Code Claude reads updated docs and implements changes

**Version Control:**
All these docs are in your Git repository, so:
```bash
git add Docs/LittleCafe/
git commit -m "Add/update Little Cafe design docs"
git push
```

## 📞 Questions?

If anything is unclear during implementation:
- Refer to the handoff document for design decisions
- Check the visual diagram for exact positioning
- Default to Plate Up mechanics when in doubt
- Prioritize player control and feedback

---

**Project Type:** Restaurant Management Game
**Base System:** RTChess/Clockwork Grid (15x15 isometric grid)
**Inspiration:** Plate Up, Overcooked
**Target Platform:** Mobile (iOS/Android)
