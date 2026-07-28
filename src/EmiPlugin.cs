using System;
using BepInEx;
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
        public const string PluginVersion = "1.0.2";

        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private bool _applicationIsQuitting;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"[EMI] Loading {PluginName} {PluginVersion}.");

            PreferenceStore.Initialize();

            gameObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);

            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(GetType().Assembly);
                Logger.LogInfo($"[EMI] {PluginName} initialized.");
            }
            catch (Exception exception)
            {
                Logger.LogError($"[EMI] Initialization failed:\n{exception}");
                throw;
            }
        }

        private void OnDestroy()
        {
            if (!_applicationIsQuitting)
            {
                Logger.LogWarning("[EMI] Plugin destroyed outside application shutdown; removing patches.");
            }

            _harmony?.UnpatchSelf();
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
    }
}
