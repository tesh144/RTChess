# SFX & GUI Implementation Proposal — ClockworkCraft

## Asset Inventory Summary

**Sound Effects:** 331 .wav files across the Interface And Item Sounds pack (now in `ThirdParty/InterfaceSounds/`), plus 5 existing SFX in `Audio/SFX/`.

**GUI Kit:** 660+ prefabs, 913+ sprites, 4 fonts with SDF atlases, across buttons, frames, popups, sliders, labels, and utility UI (now in `ThirdParty/GUIProKit/`).

---

## PART 1: SOUND EFFECTS

### Tier 1 — High Impact, I Can Do Solo

These are code-only changes — wiring up AudioClip fields and PlayOneShot calls to existing game events. No Unity Editor work needed from you.

#### 1. Unified GameSFXManager (replaces SFXManager + PlacementAudioManager)
Right now there are two competing audio singletons doing basically the same thing. I'd merge them into one `GameSFXManager` with categorized AudioClip slots:

```
[Header("Placement")]    placementDrop, placementError
[Header("UI")]           buttonClick, cardDraw, cardSlide, dragStart, dragCancel
[Header("Combat")]       hitImpact, hitWeak, entityDeath, damageFlash
[Header("Resources")]    coinCollect, lootBurst, lootArrival, resourceDepleted
[Header("Production")]   timerComplete, popupAppear, rewardCollect
[Header("Feedback")]     errorBuzz, successChime, fogReveal
[Header("Ambient")]      clockTick (optional, subtle)
```

**Recommended sound mappings from the pack:**
- `placementDrop` → Pop (3).wav or Boing (2).wav — satisfying tactile drop
- `placementError` → Buzz Error (1).wav — can't place here
- `buttonClick` → Click (3).wav — clean, snappy
- `cardDraw` → Special Pop (4).wav or Item Purchase (2).wav — card appears
- `dragStart` → Switch (2).wav — pickup feel
- `dragCancel` → Click Back (3).wav — release/cancel
- `hitImpact` → existing sfx_sword_slash.wav or sfx_sword_rock.wav
- `hitWeak` → Pop (1).wav — light tap/nudge
- `entityDeath` → Crunch Bite Item (3).wav or existing sfx_sword_rock.wav
- `coinCollect` → Coins (5).wav — single coin jingle
- `lootBurst` → Special & Powerup (8).wav — burst of particles
- `lootArrival` → Coins (1).wav or Crystal Reward Tick.wav — landing ding
- `resourceDepleted` → Gong (1).wav — deep finish tone
- `timerComplete` → Special (3).wav or Alert (1).wav — attention-getter
- `popupAppear` → Boing (1).wav — bouncy popup
- `rewardCollect` → Item Purchase (3).wav or Star Collect.wav — satisfying grab
- `errorBuzz` → Buzz Error (2).wav — can't afford / hand full
- `successChime` → Special & Powerup (12).wav — positive feedback
- `fogReveal` → quick transitions (1).wav — soft whoosh for discovery
- `clockTick` → Count Prize (Single Tick).wav — subtle background pulse

#### 2. Drag & Drop Sound Layer
Wire into existing DragDropHandler:
- **OnBeginDrag** → play `dragStart` (card lifts off dock)
- **Valid cell hover** → subtle click as you move between cells
- **Invalid placement** → play `placementError`
- **Successful drop** → play `placementDrop` (already partially exists)
- **Cancel/release** → play `dragCancel`

#### 3. Combat & Interaction Sounds
Wire into GridEntityActor and GridEntityHealth:
- **interact_strong trigger** → play `hitImpact` at target position
- **interact_weak trigger** → play `hitWeak`
- **TakeDamage()** → play impact sound with pitch variation based on damage
- **OnEntityDestroyed** → play `entityDeath`
- **ResourceNode depleted** → play `resourceDepleted`

#### 4. Loot & Currency Collection
Wire into ResourceLootFX and CoinFlyEffect:
- **Loot burst spawn** → play `lootBurst`
- **Each particle arrival** → play `coinCollect` with slight pitch variation
- **ResourceManager.AddResource** → play `lootArrival` on final tally

