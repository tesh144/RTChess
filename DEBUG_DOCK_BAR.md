# Debug: Dock Bar Using Wrong UI

## Issue
The game is creating runtime UI (cyan cards) instead of using your manually created dock bar with red cards.

## Diagnosis Steps

### 1. Check Console Log
Look for this message in your Console when game starts:
```
"DockBarManager: Using editor-created UI elements"  ← Should see this!
```

OR one of these error messages:
```
"DockBarManager: No UI references assigned and createUIAtRuntime is false!"
"DockBarManager: Created UI at runtime"  ← This means references weren't found
```

### 2. Check Inspector References
1. Stop Play Mode
2. Select **DockBarManager** in Hierarchy
3. In Inspector, check **DockBarManager** component:
   - **Dock Icons Container**: Should have a reference (not "None")
   - **Draw Button**: Should have a reference (not "None")
   - **Draw Button Text**: Can be "None" (optional)
   - **Create UI At Runtime**: Should be UNCHECKED ❌

### 3. Verify Scene Was Saved
The git merge may have caused issues. Check:
1. File → Save (Ctrl+S / Cmd+S)
2. Check if scene has unsaved changes (asterisk * in tab title)

---

## Quick Fix Options

### Option A: Reload Scene (Fastest)
1. **Stop Play Mode**
2. **Close the scene** (right-click scene tab → Close)
3. **Reopen scene**: Assets/Scenes/SampleScene.unity
4. **Verify references** in DockBarManager Inspector
5. **Enter Play Mode** again

### Option B: Re-assign References
1. **Stop Play Mode**
2. Select **DockBarManager** in Hierarchy
3. In Inspector, **clear and re-assign**:
   - Dock Icons Container → Find your red cards' parent container
   - Draw Button → Find your white/blue draw button
4. **Save scene** (Ctrl+S / Cmd+S)
5. **Enter Play Mode**

### Option C: Force Scene Reload
1. **Stop Play Mode**
2. Run this in terminal:
```bash
cd /Users/jai/RTChess
git status
# If scene has changes, discard them:
git checkout Assets/Scenes/SampleScene.unity
```
3. **Reopen Unity** (may need to reimport)
4. **Verify references** and test

---

## Expected Behavior When Fixed

### Console Output:
```
DockBarManager: Using editor-created UI elements
Starting hand: 3 Soldiers
```

### Visual:
- Your **red card holders** at bottom should populate with cards
- **Blue buttons** on right side become your draw button
- No cyan/white runtime cards should appear

---

## If Still Not Working

Check these:

1. **References point to correct objects:**
   - Click the reference field → it should highlight the object in Hierarchy
   - Verify it's the correct parent container

2. **Container is a child of Canvas:**
   - Your dock icons container should be under UICanvas
   - Not floating standalone in scene

3. **Button has Button component:**
   - Your draw button must have `Button` component attached
   - Check it's not just an Image

4. **Console shows no errors:**
   - Red errors can prevent initialization
   - Check for NullReferenceException messages

---

## Debug Mode

If you want to force runtime creation to test:
1. Select DockBarManager
2. Check **"Create UI At Runtime"** box
3. Clear all three reference fields
4. Enter Play Mode
5. Should create cyan cards like currently shown

Then reverse to use editor UI:
1. Uncheck "Create UI At Runtime"
2. Re-assign the three references
3. Enter Play Mode

---

## What's Happening

The code has this logic:
```csharp
if (dockIconsContainer != null && drawButton != null)
{
    SetupEditorUI();  // Use your red cards
}
else if (createUIAtRuntime)
{
    CreateDockBarUI(canvas);  // Create cyan cards
}
else
{
    Debug.LogError("No UI references!");
}
```

If you're seeing cyan cards, it means either:
- References are "None" (null) in Inspector
- OR `createUIAtRuntime` is checked (true)
- OR the scene file didn't reload after git operations

Check which case applies using the steps above!
