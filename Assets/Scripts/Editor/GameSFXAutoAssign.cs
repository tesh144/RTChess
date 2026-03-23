#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using ClockworkGrid;

/// <summary>
/// Editor tool that auto-assigns recommended AudioClips from the InterfaceSounds pack
/// to the GameSFXManager. Run via Tools > ClockworkCraft > Auto-Assign SFX.
/// </summary>
public class GameSFXAutoAssign : Editor
{
    [MenuItem("Tools/ClockworkCraft/Auto-Assign SFX")]
    static void AutoAssignSFX()
    {
        GameSFXManager sfx = FindFirstObjectByType<GameSFXManager>();
        if (sfx == null)
        {
            Debug.LogError("[SFX Auto-Assign] No GameSFXManager found in scene!");
            return;
        }

        int assigned = 0;
        SerializedObject so = new SerializedObject(sfx);

        // Placement — purchase thud (distinct from coin collect jingle)
        assigned += TryAssign(so, "placementDrop", "ThirdParty/InterfaceSounds/V1.0/Interface/Item Purchase (1)");
        assigned += TryAssign(so, "placementError", "ThirdParty/InterfaceSounds/V1.0/Interface/Buzz Error (1)");
        assigned += TryAssign(so, "objectRemove", "ThirdParty/InterfaceSounds/V1.0/Interface/Pops/Pop (6)");

        // UI & Cards — distinct mechanical/UI sounds
        assigned += TryAssign(so, "buttonClick", "ThirdParty/InterfaceSounds/V1.0/Interface/Clicks/Abstract Click (2)");
        // cardDraw + cardSlideIn — silent for now, too attention-grabbing
        TryClear(so, "cardDraw");
        TryClear(so, "cardSlideIn");
        assigned += TryAssign(so, "dragStart", "ThirdParty/InterfaceSounds/V1.0/Interface/Switches_Buttons/Switch (2)");
        assigned += TryAssign(so, "dragCancel", "ThirdParty/InterfaceSounds/V1.0/Interface/Clicks/Click Back (3)");
        assigned += TryAssign(so, "errorBuzz", "ThirdParty/InterfaceSounds/V1.0/Interface/Buzz Error (4)");
        assigned += TryAssign(so, "successChime", "ThirdParty/InterfaceSounds/V1.0/Items & Collectables/Special & Powerup (12)");
        assigned += TryAssign(so, "drawReady", "ThirdParty/InterfaceSounds/V1.0/Interface/Special (3)");

        // Combat — sword/impact sounds for attacks, distinct death sound
        assigned += TryAssign(so, "hitImpact", "Audio/SFX/sfx_sword_slash");
        assigned += TryAssign(so, "hitWeak", "ThirdParty/InterfaceSounds/V1.0/Interface/Pops/Pop (1)");
        assigned += TryAssign(so, "entityDeath", "ThirdParty/InterfaceSounds/V1.0/Interface/Pops/Special Pop (7)");
        assigned += TryAssign(so, "damageImpact", "Audio/SFX/sfx_sword_rock");

        // Resources & Loot — coin jingles and sparkly collection sounds
        assigned += TryAssign(so, "coinCollect", "ThirdParty/InterfaceSounds/V1.0/Items & Collectables/Coins (5)");
        assigned += TryAssign(so, "lootBurst", "ThirdParty/InterfaceSounds/V1.0/Interface/Pops/Pop (4)");
        assigned += TryAssign(so, "lootArrival", "ThirdParty/InterfaceSounds/V1.0/Items & Collectables/Coins (12)");
        assigned += TryAssign(so, "resourceDepleted", "ThirdParty/InterfaceSounds/V1.0/Items & Collectables/Crunch Bite Item (3)");

        // Production — light tick for timer done, popup silent, star chime for collect
        assigned += TryAssign(so, "timerComplete", "ThirdParty/InterfaceSounds/V1.0/Interface/Clicks/Abstract Click (4)");
        TryClear(so, "popupAppear"); // silent — the visual popup is enough feedback
        assigned += TryAssign(so, "rewardCollect", "ThirdParty/InterfaceSounds/V1.0/Items & Collectables/Star Collect");
        assigned += TryAssign(so, "handFull", "ThirdParty/InterfaceSounds/V1.0/Interface/Buzz Error (2)");

        // Discovery — whoosh for fog reveal
        assigned += TryAssign(so, "fogReveal", "ThirdParty/InterfaceSounds/V2.0 Files/quick transitions (1)");

        // Ambient — subtle tick
        assigned += TryAssign(so, "clockTick", "ThirdParty/InterfaceSounds/V1.0/Items & Collectables/Count Prize (Single Tick)");

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(sfx);
        Debug.Log($"[SFX Auto-Assign] Done — {assigned} clips assigned to GameSFXManager.");
    }

    static int TryAssign(SerializedObject so, string propertyName, string assetPath)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogWarning($"[SFX Auto-Assign] Property '{propertyName}' not found on GameSFXManager.");
            return 0;
        }

        // Overwrite with recommended clip (re-running the tool updates all slots)

        // Try .wav first, then .ogg, then .mp3
        string[] extensions = { ".wav", ".ogg", ".mp3" };
        foreach (string ext in extensions)
        {
            string fullPath = "Assets/" + assetPath + ext;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(fullPath);
            if (clip != null)
            {
                prop.objectReferenceValue = clip;
                Debug.Log($"  Assigned {propertyName} ← {fullPath}");
                return 1;
            }
        }

        Debug.LogWarning($"[SFX Auto-Assign] Could not find audio at 'Assets/{assetPath}' (.wav/.ogg/.mp3)");
        return 0;
    }

    /// <summary>
    /// Clear an AudioClip slot (set to null). Used when a sound should be intentionally silent.
    /// </summary>
    static void TryClear(SerializedObject so, string propertyName)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.objectReferenceValue = null;
            Debug.Log($"  Cleared {propertyName} (intentionally silent)");
        }
    }
}
