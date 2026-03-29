#pragma warning disable CS0414, CS0219, CS0618
using System;
using System.Collections.Generic;

namespace LittleCafe.Editor
{
    /// <summary>
    /// Partial: column name constants, cache validation, JSON parsing, and parse helpers
    /// for SheetSyncEditor.
    ///
    /// Column name constants (SheetKey.* and Col.*) are the single source of truth for
    /// every string used to look up sheet names and row values. Rename a sheet or column
    /// here and the compiler catches every callsite — no more silent off-by-one errors
    /// when columns are inserted or reordered in Google Sheets.
    /// </summary>
    public partial class SheetSyncEditor
    {
        // ─────────────────────────────────────────────────────────────────
        // Sheet Key Constants — Google Sheets tab names as they appear in SheetCache.json
        // ─────────────────────────────────────────────────────────────────

        internal static class SheetKey
        {
            public const string Buildings     = "Buildings & Production";
            public const string Workers       = "Workers & Entities";
            public const string Environment   = "Environment & Loot";
            public const string DrawButton    = "DrawButton";
            public const string POI           = "PointsOfInterest";
            public const string PlacementCosts = "Placement Costs";
        }

        // ─────────────────────────────────────────────────────────────────
        // Column Name Constants — exact header strings as they appear in each sheet.
        // Fields shared across multiple sheets use the same constant where names match.
        // ─────────────────────────────────────────────────────────────────

        internal static class Col
        {
            // Shared
            public const string Active            = "Active";
            public const string HP                = "HP";
            public const string KillerBehavior    = "Killer's Behavior";
            public const string AllyInteractible  = "Ally Interactible";
            public const string EnemyInteractible = "Enemy Interactible";
            public const string WildInteractible  = "Wild Animal Interactible";
            public const string DropOnDeath       = "Drop on Death";
            public const string Drops             = "Drops";
            public const string LootPerHit        = "Loot per Hit";
            public const string MapGenerated      = "MapGenerated";
            public const string Type              = "Type";
            public const string TierButton        = "Tier (button)";

            // Buildings & Production
            public const string Building              = "Building";
            public const string ProdInterval          = "Prod. Interval (s)";
            public const string IntervalBonus         = "Interval Bonus (s)";
            public const string InputCard             = "Input Card";
            public const string OutputCard            = "Output Card";
            public const string RevealRadius          = "Reveal Radius";
            public const string Attack                = "Attack";
            public const string DrawWeightBuilding    = "DrawWeight";        // No space — Buildings sheet
            public const string IsRandomBuilding      = "isRandomBuilding";
            public const string ResourceUse           = "Resource Use";
            public const string ResourceAmount        = "Resource Amount";
            public const string ResourceIncrement     = "Resource Increment";
            public const string BuildOn               = "BuildOn";

            // Workers & Entities
            public const string Entity            = "Entity";
            public const string MovementBehavior  = "Movement Behavior";
            public const string AttackBehavior    = "Attack Behavior";
            public const string DrawWeightWorker  = "Draw Weight";          // With space — Workers sheet
            public const string AttackPower       = "Attack Power";
            public const string Walkable          = "Walkable";

            // Environment & Loot
            public const string Object            = "Object";
            public const string TotalYield        = "Total Yield";

            // DrawButton
            public const string DrawButtonOrder   = "Draw Button Order";
            public const string DrawButtonOutput  = "Output";
            public const string CostType          = "Cost Type";
            public const string CostAmount        = "Cost Amount";
            public const string Cooldown          = "Cooldown (s)";

            // PointsOfInterest
            public const string POIObject         = "Object";
            public const string Grouping          = "Grouping";
            public const string QuantityMinimum   = "Quantity Minimum";
            public const string Name              = "Name";
            public const string Color             = "Color";
            public const string RewardType        = "Reward Type";
            public const string RewardQuantity    = "Reward Quantity";

            // Placement Costs
            public const string Item              = "Item";
            public const string PlacementTier     = "Tier";
            public const string PlacementCount    = "#";
            public const string Currency1         = "Currency 1";
            public const string Cost1             = "Cost 1";
            public const string Currency2         = "Currency 2";
            public const string Cost2             = "Cost 2";
            public const string Currency3         = "Currency 3";
            public const string Cost3             = "Cost 3";
        }

