# Unity Skills Pack for Claude

A comprehensive set of Unity development skills for Claude (Cowork mode) that prevent common errors and provide battle-tested patterns for Unity C# programming.

## 📦 What's Included

### 1. unity-asset-management
**Size:** ~12KB | **Focus:** Prefabs, Materials, FBX, Shaders, Textures

Covers the Asset Layer vs Runtime Layer distinction and prevents material conversion failures.

**Key Features:**
- Material conversion workflows with `PrefabUtility.LoadPrefabContents` pattern
- Creating persistent material assets
- Shader property validation with `HasProperty()`
- FBX import and shadow mesh handling
- Common failure patterns and solutions

**Triggers:** prefab, material, shader, texture, FBX, asset organization

---

### 2. unity-editor-scripting
**Size:** ~7KB | **Focus:** Editor Automation, Batch Operations, Custom Tools

Covers Unity Editor namespace APIs and automation workflows.

**Key Features:**
- `[MenuItem]` patterns for quick actions
- Batch asset processing with progress bars
- `InitializeOnLoad` for auto-execution
- Custom Editor Windows
- AssetDatabase and EditorUtility APIs
- Best practices for try/finally and saving assets

**Triggers:** editor script, batch operation, custom tool, menu item, EditorWindow

---

### 3. unity-gameplay-dev
**Size:** ~16KB | **Focus:** MonoBehaviour, Game Logic, Runtime Behavior

Covers C# gameplay programming, lifecycle methods, and runtime patterns.

**Key Features:**
- MonoBehaviour lifecycle (Awake → Start → Update → FixedUpdate)
- Singleton manager pattern
- Event-driven systems (subscribe/unsubscribe)
- Coroutines with proper cleanup
- Null-safe component access
- Physics and collision handling
- Common runtime errors and solutions

**Triggers:** MonoBehaviour, game logic, gameplay, player controller, coroutines, physics

---

## 🚀 Installation

### Method 1: Manual Installation (Recommended)

1. **Download the package:**
   - Download and unzip `unity-skills-package.zip`

2. **Locate your skills directory:**
   - Navigate to your workspace folder
   - Find the hidden `.claude/skills/` directory
   - Path is typically: `<your-workspace>/.claude/skills/`

3. **Copy the skill folders:**
   ```bash
   cp -r unity-asset-management ~/.claude/skills/
   cp -r unity-editor-scripting ~/.claude/skills/
   cp -r unity-gameplay-dev ~/.claude/skills/
   ```

4. **Verify installation:**
   - Start a new Claude conversation
   - Ask: "What Unity skills do you have?"
   - Claude should list all 3 Unity skills

### Method 2: Using Claude to Install

1. Open Claude (Cowork mode)
2. Upload the `unity-skills-package` folder
3. Ask Claude: "Please install these Unity skills to my .claude/skills directory"
4. Claude will copy them to the correct location

---

## ✨ How They Work

These skills work **automatically** - you don't need to manually invoke them. Claude will use them when you:

- **Ask Unity questions:** "How do I convert materials to URP?"
- **Request Unity tasks:** "Create a prefab with custom materials"
- **Write Unity code:** "Implement a player controller with coroutines"
- **Automate workflows:** "Batch process all FBX files in my project"

Claude automatically selects the right skill(s) based on your request.

---

## 📖 Usage Examples

### Example 1: Material Conversion
```
You: "Convert all materials in my PEPO prefab to Unlit/Texture shader"
Claude: [automatically uses unity-asset-management skill]
        [asks clarifying questions about paths and backup preferences]
        [implements PrefabUtility.LoadPrefabContents pattern]
        [validates shader properties before setting]
```

### Example 2: Editor Automation
```
You: "Create a menu item that batch-renames all prefabs in Assets/Characters"
Claude: [automatically uses unity-editor-scripting skill]
        [implements [MenuItem] pattern with AssetDatabase]
        [adds progress bar and error handling]
```

### Example 3: Gameplay Code
```
You: "Implement a unit that rotates and attacks on an interval timer"
Claude: [automatically uses unity-gameplay-dev skill]
        [implements Singleton pattern for timer]
        [uses event-driven system with OnEnable/OnDisable]
        [adds coroutine with proper cleanup]
```

---

## 🛡️ What These Skills Prevent

✅ **Asset Management:**
- Material conversion failures (Asset Layer vs Runtime Layer confusion)
- Lost material properties during shader conversion
- FBX import issues and shadow mesh problems
- Prefab modification errors

✅ **Editor Scripting:**
- Missing try/finally cleanup blocks
- Forgotten AssetDatabase.SaveAssets() calls
- Missing progress bars for long operations
- Incorrect AssetDatabase API usage

✅ **Gameplay Programming:**
- NullReferenceException from missing components
- Memory leaks from unsubscribed events
- Orphaned coroutines after object destruction
- Physics inconsistencies from wrong Update method
- Singleton duplication across scenes
- Collision detection failures

---

## 🎯 Best Practices

1. **Let Claude ask questions** - The skills include mandatory clarifying questions to prevent ambiguous implementations

2. **Use natural language** - Just describe what you want; Claude will select the right skill

3. **Trust the patterns** - These are battle-tested patterns that prevent common Unity failures

4. **Review the output** - Skills provide comprehensive error handling, but always review generated code

---

## 🔧 Troubleshooting

### Skills not triggering?
- Check that folders are in `.claude/skills/` directory
- Verify each folder has a `SKILL.md` file at the root level
- Restart Claude/create a new conversation

### Skills conflicting?
- The skills are designed to work together and have clear separation:
  - **Asset management:** Disk-based asset manipulation
  - **Editor scripting:** Unity Editor automation
  - **Gameplay dev:** Runtime MonoBehaviour code

### Need to update a skill?
- Edit the `SKILL.md` file directly
- Changes take effect in new conversations

---

## 📝 Technical Details

### Skill Structure
```
unity-asset-management/
├── SKILL.md              # Main skill content
├── assets/               # Optional: templates, examples
├── references/           # Optional: API docs, schemas
└── scripts/              # Optional: helper scripts
```

### Skill Metadata (in SKILL.md frontmatter)
```yaml
---
name: unity-asset-management
description: Use this skill for all Unity prefab, material, FBX import...
---
```

Claude reads the frontmatter to determine when to automatically invoke each skill.

---

## 🤝 Contributing

Found an issue or want to improve a skill?

1. Edit the `SKILL.md` file in the skill directory
2. Test your changes with Claude
3. Share your improvements with the community

---

## 📜 License

These skills are provided as-is for Unity development with Claude. Feel free to modify, share, and adapt them for your needs.

---

## 🙏 Credits

Built by: Jai (@semonebunnag@gmail.com)
Created with: Claude Code (Anthropic)
Based on: Real-world Unity failures and lessons learned from RTChess project

---

## 📚 Additional Resources

### Related Skills
These Unity skills work well alongside:
- **docx skill:** Generate Unity documentation
- **pdf skill:** Create Unity technical specs
- **xlsx skill:** Track Unity asset inventories

### Learn More
- Unity Manual: https://docs.unity3d.com/Manual/
- Unity Scripting API: https://docs.unity3d.com/ScriptReference/
- Claude Documentation: https://docs.claude.com/

---

**Version:** 1.0.0
**Last Updated:** February 16, 2026
**Tested with:** Claude Sonnet 4.5 (Cowork mode)
