#pragma warning disable CS0414, CS0219, CS0618
#if DEVELOPMENT_BUILD || UNITY_EDITOR

using UnityEngine;
using ClockworkGrid;
using ClockworkCraft;

namespace LittleCafe
{
    /// <summary>
    /// Dev-only IMGUI cheat menu. Stripped from release builds via
    /// #if DEVELOPMENT_BUILD || UNITY_EDITOR — this entire file is compiled
    /// out unless the Unity "Development Build" checkbox is ticked or the
    /// game is running inside the Editor.
    ///
    /// Auto-created at runtime — no scene setup required.
    ///
    /// Toggle: small "CHEAT" button visible in the top-right corner of the screen.
    ///
    /// Tabs:
    ///   Buildings — adds any building from BuildingDatabase to the player's hand.
    ///   Units     — adds any unit or enemy from UnitDatabase to the player's hand.
    ///   Workers   — adds any worker from WorkerDatabase to the player's hand.
    ///   Resources — give resources (Food replaced with Meat).
    ///   Fog       — reveal all fog, or reset fog back to fully hidden.
    /// </summary>
    public class DevCheatMenu : MonoBehaviour
    {
        public static DevCheatMenu Instance { get; private set; }

        // ── Auto-create ───────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindFirstObjectByType<DevCheatMenu>() != null) return;
            new GameObject("[DevCheatMenu]").AddComponent<DevCheatMenu>();
        }

        // ── Global cheat flags (read by DragDropHandler, DockBarManager, BPM, etc.) ─
        /// <summary>When true, all placement and draw costs are bypassed. Dev builds only.</summary>
        public static bool FreeCosts = false;
        /// <summary>When true, all building production intervals are forced to 1 second. Dev builds only.</summary>
        public static bool InstantProduction = false;

        // ── Window state ──────────────────────────────────────────────────────
        private bool isOpen = false;
        private int selectedTab = 0;
        private Vector2 scrollPos;
        // x is set to top-right on first OnGUI call; y/w/h are the real defaults.
        private Rect windowRect = new Rect(0, 52, 560, 650);
        private bool windowRectInitialised = false;
        private static readonly string[] TabNames = { "Buildings", "Units", "Workers", "Resources", "Fog" };

        // ── Database cache ────────────────────────────────────────────────────
        private BuildingDatabase buildingDb;
        private UnitDatabase unitDb;
        private WorkerDatabase workerDb;
        private bool dbCached = false;

        // ── Resource helpers ──────────────────────────────────────────────────
        // Food replaced with Meat — Food is not currently used in game.
        private static readonly ResourceType[] AllResources =
        {
            ResourceType.Gold, ResourceType.Wood, ResourceType.Meat,
            ResourceType.Stone, ResourceType.Water, ResourceType.Clay, ResourceType.Flowers
        };

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── OnGUI ─────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            // Position the window in the top-RIGHT corner on first call so it
            // doesn't overlap the map or the game's HUD (which live top-left).
            if (!windowRectInitialised)
            {
                windowRect.x = Screen.width - windowRect.width - 10f;
                windowRectInitialised = true;
            }

            // Toggle button — larger and slightly lower than the original,
            // sitting in the top-right corner flush above the cheat window.
            float btnW = 74f;
            float btnX = Screen.width - btnW - 10f;
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = isOpen
                ? new Color(0.8f, 0.4f, 0.1f)    // amber when open
                : new Color(0.15f, 0.15f, 0.15f); // dark when closed
            if (GUI.Button(new Rect(btnX, 16, btnW, 28), "<b>CHEAT</b>", ToggleButtonStyle()))
            {
                isOpen = !isOpen;
                if (isOpen && !dbCached) CacheDatabases();
            }
            GUI.backgroundColor = prevColor;

            if (!isOpen) return;

