using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

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

        [HarmonyPatch(typeof(PlayerCamera), "LateUpdate")]
        private static class PlayerCameraLateUpdatePatch
        {
            private static void Postfix(PlayerCamera __instance)
            {
                try
                {
                    CraftingTreeHud.Active?.HandlePlayerLateUpdate(__instance);
                }
                catch (Exception exception)
                {
                    EmiPlugin.Log?.LogError($"[EMI] PlayerCamera.LateUpdate postfix failed:\n{exception}");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.RefreshRecipeList))]
        private static class RefreshRecipeListPatch
        {
            private sealed class RecipeListEntry
            {
                public Recipe Recipe;
                public bool Available;
                public int OriginalOrder;
            }

            private static void Prefix(
                PlayerCamera __instance,
                Item ___recipeItemFilter,
                Recipes.RecipeCategory? ___selectedRecipeCategory,
                out List<Recipe> __state)
            {
                try
                {
                    __state = BuildDisplayedRecipeOrder(
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

            private static List<Recipe> BuildDisplayedRecipeOrder(
                PlayerCamera player,
                Item itemFilter,
                Recipes.RecipeCategory? category)
            {
                List<Recipe> visibleRecipes = Recipes.GetVisibleRecipes(itemFilter);
                List<RecipeListEntry> entries = new List<RecipeListEntry>(visibleRecipes.Count);
                for (int index = 0; index < visibleRecipes.Count; index++)
                {
                    Recipe recipe = visibleRecipes[index];
                    entries.Add(new RecipeListEntry
                    {
                        Recipe = recipe,
                        Available = recipe.GetItemsForRecipe() != null,
                        OriginalOrder = index
                    });
                }

                entries.Sort((left, right) =>
                {
                    int intelligence = left.Recipe.INT.CompareTo(right.Recipe.INT);
                    return intelligence != 0
                        ? intelligence
                        : left.OriginalOrder.CompareTo(right.OriginalOrder);
                });

                List<Recipe> displayed = new List<Recipe>(entries.Count);
                AddDisplayedRecipes(displayed, entries, true, player, itemFilter, category);
                AddDisplayedRecipes(displayed, entries, false, player, itemFilter, category);
                return displayed;
            }

            private static void AddDisplayedRecipes(
                List<Recipe> displayed,
                List<RecipeListEntry> entries,
                bool available,
                PlayerCamera player,
                Item itemFilter,
                Recipes.RecipeCategory? category)
            {
                foreach (RecipeListEntry entry in entries)
                {
                    if (entry.Available != available)
                    {
                        continue;
                    }

                    Recipe recipe = entry.Recipe;
                    if (category.HasValue && string.IsNullOrEmpty(player.recipeFilter) && itemFilter == null &&
                        recipe.category != category.Value)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(player.recipeFilter) && itemFilter == null &&
                        recipe.simpleName.IndexOf(player.recipeFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    displayed.Add(recipe);
                }
            }
        }
    }
}