#### 5. Building Production Sounds
Wire into BuildingProductionManager:
- **Timer completes** → play `timerComplete`
- **Popup spawns** → play `popupAppear`
- **Player taps to collect** → play `rewardCollect`
- **Hand full (can't collect)** → play `errorBuzz`

#### 6. Card Draw & Dock Bar
Wire into DockBarManager:
- **Draw button press** → play `cardDraw`
- **Card slides into dock** → play `cardSlide` (quick transitions (2).wav)
- **Can't afford draw** → play `errorBuzz` + flash
- **Worker card arrives from building** → play `successChime`

#### 7. Fog of War Discovery
Wire into FogManager.OnCellRevealed:
- **Cells revealed** → play `fogReveal` (debounced — one sound per reveal batch, not per cell)
- Could vary pitch slightly based on number of cells revealed

#### 8. Enhanced MusicSystem
The existing MusicSystem already has lobby/battle crossfade. I'd add:
- **Multiple track slots** per game phase (lobby, building, exploration, combat)
- **Smooth crossfade** when state changes (e.g., first worker placed → building music)
- **Volume ducking** when important SFX play (reward collect, timer complete)
- **Ambient layer** — optional looping wind/nature that fades in as map is revealed

**Recommended track assignments:**
- `lobbyTrack` → clockwork_grid_lobby.wav (already exists)
- `buildingTrack` → clockwork_grid_theme_1.wav (already exists)
- `ambientLoop` → Windmill [loop].wav from the sound pack — gentle ambient

---

### Tier 2 — Needs Your Help (Inspector Assignment)

These features I can write the code for, but you'd need to assign the specific AudioClip references in the Unity Inspector after I create the manager.

#### 9. Per-Database Sound Overrides
Add an optional `AudioClip interactSound` field to each database entry (Environment, Worker, Unit) so different objects can have unique sounds:
- Trees → wood chop sound
- Rocks → pickaxe clang
- Gold → metallic ring
- Workers → varied grunt/effort

This requires you to drag clips onto each database entry in the Inspector.

#### 10. Ambient Soundscape
Add positional AudioSources to environment clusters:
- Water tiles → gentle water loop
- Forest clusters → bird/wind ambience
- These would be authored in the scene, not code-only

---

## PART 2: GUI KIT

### Tier 1 — I Can Do Solo

#### 11. Production Timer → World-Space Canvas Prefab
Replace the current programmatic donut timer with a designed world-space Canvas using the GUI Kit's slider sprites:
- Use `Slider_Custom` sprites for a styled progress ring/bar
- Frame it with a small `BasicFrame_Circle` from the kit
- Much better looking than the raw Unity Image fill we have now
- Billboard toward camera, positioned above buildings

#### 12. Reward Popup Redesign
Replace the current SpriteRenderer popup with a proper GUI Kit popup:
- Use `Popup_Custom` or `Popup_Demo_Common` as background frame
- Reward icon centered inside
- Subtle bounce animation
- "Tap to collect" label using kit fonts (Quicksand-Bold)
- Notification badge (`Notify_Count`) showing amount if > 1

#### 13. Card Draw Button Reskin
The gacha/draw button could use the GUI Kit's styled buttons:
- `Btn_IconButton_Circle_121` for the main draw button
- Color variant based on affordability (green when affordable, gray when not)
- `Notify_Count` badge showing draw cost

#### 14. Dock Bar Card Frames
Each card in the dock bar could use the kit's frame sprites:
- `BasicFrame_Circle` or `BasicFrame_Rectangle` around each card icon
- Color-coded by type (workers = blue frame, buildings = orange frame)
- `Label_CornerTag` for rarity or cost badge

#### 15. Resource Bar Reskin
The currency display bars at top could use:
- `StatusBar_Group` prefab from the kit
- Styled coin/gem/resource icons from `Demo_Icon/Shop` sprites
- Consistent with the kit's visual language

#### 16. Floating Damage Numbers
The kit includes `Text_CriticalNum_Red_64` prefab with MuseoModerno-ExtraBold font:
- Use this for floating damage numbers when workers hit targets
- Red for damage dealt, green for healing/collection
- Already has SDF font atlas for crisp rendering at any scale

#### 17. Tutorial/Onboarding Overlays
The kit has a complete tutorial focus system:
- `Tutorial_Focus_Circle` / `Tutorial_Focus_Square` for highlighting UI elements
- `Tutorial_Focus_Icon_Hand` for pointing at interactable elements
- Could create a first-time-player tutorial: "Draw a card" → "Drag to place" → "Watch your worker explore"

---

### Tier 2 — Needs Your Involvement

#### 18. Full UI Theme Pass
Apply a consistent theme (Dark or Light) across all game UI:
- Replace placeholder UI elements with kit prefabs
- Consistent button styling across debug menu, dock bar, draw button
- This is a larger visual polish pass we'd do together

#### 19. Settings/Options Panel
Build a proper settings panel using kit prefabs:
- `Toggle_Switch` for fog toggle, sound on/off
- `Slider` prefabs for volume control, game speed
- `Popup` frame as the settings window
- `Btn_MenuButton_Rectangle` for close/apply

#### 20. Victory/Defeat Screens
The kit has victory/defeat decorative elements:
- Ribbons, crowns, skull icons in Demo_Image
- `You Win` / `You Lose` sounds from the SFX pack
- Combine with `MiddlePopup` frames for end-game screens

---

## PART 3: IMPLEMENTATION PRIORITY

### Phase 1 — Sound Foundation (I do this now, solo)
1. Create GameSFXManager (unified audio singleton)
2. Wire all placement, combat, loot, and production sounds
3. Add drag-and-drop audio feedback
4. Add card draw and UI feedback sounds
5. Enhanced MusicSystem with state-based tracks

### Phase 2 — GUI Quick Wins (I do this now, solo)
6. World-space Canvas timer prefab (replaces programmatic donut)
7. Reward popup using kit frames
8. Floating damage numbers with kit font

### Phase 3 — Visual Polish (together)
9. Card/dock bar reskin with kit frames
10. Resource bar reskin
11. Draw button reskin
12. Full UI theme consistency pass

### Phase 4 — Future
13. Tutorial system
14. Settings panel
15. Per-database sound overrides
16. Ambient soundscapes
17. Victory/defeat screens

---

## Recommended Sound Picks (My Top Choices)

| Game Event | Sound File | Why |
|---|---|---|
| Card placement | Pop (3).wav | Satisfying tactile pop |
| Placement error | Buzz Error (1).wav | Clear "nope" feedback |
| Button click | Click (3).wav | Clean, not too loud |
| Card draw | Item Purchase (2).wav | Purchase/acquire feel |
| Drag start | Switch (2).wav | Mechanical pickup |
| Coin collect | Coins (5).wav | Classic coin jingle |
| Loot burst | Special & Powerup (8).wav | Exciting burst |
| Timer done | Special (3).wav | Attention-getting chime |
| Popup appear | Boing (1).wav | Playful bounce |
| Reward grab | Star Collect.wav | Satisfying achievement |
| Error/can't afford | Buzz Error (2).wav | Gentle warning |
| Entity death | Gong (1).wav | Weighty finish |
| Fog reveal | quick transitions (1).wav | Soft whoosh |
| Worker hit | sfx_sword_slash.wav | Already in project |
| Clock tick | Count Prize (Single Tick).wav | Subtle pulse |
