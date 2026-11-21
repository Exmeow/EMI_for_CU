using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
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

        [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.HandleInput))]
        private static class HandleInputPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo original = AccessTools.PropertyGetter(typeof(TMP_InputField), nameof(TMP_InputField.isFocused));
                MethodInfo replacement = AccessTools.Method(typeof(EmiPatches), nameof(IsCraftingTextInputFocused));
                foreach (CodeInstruction instruction in instructions)
                {
                    // 延用原版对制作键和身体界面键的焦点保护，不改变 Escape/暂停分支。
                    if (instruction.Calls(original))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = replacement;
                    }

                    yield return instruction;
                }
            }
        }

        internal static bool IsCraftingTextInputFocused(TMP_InputField original)
        {
            return original.isFocused || CraftingTreeHud.Active?.IsCompendiumSearchFocused == true;
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
            private sealed class RefreshState
            {
                internal List<Recipe> VanillaOrder;
                internal QolRecipeRows.Capture QolRows;
            }

            [HarmonyPrefix]
            [HarmonyBefore(QolRecipeRows.BulkOwner, QolRecipeRows.FilterOwner)]
            private static void CaptureQolRows(out RefreshState __state)
            {
                __state = new RefreshState { QolRows = QolRecipeRows.BeginCapture() };
            }

            [HarmonyPrefix]
            [HarmonyAfter(QolRecipeRows.BulkOwner, QolRecipeRows.FilterOwner)]
            private static void CaptureVanillaOrder(
                PlayerCamera __instance,
                Item ___recipeItemFilter,
                Recipes.RecipeCategory? ___selectedRecipeCategory,
                bool __runOriginal,
                RefreshState __state)
            {
                if (!__runOriginal || __state == null)
                {
                    return;
                }

                try
                {
                    __state.VanillaOrder = RecipeListOrdering.Build(
                        __instance, ___recipeItemFilter, ___selectedRecipeCategory);
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError($"[EMI] PlayerCamera.RefreshRecipeList prefix mapping failed:\n{exception}");
                }
            }

            private static void Postfix(
                PlayerCamera __instance,
                RefreshState __state,
                List<GameObject> ___recipeObjects,
                bool __runOriginal)
            {
                try
                {
                    CraftingTreeHud.Active?.HandleRecipeListRefreshed(
                        __instance, ___recipeObjects,
                        __runOriginal ? __state?.VanillaOrder : null,
                        __runOriginal ? null : __state?.QolRows?.Recipes);
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError($"[EMI] PlayerCamera.RefreshRecipeList postfix failed:\n{exception}");
                }
            }

            private static void Finalizer(RefreshState __state)
            {
                __state?.QolRows?.Dispose();
            }
        }
    }
}
