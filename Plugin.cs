using BepInEx;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class TrashMod : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.batchscrapping";
    public const string PluginName = "BatchScrapping";
    public const string PluginVersion = "1.0.1";

    private Harmony harmony;
    private float holdTimer = 0f;
    private bool isScrapping = false;
    private const float HOLD_DURATION = 1.0f;
    private const int BATCH_SIZE = 10;
    private const float BATCH_INTERVAL = 0.1f;

    void Awake()
    {
        var harmony = new Harmony(PluginGUID);
        harmony.PatchAll(typeof(Patches));
        Logger.LogInfo($"{PluginName} loaded successfully.");
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
                holdTimer = 0f;
            }
        }
        else
        {
            holdTimer = 0f;
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
            yield return new WaitForSeconds(BATCH_INTERVAL);
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
        return (flags & 0x01) != 0;
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