using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EMI
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class EmiPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "exmeow.casualtiesunknown.emi";
        public const string PluginName = "EMI";
        public const string PluginVersion = "0.5.1";

        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private bool _applicationIsQuitting;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"[EMI] Awake entered. Version={PluginVersion}, Unity={Application.unityVersion}, Assembly={GetType().Assembly.Location}");

            gameObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            Logger.LogInfo(
                $"[EMI] BepInEx manager persistence applied. GameObject={gameObject.name}, " +
                $"HideFlags={gameObject.hideFlags}, ActiveSelf={gameObject.activeSelf}");

            try
            {
                var playerStart = AccessTools.Method(typeof(PlayerCamera), "Start");
                var recipesSetup = AccessTools.Method(typeof(Recipes), nameof(Recipes.SetUpRecipes));
                var pinRecipe = AccessTools.Method(typeof(PlayerCamera), nameof(PlayerCamera.PinRecipe));
                var playerLateUpdate = AccessTools.Method(typeof(PlayerCamera), "LateUpdate");
                var refreshRecipeList = AccessTools.Method(typeof(PlayerCamera), nameof(PlayerCamera.RefreshRecipeList));
                Logger.LogInfo(
                    $"[EMI] Patch targets: PlayerCamera.Start={playerStart != null}, " +
                    $"Recipes.SetUpRecipes={recipesSetup != null}, PlayerCamera.PinRecipe={pinRecipe != null}, " +
                    $"PlayerCamera.LateUpdate={playerLateUpdate != null}, " +
                    $"PlayerCamera.RefreshRecipeList={refreshRecipeList != null}");

                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(GetType().Assembly);

                LogPatchState("PlayerCamera.Start", playerStart);
                LogPatchState("Recipes.SetUpRecipes", recipesSetup);
                LogPatchState("PlayerCamera.PinRecipe", pinRecipe);
                LogPatchState("PlayerCamera.LateUpdate", playerLateUpdate);
                LogPatchState("PlayerCamera.RefreshRecipeList", refreshRecipeList);
                Logger.LogInfo("[EMI] Initialization and Harmony patching completed.");
            }
            catch (Exception exception)
            {
                Logger.LogError($"[EMI] Initialization failed:\n{exception}");
                throw;
            }
        }

        private void LogPatchState(string name, System.Reflection.MethodBase method)
        {
            if (method == null)
            {
                Logger.LogError($"[EMI] Cannot inspect {name}: target method was not found.");
                return;
            }

            Patches patches = Harmony.GetPatchInfo(method);
            bool owned = patches != null && patches.Owners.Contains(PluginGuid);
            Logger.LogInfo($"[EMI] Patch state: {name}, ownedByEMI={owned}");
        }

        private void OnDestroy()
        {
            GameObject manager = Chainloader.ManagerObject;
            Transform parent = transform.parent;
            Logger.LogWarning(
                "[EMI] Plugin OnDestroy entered.\n" +
                $"Frame={Time.frameCount}\n" +
                $"Scene={gameObject.scene.name}\n" +
                $"GameObject={gameObject.name}\n" +
                $"InstanceId={gameObject.GetInstanceID()}\n" +
                $"Parent={(parent != null ? parent.name : "none")}\n" +
                $"Root={transform.root.name}\n" +
                $"ActiveSelf={gameObject.activeSelf}\n" +
                $"ActiveInHierarchy={gameObject.activeInHierarchy}\n" +
                $"ComponentEnabled={enabled}\n" +
                $"HideFlags={gameObject.hideFlags}\n" +
                $"ApplicationIsQuitting={_applicationIsQuitting}\n" +
                $"ManagerObjectPresent={manager != null}\n" +
                $"IsManagerObject={manager == gameObject}\n" +
                $"ManagerObjectName={(manager != null ? manager.name : "none")}\n" +
                $"ManagerObjectScene={(manager != null ? manager.scene.name : "none")}\n" +
                "Managed stack trace:\n" + Environment.StackTrace);

            Logger.LogInfo("[EMI] Removing Harmony patches from OnDestroy.");
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            Logger.LogInfo("[EMI] OnApplicationQuit entered.");
        }
    }
}
