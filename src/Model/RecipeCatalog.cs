using System;
using System.Collections.Generic;
using System.Linq;

namespace EMI
{
    internal static class RecipeCatalog
    {
        private static readonly Dictionary<ResourceKey, List<Recipe>> Producers =
            new Dictionary<ResourceKey, List<Recipe>>();

        public static bool IsReady { get; private set; }

        public static void Rebuild()
        {
            Producers.Clear();
            IsReady = false;

            if (Recipes.recipes == null)
            {
                EmiPlugin.Log?.LogWarning("[EMI] Recipe catalog rebuild skipped because Recipes.recipes is null.");
                return;
            }

            foreach (Recipe recipe in Recipes.recipes)
            {
                if (recipe == null || recipe.result == null || string.IsNullOrEmpty(recipe.result.id))
                {
                    continue;
                }

                ResourceKey key = new ResourceKey(recipe.result.id, recipe.result.isLiquid);
                if (!Producers.TryGetValue(key, out List<Recipe> recipes))
                {
                    recipes = new List<Recipe>();
                    Producers.Add(key, recipes);
                }

                recipes.Add(recipe);
            }

            foreach (List<Recipe> recipes in Producers.Values)
            {
                recipes.Sort((left, right) =>
                {
                    int intelligence = left.INT.CompareTo(right.INT);
                    return intelligence != 0
                        ? intelligence
                        : string.Compare(left.simpleName, right.simpleName, StringComparison.CurrentCulture);
                });
            }

            IsReady = true;
            EmiPlugin.Log?.LogInfo(
                $"[EMI] Recipe catalog ready. Recipes={Recipes.recipes.Count}, Products={Producers.Count}, " +
                $"GlobalItemsPresent={Item.GlobalItems != null}, LiquidsPresent={Liquids.Registry != null}");
        }

        public static IReadOnlyList<Recipe> GetProducers(ResourceKey resource)
        {
            return Producers.TryGetValue(resource, out List<Recipe> recipes)
                ? recipes
                : Array.Empty<Recipe>();
        }

        public static List<ResourceCandidate> GetCandidates(RecipeItem requirement)
        {
            if (requirement == null || requirement.specific || requirement.quality == null)
            {
                return new List<ResourceCandidate>();
            }

            return requirement.isLiquid
                ? GetLiquidCandidates(requirement)
                : GetItemCandidates(requirement);
        }

        private static List<ResourceCandidate> GetItemCandidates(RecipeItem requirement)
        {
            List<ResourceCandidate> candidates = new List<ResourceCandidate>();
            if (Item.GlobalItems == null)
            {
                return candidates;
            }

            foreach (KeyValuePair<string, ItemInfo> entry in Item.GlobalItems)
            {
                List<CraftingQuality> qualities = entry.Value?.qualities;
                if (qualities == null)
                {
                    continue;
                }

                bool matches = qualities.Any(quality =>
                    quality.id == requirement.quality.id &&
                    quality.amount >= requirement.quality.amount);

                if (matches)
                {
                    candidates.Add(new ResourceCandidate(new ResourceKey(entry.Key, false)));
                }
            }

            candidates.Sort(CompareCandidates);
            return candidates;
        }

        private static List<ResourceCandidate> GetLiquidCandidates(RecipeItem requirement)
        {
            List<ResourceCandidate> candidates = new List<ResourceCandidate>();
            if (Liquids.Registry == null)
            {
                return candidates;
            }

            foreach (KeyValuePair<string, LiquidType> entry in Liquids.Registry)
            {
                CraftingQuality quality = entry.Value?.qualities?.FirstOrDefault(candidate =>
                    candidate.id == requirement.quality.id && candidate.amount > 0f);

                if (quality == null)
                {
                    continue;
                }

                float requiredAmount = requirement.quality.amount / quality.amount;
                candidates.Add(new ResourceCandidate(new ResourceKey(entry.Key, true), requiredAmount));
            }

            candidates.Sort(CompareCandidates);
            return candidates;
        }

        private static int CompareCandidates(ResourceCandidate left, ResourceCandidate right)
        {
            bool leftCraftable = GetProducers(left.Resource).Count > 0;
            bool rightCraftable = GetProducers(right.Resource).Count > 0;
            int craftable = rightCraftable.CompareTo(leftCraftable);
            return craftable != 0
                ? craftable
                : string.Compare(left.Resource.DisplayName, right.Resource.DisplayName, StringComparison.CurrentCulture);
        }
    }
}
