using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace EMI
{
    /// <summary>
    /// 将原版游戏生命周期和输入事件转发给 EMI。
    /// 补丁只负责接入与异常隔离，具体目录、规划和界面行为由各自模块处理。
    /// </summary>
    internal static class EmiPatches
    {
        [HarmonyPatch(typeof(Recipes), nameof(Recipes.SetUpRecipes))]
        private static class RecipesSetUpPatch
        {
            private static void Postfix()
            {
                try
                {
                    CatalogCoordinator.Rebuild();
                    CraftingTreeHud.Active?.HandleRecipesRebuilt();
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError($"[EMI] Recipes.SetUpRecipes postfix failed:\n{exception}");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), "Start")]
        private static class PlayerCameraStartPatch
        {
            private static void Postfix(PlayerCamera __instance)
            {
                try
                {
                    CatalogCoordinator.EnsureReady();
                    CraftingTreeHud.Attach(__instance);
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError($"[EMI] PlayerCamera.Start postfix failed:\n{exception}");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.PinRecipe))]
        private static class PinRecipePatch
        {
            private static void Postfix(PlayerCamera __instance)
            {
                try
                {
                    CraftingTreeHud.Active?.HandlePinnedRecipeChanged(__instance);
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError($"[EMI] PlayerCamera.PinRecipe postfix failed:\n{exception}");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.TryCraft))]
        private static class TryCraftPatch
        {
            private static void Postfix(PlayerCamera __instance)
            {
                try
                {
                    CraftingTreeHud.Active?.HandleCraftAttempt(__instance);
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError($"[EMI] PlayerCamera.TryCraft postfix failed:\n{exception}");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), "LateUpdate")]
        private static class PlayerCameraLateUpdatePatch
        {
            private static bool _failureLogged;

            private static void Postfix(PlayerCamera __instance)
            {
                try
                {
                    CraftingTreeHud.Active?.HandlePlayerLateUpdate(__instance);
                }
                catch (Exception exception)
                {
                    if (!_failureLogged)
                    {
                        _failureLogged = true;
                        EmiPlugin.Log?.LogError(
                            $"[EMI] PlayerCamera.LateUpdate postfix failed:\n{exception}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.OpenCraftScreen))]
        private static class OpenCraftScreenPatch
        {
            private static bool Prefix()
            {
                try
                {
                    // 原版会先处理关闭界面的按键，再把点击事件交给图鉴资源格，因此这里需要提前截获。
                    KeyCode interaction = KeyBinds.GetBind("iteminteract");
                    bool mouseInteraction = interaction >= KeyCode.Mouse0 &&
                                            interaction <= KeyCode.Mouse6 &&
                                            Input.GetKeyDown(interaction);
                    if (mouseInteraction &&
                        CraftingTreeHud.Active?.ShouldCaptureCompendiumMouseInteraction() == true)
                    {
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError(
                        $"[EMI] PlayerCamera.OpenCraftScreen prefix failed; " +
                        $"the original close behavior will continue:\n{exception}");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), "HandleCursorIcon")]
        private static class HandleCursorIconPatch
        {
            private static bool _failureLogged;

            private static void Prefix(ref int ___cursor)
            {
                try
                {
                    if (CraftingTreeHud.Active?.TryGetForegroundCursor(out int cursor) == true)
                    {
                        ___cursor = cursor;
                    }
                }
                catch (Exception exception)
                {
                    if (!_failureLogged)
                    {
                        _failureLogged = true;
                        EmiPlugin.Log?.LogError(
                            $"[EMI] PlayerCamera.HandleCursorIcon prefix failed; " +
                            $"the original cursor will continue:\n{exception}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.RefreshRecipeList))]
        private static class RefreshRecipeListPatch
        {
            private static void Prefix(
                PlayerCamera __instance,
                Item ___recipeItemFilter,
                Recipes.RecipeCategory? ___selectedRecipeCategory,
                out List<Recipe> __state)
            {
                try
                {
                    __state = RecipeListOrdering.Build(
                        __instance,
                        ___recipeItemFilter,
                        ___selectedRecipeCategory);
                }
                catch (Exception exception)
                {
                    __state = null;
                    EmiPlugin.Log?.LogError(
                        $"[EMI] PlayerCamera.RefreshRecipeList prefix mapping failed; " +
                        $"the original list will continue without EMI ordering:\n{exception}");
                }
            }

            private static void Postfix(
                PlayerCamera __instance,
                List<Recipe> __state,
                List<GameObject> ___recipeObjects)
            {
                try
                {
                    CraftingTreeHud.Active?.HandleRecipeListRefreshed(
                        __instance,
                        __state,
                        ___recipeObjects);
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError($"[EMI] PlayerCamera.RefreshRecipeList postfix failed:\n{exception}");
                }
            }
        }
    }
}
