using BepInEx;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Reflection;
using System;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class TrashMod : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.batchscrapping";
    public const string PluginName = "BatchScrapping";
    public const string PluginVersion = "1.1.0";

    private Harmony harmony;
    private bool isScrapping = false;
    public static TrashMod Instance;
    private const float HOLD_DURATION = 1.0f;
    private const int BATCH_SIZE = 100;
    private const float BATCH_INTERVAL = 0f;

    void Awake()
    {
        var harmony = new Harmony(PluginGUID);
        harmony.PatchAll(typeof(Patches));
        Instance = this;
        base.Logger.LogInfo($"{PluginName} loaded successfully - patches applied.");
    }

    void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }

    public IEnumerator ScrapMarkedUpgrades()
    {
        if (isScrapping)
        {
            yield break;
        }

        isScrapping = true;

        var gearWindow = UnityEngine.Object.FindObjectOfType<GearDetailsWindow>();
        if (gearWindow == null)
        {
            isScrapping = false;
            yield break;
        }

        var gear = gearWindow.UpgradablePrefab;
        if (gear == null)
        {
            isScrapping = false;
            yield break;
        }

        List<UpgradeInstance> toScrap = new List<UpgradeInstance>();
        var inSkinModeField = AccessTools.Field(typeof(GearDetailsWindow), "inSkinMode");
        bool inSkinMode = (bool)inSkinModeField.GetValue(gearWindow);
        var all = inSkinMode ? PlayerData.GetAllSkins(gear) : PlayerData.GetAllUpgrades(gear);
        foreach (var info in all)
        {
            if (info?.Instances == null) continue;
            toScrap.AddRange(info.Instances.Where(inst => inst != null && IsTrashMarked(inst)));
        }

        if (toScrap.Count == 0)
        {
            isScrapping = false;
            yield break;
        }

        int scrappedCount = 0;
        var totalResources = new System.Collections.Generic.Dictionary<PlayerResource, int>();
        for (int i = 0; i < toScrap.Count; i += BATCH_SIZE)
        {
            int batchNumber = i / BATCH_SIZE + 1;
            int batchEnd = Mathf.Min(i + BATCH_SIZE, toScrap.Count);
            for (int j = i; j < batchEnd; j++)
            {
                var inst = toScrap[j];
                if (inst == null || inst.Upgrade == null)
                {
                    continue;
                }

                    ref RarityData rarity = ref Global.GetRarity(inst.Upgrade.Rarity);
                    totalResources.TryGetValue(rarity.scrapResource, out int existingScrap);
                    totalResources[rarity.scrapResource] = existingScrap + 2;
                    int scrip = rarity.upgradeScripCost / 6;
                    totalResources.TryGetValue(Global.Instance.ScripResource, out int existingScrip);
                    totalResources[Global.Instance.ScripResource] = existingScrip + scrip;
                    inst.Destroy();
                    scrappedCount++;
            }
            if (BATCH_INTERVAL > 0f)
            {
                yield return new WaitForSeconds(BATCH_INTERVAL);
            }
        }
        foreach (var kvp in totalResources)
        {
            PlayerData.Instance.AddResource(kvp.Key, kvp.Value);
        }

        RefreshOpenWindows();
        isScrapping = false;
    }

    public static bool IsFavorite(UpgradeInstance instance)
    {
        if (instance == null) return false;
        var flagsField = AccessTools.Field(typeof(UpgradeInstance), "flags");
        byte flags = (byte)flagsField.GetValue(instance);
        return (flags & 0x01) != 0;
    }

    public static bool IsTrashMarked(UpgradeInstance instance)
    {
        if (instance == null) return false;
        var flagsField = AccessTools.Field(typeof(UpgradeInstance), "flags");
        byte flags = (byte)flagsField.GetValue(instance);
        return (flags & 0x02) != 0;
    }

    public static void SetTrashMark(UpgradeInstance instance, bool marked)
    {
        if (instance == null) return;
        var flagsField = AccessTools.Field(typeof(UpgradeInstance), "flags");
        byte flags = (byte)flagsField.GetValue(instance);
        if (marked)
        {
            flags |= 0x02;
            flags &= 0xFE;
        }
        else
        {
            flags &= 0xFD;
        }
        flagsField.SetValue(instance, flags);
    }

    public static void SetFavorite(UpgradeInstance instance, bool favorite)
    {
        if (instance == null) return;
        var flagsField = AccessTools.Field(typeof(UpgradeInstance), "flags");
        byte flags = (byte)flagsField.GetValue(instance);
        if (favorite)
        {
            flags |= 0x01;
            flags &= 0xFD;
        }
        else
        {
            flags &= 0xFE;
        }
        flagsField.SetValue(instance, flags);
    }

    public void TryScrapMarkedUpgrades()
    {
        var gearWindow = UnityEngine.Object.FindObjectOfType<GearDetailsWindow>();
        if (gearWindow == null) return;
        var gear = gearWindow.UpgradablePrefab;
        if (gear == null) return;
        var inSkinModeField = AccessTools.Field(typeof(GearDetailsWindow), "inSkinMode");
        bool inSkinMode = (bool)inSkinModeField.GetValue(gearWindow);
        var all = inSkinMode ? PlayerData.GetAllSkins(gear) : PlayerData.GetAllUpgrades(gear);
        bool hasMarked = false;
        foreach (var info in all)
        {
            if (info?.Instances == null) continue;
            if (info.Instances.Any(inst => inst != null && TrashMod.IsTrashMarked(inst)))
            {
                hasMarked = true;
                break;
            }
        }
        if (hasMarked)
        {
            StartCoroutine(ScrapMarkedUpgrades());
        }
        else
        {
            GameManager.Instance.ShowInfo("No upgrades marked for scrapping.", null, null, null, Color.white, Rarity.None, playSounds: false);
        }
    }

    public IEnumerator ScrapNonFavoriteUpgrades()
    {
        if (isScrapping)
        {
            yield break;
        }

        isScrapping = true;

        var gearWindow = UnityEngine.Object.FindObjectOfType<GearDetailsWindow>();
        if (gearWindow == null)
        {
            isScrapping = false;
            yield break;
        }

        var gear = gearWindow.UpgradablePrefab;
        if (gear == null)
        {
            isScrapping = false;
            yield break;
        }

        List<UpgradeInstance> toScrap = new List<UpgradeInstance>();
        var inSkinModeField = AccessTools.Field(typeof(GearDetailsWindow), "inSkinMode");
        bool inSkinMode = (bool)inSkinModeField.GetValue(gearWindow);
        var all = inSkinMode ? PlayerData.GetAllSkins(gear) : PlayerData.GetAllUpgrades(gear);
        foreach (var info in all)
        {
            if (info?.Instances == null) continue;
            toScrap.AddRange(info.Instances.Where(inst => inst != null && !IsFavorite(inst)));
        }

        if (toScrap.Count == 0)
        {
            isScrapping = false;
            yield break;
        }

        int scrappedCount = 0;
        var totalResources = new System.Collections.Generic.Dictionary<PlayerResource, int>();
        for (int i = 0; i < toScrap.Count; i += BATCH_SIZE)
        {
            int batchNumber = i / BATCH_SIZE + 1;
            int batchEnd = Mathf.Min(i + BATCH_SIZE, toScrap.Count);
            for (int j = i; j < batchEnd; j++)
            {
                var inst = toScrap[j];
                if (inst == null || inst.Upgrade == null)
                {
                    continue;
                }

                    ref RarityData rarity = ref Global.GetRarity(inst.Upgrade.Rarity);
                    totalResources.TryGetValue(rarity.scrapResource, out int existingScrap);
                    totalResources[rarity.scrapResource] = existingScrap + 2;
                    int scrip = rarity.upgradeScripCost / 6;
                    totalResources.TryGetValue(Global.Instance.ScripResource, out int existingScrip);
                    totalResources[Global.Instance.ScripResource] = existingScrip + scrip;
                    inst.Destroy();
                    scrappedCount++;
            }
            if (BATCH_INTERVAL > 0f)
            {
                yield return new WaitForSeconds(BATCH_INTERVAL);
            }
        }
        foreach (var kvp in totalResources)
        {
            PlayerData.Instance.AddResource(kvp.Key, kvp.Value);
        }

        RefreshOpenWindows();
        isScrapping = false;
    }

    public void TryScrapNonFavoriteUpgrades()
    {
        var gearWindow = UnityEngine.Object.FindObjectOfType<GearDetailsWindow>();
        if (gearWindow == null) return;
        var gear = gearWindow.UpgradablePrefab;
        if (gear == null) return;
        var inSkinModeField = AccessTools.Field(typeof(GearDetailsWindow), "inSkinMode");
        bool inSkinMode = (bool)inSkinModeField.GetValue(gearWindow);
        var all = inSkinMode ? PlayerData.GetAllSkins(gear) : PlayerData.GetAllUpgrades(gear);
        bool hasNonFavorite = false;
        foreach (var info in all)
        {
            if (info?.Instances == null) continue;
            if (info.Instances.Any(inst => inst != null && !TrashMod.IsFavorite(inst)))
            {
                hasNonFavorite = true;
                break;
            }
        }
        if (hasNonFavorite)
        {
            StartCoroutine(ScrapNonFavoriteUpgrades());
        }
        else
        {
            GameManager.Instance.ShowInfo("No non-favorite upgrades to scrap.", null, null, null, Color.white, Rarity.None, playSounds: false);
        }
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

public class Patches
{
    [HarmonyPatch(typeof(GearDetailsWindow), "Setup")]
    [HarmonyPostfix]
    private static void SetupPostfix(GearDetailsWindow __instance, IUpgradable upgradable)
    {
        AddScrapButton(__instance);
    }

    private static void AddScrapButton(GearDetailsWindow window)
    {
        var oldButtonMarked = window.transform.Find("ModScrapButtonMarked");
        if (oldButtonMarked != null)
        {
            UnityEngine.Object.DestroyImmediate(oldButtonMarked.gameObject);
        }
        var oldButtonNonFavorite = window.transform.Find("ModScrapButtonNonFavorite");
        if (oldButtonNonFavorite != null)
        {
            UnityEngine.Object.DestroyImmediate(oldButtonNonFavorite.gameObject);
        }

        var parentRect = window.transform.GetComponent<RectTransform>();

        var markedButtonGO = new GameObject("ModScrapButtonMarked");
        markedButtonGO.transform.SetParent(window.transform, false);
        var rectMarked = markedButtonGO.AddComponent<RectTransform>();
        rectMarked.sizeDelta = new Vector2(200, 50);
        rectMarked.anchorMin = new Vector2(1, 0);
        rectMarked.anchorMax = new Vector2(1, 0);
        rectMarked.pivot = new Vector2(1, 0);
        rectMarked.anchoredPosition = new Vector2(-parentRect.rect.width * 0.25f, 10);

        var imageMarked = markedButtonGO.AddComponent<Image>();
        imageMarked.color = Color.gray;
        imageMarked.raycastTarget = true;

        var buttonMarked = markedButtonGO.AddComponent<Button>();
        buttonMarked.transition = Button.Transition.ColorTint;
        var colorsMarked = buttonMarked.colors;
        colorsMarked.normalColor = Color.gray;
        colorsMarked.highlightedColor = Color.white;
        colorsMarked.pressedColor = Color.blue;
        buttonMarked.colors = colorsMarked;

        var textChildMarked = new GameObject("Text");
        textChildMarked.transform.SetParent(markedButtonGO.transform, false);
        var textRectMarked = textChildMarked.AddComponent<RectTransform>();
        textRectMarked.sizeDelta = rectMarked.sizeDelta;
        var tmproMarked = textChildMarked.AddComponent<TMPro.TextMeshProUGUI>();
        tmproMarked.text = "Scrap Marked";
        tmproMarked.alignment = TMPro.TextAlignmentOptions.Center;
        tmproMarked.color = Color.white;
        tmproMarked.fontSize = 24;

        var handlerMarked = markedButtonGO.AddComponent<HoldButtonHandler>();
        handlerMarked.onHoldComplete = () => {
            TrashMod.Instance?.TryScrapMarkedUpgrades();
        };

        var nonFavButtonGO = new GameObject("ModScrapButtonNonFavorite");
        nonFavButtonGO.transform.SetParent(window.transform, false);
        var rectNonFav = nonFavButtonGO.AddComponent<RectTransform>();
        rectNonFav.sizeDelta = new Vector2(250, 50);
        rectNonFav.anchorMin = new Vector2(1, 0);
        rectNonFav.anchorMax = new Vector2(1, 0);
        rectNonFav.pivot = new Vector2(1, 0);
        rectNonFav.anchoredPosition = new Vector2(rectMarked.anchoredPosition.x + 210, 50);

        var imageNonFav = nonFavButtonGO.AddComponent<Image>();
        imageNonFav.color = Color.red;
        imageNonFav.raycastTarget = true;

        var buttonNonFav = nonFavButtonGO.AddComponent<Button>();
        buttonNonFav.transition = Button.Transition.ColorTint;
        var colorsNonFav = buttonNonFav.colors;
        colorsNonFav.normalColor = Color.red;
        colorsNonFav.highlightedColor = Color.white;
        colorsNonFav.pressedColor = Color.blue;
        buttonNonFav.colors = colorsNonFav;

        var textChildNonFav = new GameObject("Text");
        textChildNonFav.transform.SetParent(nonFavButtonGO.transform, false);
        var textRectNonFav = textChildNonFav.AddComponent<RectTransform>();
        textRectNonFav.sizeDelta = rectNonFav.sizeDelta;
        var tmproNonFav = textChildNonFav.AddComponent<TMPro.TextMeshProUGUI>();
        tmproNonFav.text = "Scrap All Non-Favorite";
        tmproNonFav.alignment = TMPro.TextAlignmentOptions.Center;
        tmproNonFav.color = Color.white;
        tmproNonFav.fontSize = 20;

        var handlerNonFav = nonFavButtonGO.AddComponent<HoldButtonHandler>();
        handlerNonFav.onHoldComplete = () => {
            TrashMod.Instance?.TryScrapNonFavoriteUpgrades();
        };

    }

    [HarmonyPatch(typeof(GearUpgradeUI), "UpdateFavoriteIcon")]
    [HarmonyPostfix]
    private static void UpdateFavoriteIconPostfix(GearUpgradeUI __instance)
    {
        var favoriteIconField = AccessTools.Field(typeof(GearUpgradeUI), "favoriteIcon");
        var favoriteIcon = (Image)favoriteIconField.GetValue(__instance);
        bool favorite = TrashMod.IsFavorite(__instance.Upgrade);
        bool trash = TrashMod.IsTrashMarked(__instance.Upgrade);
        if (favorite || trash)
        {
            favoriteIcon.gameObject.SetActive(true);
            favoriteIcon.color = favorite ? Color.white : Color.red;
        }
        else
        {
            favoriteIcon.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(GearUpgradeUI), "OnAdditionalAction")]
    [HarmonyPostfix]
    private static void OnAdditionalActionPostfix(GearUpgradeUI __instance, int index, ref bool refreshUI)
    {
        if (index == 0) {
            TrashMod.SetFavorite(__instance.Upgrade, __instance.Upgrade.Favorite);
        }
    }

    [HarmonyPatch(typeof(GearUpgradeUI), "EnableGridView")]
    [HarmonyPostfix]
    private static void EnableGridViewPostfix(GearUpgradeUI __instance, bool grid)
    {
        var favoriteIconField = AccessTools.Field(typeof(GearUpgradeUI), "favoriteIcon");
        var favoriteIcon = (Image)favoriteIconField.GetValue(__instance);
        if (favoriteIcon.gameObject.activeSelf)
        {
            favoriteIcon.color = TrashMod.IsFavorite(__instance.Upgrade) ? Color.white : Color.red;
        }
    }

    [HarmonyPatch(typeof(GearDetailsWindow), "Update")]
    [HarmonyPrefix]
    private static void UpdatePrefix(GearDetailsWindow __instance)
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            GearUpgradeUI hoveredUI = null;
            if (UIRaycaster.RaycastForComponent<GearUpgradeUI>(out hoveredUI))
            {
                var upgrade = hoveredUI.Upgrade;
                if (upgrade != null && !TrashMod.IsFavorite(upgrade))
                {
                    bool wasMarked = TrashMod.IsTrashMarked(upgrade);
                    TrashMod.SetTrashMark(upgrade, !wasMarked);
                    var updateMethod = AccessTools.Method(typeof(GearUpgradeUI), "UpdateFavoriteIcon");
                    updateMethod.Invoke(hoveredUI, new object[0]);
                }
            }
        }
    }
}

public class HoldButtonHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private bool isHolding = false;
    private float holdTimer = 0f;
    private const float HOLD_DURATION = 1.0f;

    public System.Action onHoldComplete;

    public void OnPointerDown(PointerEventData eventData)
    {
        isHolding = true;
        holdTimer = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isHolding = false;
    }

    void Update()
    {
        if (isHolding)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= HOLD_DURATION)
            {
                onHoldComplete?.Invoke();
                isHolding = false;
            }
        }
    }
}