        // ─────────────────────────────────────────────────────────────────
        // Cache Column Validation
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks that every column key this editor reads by name is present in the
        /// corresponding cached sheet. Returns one error string per missing column.
        /// If the cache or a sheet is absent the check is skipped for that sheet
        /// (the missing-sheet warning is handled elsewhere).
        /// </summary>
        private List<string> ValidateCacheColumns(SheetCacheData data)
        {
            var errors = new List<string>();
            if (data?.sheets == null) return errors;

            void Check(string sheetKey, params string[] required)
            {
                if (!data.sheets.TryGetValue(sheetKey, out var sheet)) return;
                var headers = new System.Collections.Generic.HashSet<string>(sheet.headers);
                foreach (var col in required)
                    if (!headers.Contains(col))
                        errors.Add($"  [{sheetKey}] missing column: \"{col}\"");
            }

            Check(SheetKey.Buildings,
                Col.Building, Col.Active, Col.ProdInterval, Col.IntervalBonus,
                Col.InputCard, Col.OutputCard, Col.RevealRadius, Col.HP, Col.Attack,
                Col.AllyInteractible, Col.EnemyInteractible, Col.WildInteractible,
                Col.KillerBehavior, Col.TierButton, Col.DrawWeightBuilding, Col.IsRandomBuilding,
                Col.ResourceUse, Col.ResourceAmount, Col.ResourceIncrement, Col.BuildOn);

            Check(SheetKey.Workers,
                Col.Entity, Col.Type, Col.Active, Col.HP, Col.AttackPower,
                Col.MovementBehavior, Col.DrawWeightWorker, Col.KillerBehavior, Col.TierButton, Col.Walkable);

            Check(SheetKey.Environment,
                Col.Object, Col.Active, Col.MapGenerated, Col.Type, Col.Drops, Col.LootPerHit,
                Col.HP, Col.TotalYield, Col.AllyInteractible, Col.EnemyInteractible,
                Col.WildInteractible, Col.KillerBehavior, Col.DropOnDeath);

            Check(SheetKey.DrawButton,
                Col.DrawButtonOrder, Col.DrawButtonOutput, Col.CostType, Col.CostAmount, Col.Cooldown);

            Check(SheetKey.POI,
                Col.Active, Col.Object, Col.Grouping, Col.QuantityMinimum,
                Col.Name, Col.Color, Col.RewardType, Col.RewardQuantity);

            Check(SheetKey.PlacementCosts,
                Col.Item, Col.PlacementTier, Col.PlacementCount,
                Col.Currency1, Col.Cost1,
                Col.Currency2, Col.Cost2,
                Col.Currency3, Col.Cost3);

            return errors;
        }

        // ─────────────────────────────────────────────────────────────────
        // JSON Parsing
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Manual JSON parsing because JsonUtility can't handle Dictionary or heterogeneous structures.
        /// Delegates to MiniJSON for the heavy lifting, then maps to strongly-typed data classes.
        /// </summary>
        private SheetCacheData ParseCacheJson(string json)
        {
            var data = new SheetCacheData();

            var parsed = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (parsed == null) return null;

            data.lastSynced    = parsed.ContainsKey("lastSynced")    ? parsed["lastSynced"] as string    : "";
            data.spreadsheetId = parsed.ContainsKey("spreadsheetId") ? parsed["spreadsheetId"] as string : "";
            data.sheets = new Dictionary<string, SheetData>();

            if (parsed.ContainsKey("sheets") && parsed["sheets"] is Dictionary<string, object> sheets)
            {
                foreach (var kvp in sheets)
                {
                    if (kvp.Value is Dictionary<string, object> sheetDict)
                    {
                        var sheetData = new SheetData
                        {
                            headers = new List<string>(),
                            rows    = new List<Dictionary<string, string>>()
                        };

                        if (sheetDict.ContainsKey("headers") && sheetDict["headers"] is List<object> headers)
                            foreach (var h in headers)
                                sheetData.headers.Add(h?.ToString() ?? "");

                        if (sheetDict.ContainsKey("rows") && sheetDict["rows"] is List<object> rows)
                        {
                            foreach (var rowObj in rows)
                            {
                                if (rowObj is Dictionary<string, object> rowDict)
                                {
                                    var row = new Dictionary<string, string>();
                                    foreach (var cell in rowDict)
                                        row[cell.Key] = cell.Value?.ToString() ?? "";
                                    sheetData.rows.Add(row);
                                }
                            }
                        }

                        data.sheets[kvp.Key] = sheetData;
                    }
                }
            }

            return data;
        }

        // ─────────────────────────────────────────────────────────────────
        // Parse Helpers
        // ─────────────────────────────────────────────────────────────────

        private string GetValue(Dictionary<string, string> row, string key)
            => row.ContainsKey(key) ? row[key] : "";

        /// <summary>
        /// Strips emoji prefix from values like "💰 Gold" → "Gold", "👷 Worker" → "Worker".
        /// Handles surrogate pairs and multi-byte emoji. Returns original string if no emoji found.
        /// </summary>
        private string StripEmoji(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    return s.Substring(i);
            }
            return s;
        }

        private bool TrySetInt(ref int field, string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (int.TryParse(value, out int v) && field != v) { field = v; return true; }
            return false;
        }

        private bool TrySetFloat(ref float field, string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v)
                && Math.Abs(field - v) > 0.001f)
            { field = v; return true; }
            return false;
        }

        private ProductionInputType ParseInputType(string s)
        {
            string clean = StripEmoji(s);
            if (string.IsNullOrEmpty(clean) || clean == "None") return ProductionInputType.None;
            if (clean.StartsWith("Worker",   StringComparison.OrdinalIgnoreCase)) return ProductionInputType.Worker;
            if (clean.Equals("Hold to Fill", StringComparison.OrdinalIgnoreCase) ||
                clean.Equals("HoldToFill",   StringComparison.OrdinalIgnoreCase)) return ProductionInputType.HoldToFill;
            if (clean.StartsWith("Any",      StringComparison.OrdinalIgnoreCase)) return ProductionInputType.Any;
            if (Enum.TryParse<ProductionInputType>(clean, true, out var result)) return result;
            return ProductionInputType.None;
        }

        private ProductionOutputType ParseOutputType(string s)
        {
            string clean = StripEmoji(s);
            if (string.IsNullOrEmpty(clean) || clean == "None") return ProductionOutputType.None;
            if (clean.StartsWith("Worker",    StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.Worker;
            if (clean.Equals("Tree",          StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.TreeSeed;
            if (clean.Equals("Feast",         StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.Meal;
            if (clean.Equals("Lizard",        StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.Lizard;
            if (clean.Equals("Tree Seed",     StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.TreeSeed;
            if (Enum.TryParse<ProductionOutputType>(clean, true, out var result)) return result;
            return ProductionOutputType.None;
        }
    }
}
