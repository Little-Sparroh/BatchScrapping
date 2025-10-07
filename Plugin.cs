using BepInEx;
using HarmonyLib;
using Pigeon.Math;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CraniusMissionMod
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class CraniusMissionMod : BaseUnityPlugin
    {
        public const string PluginGUID = "sparroh.craniushunt";
        public const string PluginName = "CraniusHunt";
        public const string PluginVersion = "1.0.2";

        private void Awake()
        {
            var harmony = new Harmony(PluginGUID);
            harmony.PatchAll(typeof(MissionSelectWindowInitPatch));
            harmony.PatchAll(typeof(AmalgamationObjectivePatch));
            harmony.PatchAll(typeof(MissionSelectWindowPatch));
            Logger.LogInfo($"{PluginName} loaded successfully.");
        }
    }

    public class CraniusMission : AmalgamationMission
    {
        public override bool CanShowInMissionSelect(out bool showLocked, out string lockedMessage)
        {
            showLocked = false;
            lockedMessage = null;
            return true;
        }
    }

    [HarmonyPatch(typeof(MissionSelectWindow), "Awake")]
    public class MissionSelectWindowInitPatch
    {
        public static CraniusMission craniusMission;
        public static EnemyClass craniusClass;

        [HarmonyPostfix]
        public static void Postfix(MissionSelectWindow __instance)
        {
            AmalgamationMission original = Resources.FindObjectsOfTypeAll<AmalgamationMission>()[0];
            if (original == null)
            {
                return;
            }

            craniusMission = ScriptableObject.CreateInstance<CraniusMission>();

            System.Type missionType = typeof(AmalgamationMission);
            FieldInfo[] missionFields = missionType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in missionFields)
            {
                field.SetValue(craniusMission, field.GetValue(original));
            }

            FieldInfo nameField = typeof(Mission).GetField("_missionName", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nameField != null)
            {
                nameField.SetValue(craniusMission, "cranius_hunt");
            }

            FieldInfo descriptionField = typeof(Mission).GetField("_description", BindingFlags.NonPublic | BindingFlags.Instance);
            if (descriptionField != null)
            {
                descriptionField.SetValue(craniusMission, "Hunt down the secret boss Cranius!");
            }

            craniusMission.MissionIcon = original.MissionIcon;
            craniusMission.MissionColor = Color.magenta;

            craniusMission.ShowInReplayMenu = true;
            craniusMission.Selectable = true;

            EnemyClass[] allEnemyClasses = Resources.FindObjectsOfTypeAll<EnemyClass>();
            foreach (EnemyClass ec in allEnemyClasses)
            {
                if (ec.customSpawner is ScrapBossSpawner)
                {
                    craniusClass = ec;
                    break;
                }
            }

            if (craniusClass == null)
            {
                return;
            }

            FieldInfo missionsField = typeof(Global).GetField("Missions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (missionsField == null)
            {
                return;
            }

            Mission[] missions = (Mission[])missionsField.GetValue(Global.Instance);
            bool alreadyAdded = false;
            foreach (Mission m in missions)
            {
                if (m is CraniusMission)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                Mission[] newMissions = new Mission[missions.Length + 1];
                Array.Copy(missions, newMissions, missions.Length);
                newMissions[missions.Length] = craniusMission;
                missionsField.SetValue(Global.Instance, newMissions);
            }

            if (PlayerData.Instance != null && PlayerData.Instance.GetFlag("cranius") == 0)
            {
                PlayerData.Instance.SetFlag("cranius", 1);
            }
            if (PlayerData.Instance.GetFlag("cranius") != 1)
            {
                PlayerData.Instance.SetFlag("cranius", 1);
            }
        }
    }

    [HarmonyPatch(typeof(AmalgamationObjective), "Setup")]
    public class AmalgamationObjectivePatch
    {
        [HarmonyPostfix]
        public static void Postfix(AmalgamationObjective __instance)
        {
            if (MissionManager.Instance.Mission is CraniusMission)
            {
                FieldInfo classField = typeof(AmalgamationObjective).GetField("amalgamationClass", BindingFlags.NonPublic | BindingFlags.Instance);
                if (classField != null)
                {
                    classField.SetValue(__instance, MissionSelectWindowInitPatch.craniusClass);
                }
            }
        }
    }

    [HarmonyPatch(typeof(MissionSelectWindow), "ToggleReplayWindow")]
    public class MissionSelectWindowPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MissionSelectWindow __instance)
        {

            if (MissionSelectWindowInitPatch.craniusMission == null)
            {
                return;
            }

            FieldInfo containerField = typeof(MissionSelectWindow).GetField("replayMissionContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            if (containerField == null)
            {
                return;
            }

            RectTransform replayMissionContainer = (RectTransform)containerField.GetValue(__instance);
            if (replayMissionContainer == null)
            {
                return;
            }

            List<MissionSelectButton> craniusButtons = new List<MissionSelectButton>();
            for (int i = 0; i < replayMissionContainer.childCount; i++)
            {
                MissionSelectButton btn = replayMissionContainer.GetChild(i).GetComponent<MissionSelectButton>();
                if (btn != null && btn.Mission.Mission == MissionSelectWindowInitPatch.craniusMission)
                {
                    craniusButtons.Add(btn);
                }
            }

            if (craniusButtons.Count > 1)
            {
                for (int i = 1; i < craniusButtons.Count; i++)
                {
                    UnityEngine.Object.Destroy(craniusButtons[i].gameObject);
                }
            }

            bool exists = craniusButtons.Count > 0;
            if (exists)
            {
            }
            else
            {
                FieldInfo prefabField = typeof(MissionSelectWindow).GetField("missionButtonPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
                if (prefabField == null)
                {
                    return;
                }

                MissionSelectButton prefab = (MissionSelectButton)prefabField.GetValue(__instance);
                if (prefab == null)
                {
                    return;
                }

                MissionSelectButton missionSelectButton = UnityEngine.Object.Instantiate(prefab, replayMissionContainer);
                missionSelectButton.gameObject.SetActive(true);
                Pigeon.Math.Random random = new Pigeon.Math.Random(Global.MissionSelectSeed + (MissionSelectWindowInitPatch.craniusMission.ID.GetHashCode() + 3173) * 5390921);
                WorldRegion worldRegion = MissionSelectWindowInitPatch.craniusMission.OverrideRegion() ?? Global.Instance.Regions[random.Next(Global.Instance.Regions.Length)];
                string sceneName;
                string scene = (MissionSelectWindowInitPatch.craniusMission.OverrideScene(out sceneName) ? sceneName : worldRegion.SceneNames[random.Next(worldRegion.SceneNames.Length)]);
                missionSelectButton.Setup(new MissionData(random.Next(), MissionSelectWindowInitPatch.craniusMission, worldRegion, scene, null), __instance, allowInteraction: true);

                RectTransform rect = missionSelectButton.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(100f, 100f);
                    rect.localScale = Vector3.one * 1.5f;
                }
            }
        }
    }
}