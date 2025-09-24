using BepInEx;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Pigeon.Movement;
using Pigeon;

[BepInPlugin("com.yourname.mycopunk.batchscrapping", "BatchScrapping", "1.0.0")]
public class TrashMod : BaseUnityPlugin
{
    private Harmony harmony;
    private float holdTimer = 0f;
    private bool isScrapping = false;
    private const float HOLD_DURATION = 1.0f; // Seconds to hold "T"
    private const int BATCH_SIZE = 10; // Upgrades per batch
    private const float BATCH_INTERVAL = 0.1f; // Seconds per batch (10 upgrades per second)

    void Awake()
    {
        var harmony = new Harmony("com.yourname.mycopunk.batchscrapping");
        harmony.PatchAll(typeof(Patches));
        Logger.LogInfo($"{harmony.Id} loaded!");
    }

    void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }

    void Update()
    {
        if (isScrapping || Keyboard.current == null) return;

        if (Keyboard.current.tKey.isPressed)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= HOLD_DURATION)
            {
                StartCoroutine(ScrapNonFavoredUpgrades());
                holdTimer = 0f; // Reset timer after triggering
            }
        }
        else
        {
            holdTimer = 0f; // Reset timer if key is released
        }
    }

    private IEnumerator ScrapNonFavoredUpgrades()
    {
        if (isScrapping)
        {
            Logger.LogWarning("[TrashMod] Scrapping already in progress; skipping.");
            yield break;
        }

        isScrapping = true;

        var gearWindow = FindObjectOfType<GearDetailsWindow>();
        if (gearWindow == null)
        {
            Logger.LogWarning("[TrashMod] GearDetailsWindow is null; cannot scrap upgrades.");
            isScrapping = false;
            yield break;
        }

        var gear = gearWindow.UpgradablePrefab;
        if (gear == null)
        {
            Logger.LogWarning("[TrashMod] UpgradablePrefab is null; cannot scrap upgrades.");
            isScrapping = false;
            yield break;
        }

        List<UpgradeInstance> toScrap = new List<UpgradeInstance>();
        foreach (var info in PlayerData.GetAllUpgrades(gear))
        {
            if (info?.Instances == null) continue;
            toScrap.AddRange(info.Instances.Where(inst => inst != null && !IsFavorite(inst)));
        }

        if (toScrap.Count == 0)
        {
            Logger.LogInfo("[TrashMod] No non-favored upgrades to scrap for current gear.");
            isScrapping = false;
            yield break;
        }

        int scrappedCount = 0;
        for (int i = 0; i < toScrap.Count; i += BATCH_SIZE)
        {
            int batchEnd = Mathf.Min(i + BATCH_SIZE, toScrap.Count);
            Logger.LogInfo($"[TrashMod] Processing batch {i / BATCH_SIZE + 1} (upgrades {i} to {batchEnd - 1})");
            for (int j = i; j < batchEnd; j++)
            {
                var inst = toScrap[j];
                if (inst == null || inst.Upgrade == null)
                {
                    Logger.LogWarning($"[TrashMod] Skipping invalid upgrade at index {j}: instance or Upgrade is null.");
                    continue;
                }

                try
                {
                    inst.Upgrade.GiveDismantleResources();
                    inst.Destroy();
                    scrappedCount++;
                }
                catch (System.Exception e)
                {
                    Logger.LogError($"[TrashMod] Failed to scrap upgrade at index {j}: {e.Message}");
                }
            }
            yield return new WaitForSeconds(BATCH_INTERVAL); // Wait 0.1s per batch
        }

        Logger.LogInfo($"[TrashMod] Scrapped {scrappedCount} non-favored upgrades.");
        RefreshOpenWindows();
        isScrapping = false;
    }

    private static bool IsFavorite(UpgradeInstance instance)
    {
        if (instance == null) return false;
        var flagsField = AccessTools.Field(typeof(UpgradeInstance), "flags");
        byte flags = (byte)flagsField.GetValue(instance);
        return (flags & 0x01) != 0; // Assume 0x01 is the favorite flag
    }

    private static void RefreshOpenWindows()
    {
        if (Menu.Instance != null && Menu.Instance.IsOpen)
        {
            var top = Menu.Instance.WindowSystem.GetTop();
            if (top != null)
            {
                top.OnOpen(Menu.Instance.WindowSystem);
            }
        }
    }
}