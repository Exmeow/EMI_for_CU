using System;
using HarmonyLib;

namespace EMI
{
    internal static class EmiPatches
    {
        [HarmonyPatch(typeof(Recipes), nameof(Recipes.SetUpRecipes))]
        private static class RecipesSetUpPatch
        {
            private static void Prefix()
            {
                EmiPlugin.Log?.LogInfo("[EMI] Recipes.SetUpRecipes prefix entered.");
            }

            private static void Postfix()
            {
                EmiPlugin.Log?.LogInfo(
                    $"[EMI] Recipes.SetUpRecipes postfix entered. RecipeCount={Recipes.recipes?.Count ?? -1}");

                try
                {
                    RecipeCatalog.Rebuild();
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
            private static void Prefix(PlayerCamera __instance)
            {
                EmiPlugin.Log?.LogInfo(
                    $"[EMI] PlayerCamera.Start prefix entered. InstancePresent={__instance != null}");
            }

            private static void Postfix(PlayerCamera __instance)
            {
                EmiPlugin.Log?.LogInfo(
                    $"[EMI] PlayerCamera.Start postfix entered. InstancePresent={__instance != null}, " +
                    $"CraftingPanelPresent={__instance != null && __instance.craftingPanel != null}, " +
                    $"PinTextPresent={__instance != null && __instance.pinRecipeText != null}, " +
                    $"CatalogReady={RecipeCatalog.IsReady}");

                try
                {
                    if (!RecipeCatalog.IsReady)
                    {
                        RecipeCatalog.Rebuild();
                    }

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
                EmiPlugin.Log?.LogInfo(
                    $"[EMI] PlayerCamera.PinRecipe postfix entered. PinnedRecipe={__instance?.pinnedRecipe?.ToString() ?? "none"}, " +
                    $"HudPresent={CraftingTreeHud.Active != null}");

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
    }
}