            windowRect = GUILayout.Window(98765, windowRect, DrawWindow, "  DEV CHEAT MENU  (dev builds only)");
        }

        private GUIStyle ToggleButtonStyle()
        {
            var s = new GUIStyle(GUI.skin.button);
            s.fontSize = 11;
            s.richText = true;
            s.normal.textColor  = new Color(1f, 0.85f, 0.3f);
            s.hover.textColor   = Color.white;
            s.active.textColor  = Color.white;
            return s;
        }

        // ── Window draw ───────────────────────────────────────────────────────
        private void DrawWindow(int id)
        {
            selectedTab = GUILayout.Toolbar(selectedTab, TabNames);
            GUILayout.Space(4);

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(564));

            switch (selectedTab)
            {
                case 0: DrawBuildingsTab(); break;
                case 1: DrawUnitsTab();     break;
                case 2: DrawWorkersTab();   break;
                case 3: DrawResourcesTab(); break;
                case 4: DrawFogTab();       break;
            }

            GUILayout.EndScrollView();

            // ── Always-visible global cheats ──────────────────────────────────
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();

            // Toggle instant production (all intervals → 1 second while ON)
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = InstantProduction ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.3f, 0.6f, 1f);
            string timerLabel = InstantProduction ? "⚡ Skip Timers  [ON]" : "⚡ Skip Timers [OFF]";
            if (GUILayout.Button(timerLabel))
                InstantProduction = !InstantProduction;

            // Toggle free costs (placement + draw button)
            GUI.backgroundColor = FreeCosts ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
            string freeCostLabel = FreeCosts ? "💸 Free Costs  [ON]" : "💸 Free Costs [OFF]";
            if (GUILayout.Button(freeCostLabel))
                FreeCosts = !FreeCosts;

            GUI.backgroundColor = prevBg;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            if (GUILayout.Button("Close"))
                isOpen = false;

            GUI.DragWindow();
        }

        // ── Tab: Buildings ────────────────────────────────────────────────────
        private void DrawBuildingsTab()
        {
            if (buildingDb == null)
            {
                GUILayout.Label("BuildingDatabase not found. Play in a scene that uses MapGeneratorV2.");
                return;
            }

            if (DockBarManager.Instance == null)
            {
                GUILayout.Label("DockBarManager not in scene — start a game session to add cards to hand.");
                return;
            }

            GUILayout.Label("Adds the card to your hand (drag to place as normal):");
            GUILayout.Space(6);

            bool any = false;
            foreach (var data in buildingDb.AllBuildings)
            {
                if (data == null || data.prefab == null) continue;
                any = true;
                string label = data.GetCleanName();
                if (DockBarManager.Instance.IsHandFull)
                    GUI.enabled = false;
                if (GUILayout.Button(label))
                    AddBuildingToHand(data);
                GUI.enabled = true;
            }

            if (!any)
                GUILayout.Label("No buildings with prefabs assigned.");

            if (DockBarManager.Instance.IsHandFull)
            {
                GUILayout.Space(4);
                GUILayout.Label("Hand is full (max 5 cards). Place a card first.");
            }
        }

        // ── Tab: Units ────────────────────────────────────────────────────────
        private void DrawUnitsTab()
        {
            if (unitDb == null)
            {
                GUILayout.Label("UnitDatabase not found. Play in a scene that uses MapGeneratorV2.");
                return;
            }

            if (DockBarManager.Instance == null)
            {
                GUILayout.Label("DockBarManager not in scene — start a game session to add cards to hand.");
                return;
            }

            GUILayout.Label("Adds the card to your hand (drag to place as normal):");
            GUILayout.Space(6);

            bool handFull = DockBarManager.Instance.IsHandFull;

            // Allied section
            bool anyAllied = false;
            GUILayout.Label("── Allied ──────────────────");
            foreach (var data in unitDb.AllUnits)
            {
                if (data == null || data.prefab == null || data.isEnemy) continue;
                anyAllied = true;
                if (handFull) GUI.enabled = false;
                if (GUILayout.Button(data.GetCleanName()))
                    AddUnitToHand(data);
                GUI.enabled = true;
            }
            if (!anyAllied) GUILayout.Label("(none with prefabs)");

            GUILayout.Space(4);

            // Enemy section
            bool anyEnemy = false;
            GUILayout.Label("── Enemies ─────────────────");
            foreach (var data in unitDb.AllUnits)
            {
                if (data == null || data.prefab == null || !data.isEnemy) continue;
                anyEnemy = true;
                if (handFull) GUI.enabled = false;
                if (GUILayout.Button(data.GetCleanName()))
                    AddUnitToHand(data);
                GUI.enabled = true;
            }
            if (!anyEnemy) GUILayout.Label("(none with prefabs)");

            if (handFull)
            {
                GUILayout.Space(4);
                GUILayout.Label("Hand is full (max 5 cards). Place a card first.");
            }
        }

        // ── Tab: Workers ──────────────────────────────────────────────────────
        private void DrawWorkersTab()
        {
            if (workerDb == null)
            {
                GUILayout.Label("WorkerDatabase not found — it may not be loaded in this scene.");
                return;
            }

            if (DockBarManager.Instance == null)
            {
                GUILayout.Label("DockBarManager not in scene — start a game session to add cards to hand.");
                return;
            }

            GUILayout.Label("Adds a worker card to your hand (drag to place as normal):");
            GUILayout.Space(6);

            bool handFull = DockBarManager.Instance.IsHandFull;
            bool any = false;

            foreach (var data in workerDb.AllWorkers)
            {
                if (data == null || data.prefab == null) continue;
                any = true;
                if (handFull) GUI.enabled = false;
                if (GUILayout.Button(data.GetCleanName()))
                    AddWorkerToHand(data);
                GUI.enabled = true;
            }

            if (!any)
                GUILayout.Label("No workers with prefabs assigned.");

            if (handFull)
            {
                GUILayout.Space(4);
                GUILayout.Label("Hand is full (max 5 cards). Place a card first.");
            }
        }

        // ── Tab: Resources ────────────────────────────────────────────────────
        private void DrawResourcesTab()
        {
            if (ResourceManager.Instance == null)
            {
                GUILayout.Label("ResourceManager not in scene.");
                return;
            }

            GUILayout.Label("Give resources (also unlocks locked types):");
            GUILayout.Space(6);

            foreach (var type in AllResources)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label(type.ToString(), GUILayout.Width(80));

                if (GUILayout.Button("+100", GUILayout.Width(60)))
                    GiveResource(type, 100);
                if (GUILayout.Button("+1 000", GUILayout.Width(66)))
                    GiveResource(type, 1000);
                if (GUILayout.Button("+10 000", GUILayout.Width(76)))
                    GiveResource(type, 10000);

                int current = ResourceManager.Instance.GetResource(type);
                GUILayout.Label($"= {current}");

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Max All Resources  (+10 000 each)"))
            {
                foreach (var type in AllResources)
                    GiveResource(type, 10000);
            }

            GUILayout.Space(8);

            // Legacy token system
            if (ResourceTokenManager.Instance != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Tokens: {ResourceTokenManager.Instance.CurrentTokens}", GUILayout.Width(140));
                if (GUILayout.Button("+100 Tokens"))
                    ResourceTokenManager.Instance.AddTokens(100);
                if (GUILayout.Button("+1 000 Tokens"))
                    ResourceTokenManager.Instance.AddTokens(1000);
                GUILayout.EndHorizontal();
            }
        }

        // ── Tab: Fog ──────────────────────────────────────────────────────────
        private void DrawFogTab()
        {
            if (FogManager.Instance == null)  { GUILayout.Label("FogManager not in scene.");  return; }
            if (GridManager.Instance == null) { GUILayout.Label("GridManager not in scene."); return; }

            int w = GridManager.Instance.Width;
            int h = GridManager.Instance.Height;
            int revealed = FogManager.Instance.GetRevealedCount();
            int total    = w * h;

            GUILayout.Label($"Grid size:  {w} × {h}");
            GUILayout.Label($"Revealed:   {revealed} / {total} cells  ({(total > 0 ? revealed * 100 / total : 0)}%)");
            GUILayout.Space(10);

            if (GUILayout.Button("Reveal All Fog"))
                RevealAllFog();

            GUILayout.Space(4);
            if (GUILayout.Button("Reset Fog  (re-hide entire map)"))
                ResetFog();
        }

        // ── Add to Hand: Building ─────────────────────────────────────────────
        private static void AddBuildingToHand(BuildingData data)
        {
            var dock = DockBarManager.Instance;
            if (dock == null)
            {
                Debug.LogWarning("[DevCheatMenu] DockBarManager not found — cannot add building card.");
                return;
            }

            // Synthesise a UnitStats at runtime (same pattern as DockBarManager.AddWorkerCard).
            // This gives DragDropHandler all the info it needs to place the building correctly.
            var stats = ScriptableObject.CreateInstance<UnitStats>();
            stats.unitType              = UnitType.Soldier;       // generic stand-in
            stats.unitName              = data.GetCleanName();
            stats.rarity                = Rarity.Common;
            stats.drawWeight            = data.drawWeight;
            stats.iconSprite            = data.icon;
            stats.unitColor             = Color.white;
            stats.unitPrefab            = data.prefab;
            stats.enemyPrefab           = null;
            stats.resourceCost          = data.placementCost;
            stats.gridSize              = data.gridSize;
            stats.shape                 = data.shape;
            stats.modelScale            = data.visualScale;
            stats.isActive              = data.isActive;
            stats.behaviorType          = BehaviorType.RotateAndInteract;
            stats.isAllied              = true;
            stats.killerAdvances        = data.killerAdvances;
            stats.cardSource            = CardSourceType.Building;
            stats.tier                  = data.tier;
            stats.revealRadius          = data.fogRevealRadius;
            stats.maxHP                 = data.hp;
            stats.attackDamage          = data.attackPower;
            stats.furnitureTypeOverride = -1;
            stats.isRandomBuilding      = data.isRandomBuilding;
            stats.allyInteractible      = data.allyInteractible;
            stats.enemyInteractible     = data.enemyInteractible;
            stats.wildAnimalInteractible = data.wildAnimalInteractible;
            stats.isMealSource          = data.isMealSource;
            stats.productionInputType   = data.productionInputType;
            stats.productionOutputType  = data.productionOutputType;
            stats.productionInterval    = data.productionInterval;
            stats.productionIntervalBonus = data.productionIntervalBonus;
            stats.producedResourceType  = data.producedResourceType;
            stats.productionAmount      = data.productionAmount;
            stats.productionCostResourceType = data.productionCostResourceType;
            stats.productionCostAmount  = data.productionCostAmount;

            dock.AddCard(stats, markAsNew: true);
            Debug.Log($"[DevCheatMenu] Added building '{data.GetCleanName()}' to hand.");
        }

        // ── Add to Hand: Unit ─────────────────────────────────────────────────
        private static void AddUnitToHand(UnitData data)
        {
            var dock = DockBarManager.Instance;
            if (dock == null)
            {
                Debug.LogWarning("[DevCheatMenu] DockBarManager not found — cannot add unit card.");
                return;
            }

            var stats = ScriptableObject.CreateInstance<UnitStats>();
            stats.unitType              = UnitType.Soldier;
            stats.unitName              = data.GetCleanName();
            stats.rarity                = Rarity.Common;
            stats.drawWeight            = data.drawWeight;
            stats.iconSprite            = data.icon;
            stats.unitColor             = Color.white;
            stats.unitPrefab            = data.prefab;
            stats.enemyPrefab           = null;
            stats.gridSize              = data.gridSize;
            stats.modelScale            = data.visualScale;
            stats.isActive              = data.isActive;
            stats.behaviorType          = data.behaviorType;
            stats.isAllied              = !data.isEnemy;
            stats.killerAdvances        = data.killerAdvances;
            stats.cardSource            = CardSourceType.Unit;
            stats.tier                  = data.tier;
            stats.maxHP                 = data.hp;
            stats.attackDamage          = data.attackPower;
            stats.lootResourceType      = data.lootResourceType;
            stats.lootHpCost            = data.lootHpCost;
            stats.lootYield             = data.lootYield;
            stats.furnitureTypeOverride = -1;

            dock.AddCard(stats, markAsNew: true);
            Debug.Log($"[DevCheatMenu] Added {(data.isEnemy ? "enemy" : "allied")} unit '{data.GetCleanName()}' to hand.");
        }

        // ── Add to Hand: Worker ───────────────────────────────────────────────
        private static void AddWorkerToHand(WorkerData data)
        {
            var dock = DockBarManager.Instance;
            if (dock == null)
            {
                Debug.LogWarning("[DevCheatMenu] DockBarManager not found — cannot add worker card.");
                return;
            }

            dock.AddWorkerCard(data, consumeReservation: false, animateFromDraw: true);
            Debug.Log($"[DevCheatMenu] Added worker '{data.GetCleanName()}' to hand.");
        }

        // ── Give Resource ─────────────────────────────────────────────────────
        private static void GiveResource(ResourceType type, int amount)
        {
            ResourceManager.Instance.UnlockResource(type);
            ResourceManager.Instance.AddResource(type, amount);
        }

        // ── Fog helpers ───────────────────────────────────────────────────────
        private static void RevealAllFog()
        {
            int w = GridManager.Instance.Width;
            int h = GridManager.Instance.Height;
            int cx = w / 2;
            int cy = h / 2;
            int maxReach = Mathf.CeilToInt(Mathf.Sqrt(w * w + h * h) / 2f) + 1;
            FogManager.Instance.RevealRadius(cx, cy, maxReach);
            Debug.Log("[DevCheatMenu] Revealed all fog.");
        }

        private static void ResetFog()
        {
            var gm = GridManager.Instance;
            var fm = FogManager.Instance;
            if (gm == null || fm == null) return;

            // 1. Reset FogManager internal state — all cells back to fogged.
            //    Note: TileFog meshes stay visually raised because TileFog has no
            //    public re-fog method. Use "Reveal All" after this if tile meshes look off.
            fm.Initialize(gm.Width, gm.Height);

            // 2. Rebuild dark overlay quads for the now-fogged grid.
            var fogVis = FogGridVisualizer.Instance;
            if (fogVis != null)
                fogVis.RefreshFogOverlays();

            Debug.Log("[DevCheatMenu] Fog reset — FogManager state cleared and overlay quads rebuilt.");
        }

        // ── Database cache ────────────────────────────────────────────────────
        /// <summary>
        /// Populate database references from currently loaded ScriptableObjects.
        /// Called the first time the panel is opened.
        /// </summary>
        private void CacheDatabases()
        {
            // MapGeneratorV2 is the most reliable source for unit + building DBs
            var mapGen = FindFirstObjectByType<MapGeneratorV2>();
            if (mapGen != null)
            {
                unitDb     = mapGen.unitDatabase;
                buildingDb = mapGen.buildingDatabase;
            }

            // Fall back to scanning all loaded ScriptableObjects
            if (unitDb == null)
            {
                var found = Resources.FindObjectsOfTypeAll<UnitDatabase>();
                if (found.Length > 0) unitDb = found[0];
            }
            if (buildingDb == null)
            {
                var found = Resources.FindObjectsOfTypeAll<BuildingDatabase>();
                if (found.Length > 0) buildingDb = found[0];
            }

            // WorkerDatabase is not on MapGeneratorV2; scan all loaded assets.
            // BuildingProductionManager holds a reference in-scene, try that first.
            var bpm = FindFirstObjectByType<BuildingProductionManager>();
            if (bpm != null) workerDb = bpm.workerDatabase;
            if (workerDb == null)
            {
                var found = Resources.FindObjectsOfTypeAll<WorkerDatabase>();
                if (found.Length > 0) workerDb = found[0];
            }

            dbCached = true;
            Debug.Log($"[DevCheatMenu] Databases cached — " +
                      $"Buildings: {buildingDb?.Count ?? 0}, " +
                      $"Units: {unitDb?.Count ?? 0}, " +
                      $"Workers: {workerDb?.Count ?? 0}");
        }
    }
}

#endif
