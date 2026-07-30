using System;
using System.Collections.Generic;
using System.Linq;

namespace EMI
{
    /// <summary>
    /// 按产物索引所有非修理配方，并集中处理“某配方能否满足某需求”的规则。
    /// 合成树只依赖此查询接口，不直接遍历原版全局配方表。
    /// </summary>
    internal static class RecipeCatalog
    {
        private static readonly Dictionary<ResourceKey, List<Recipe>> Producers =
            new Dictionary<ResourceKey, List<Recipe>>();

        public static bool IsReady { get; private set; }

        public static void Rebuild()
        {
            // 原版会在载入或重建数据时替换静态配方集合，因此目录不能永久缓存旧 Recipe 实例。
            Producers.Clear();
            IsReady = false;

            if (Recipes.recipes == null)
            {
                EmiPlugin.Log?.LogWarning("[EMI] Recipe catalog rebuild skipped because Recipes.recipes is null.");
                return;
            }

            foreach (Recipe recipe in Recipes.recipes)
            {
                if (recipe == null || recipe.isRepair || recipe.result == null ||
                    string.IsNullOrEmpty(recipe.result.id))
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

        public static List<Recipe> GetCompatibleProducers(ResourceKey resource, RecipeItem requirement)
        {
            IReadOnlyList<Recipe> producers = GetProducers(resource);
            List<Recipe> compatible = new List<Recipe>();
            foreach (Recipe producer in producers)
            {
                if (IsProducerCompatible(producer, requirement))
                {
                    compatible.Add(producer);
                }
            }

            return compatible;
        }

        public static bool IsProducerCompatible(Recipe producer, RecipeItem requirement)
        {
            if (producer?.result == null)
            {
                return false;
            }

            if (requirement == null || requirement.isLiquid)
            {
                return true;
            }

            // 非消耗性需求比较工具可用次数，消耗性需求只需检查产物初始耐久阈值。
            if (DurabilityRequirement.AppliesTo(requirement))
            {
                return DurabilityRequirement.GetUsesPerCraft(
                    producer.result,
                    requirement,
                    1) > 0;
            }

            return producer.result.resultCondition >= requirement.minimumCondition;
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

        public static List<Recipe> GetQualityProducers(RecipeItem requirement)
        {
            List<Recipe> matches = new List<Recipe>();
            if (requirement == null || requirement.specific || requirement.quality == null)
            {
                return matches;
            }

            foreach (KeyValuePair<ResourceKey, List<Recipe>> entry in Producers)
            {
                if (entry.Key.IsLiquid != requirement.isLiquid ||
                    !IsCandidateCompatible(new ResourceCandidate(entry.Key), requirement))
                {
                    continue;
                }

                foreach (Recipe producer in entry.Value)
                {
                    if (IsProducerCompatible(producer, requirement))
                    {
                        matches.Add(producer);
                    }
                }
            }

            return matches;
        }

        public static bool IsCandidateCompatible(
            ResourceCandidate candidate,
            RecipeItem requirement)
        {
            if (candidate == null || requirement == null || requirement.specific ||
                requirement.quality == null || candidate.Resource.IsLiquid != requirement.isLiquid)
            {
                return false;
            }

            if (requirement.isLiquid)
            {
                return Liquids.Registry != null &&
                       Liquids.Registry.TryGetValue(candidate.Resource.Id, out LiquidType liquid) &&
                       liquid?.qualities != null &&
                       liquid.qualities.Any(quality =>
                           quality.id == requirement.quality.id && quality.amount > 0f);
            }

            if (!string.IsNullOrEmpty(requirement.ignoredId) &&
                candidate.Resource.Id == requirement.ignoredId)
            {
                return false;
            }

            return Item.GlobalItems != null &&
                   Item.GlobalItems.TryGetValue(candidate.Resource.Id, out ItemInfo item) &&
                   item?.qualities != null &&
                   item.qualities.Any(quality =>
                       quality.id == requirement.quality.id &&
                       quality.amount >= requirement.quality.amount);
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
                if (!string.IsNullOrEmpty(requirement.ignoredId) && entry.Key == requirement.ignoredId)
                {
                    continue;
                }

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

            candidates.Sort((left, right) => CompareCandidates(left, right, requirement));
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

            candidates.Sort((left, right) => CompareCandidates(left, right, requirement));
            return candidates;
        }

        private static int CompareCandidates(
            ResourceCandidate left,
            ResourceCandidate right,
            RecipeItem requirement)
        {
            // 可继续展开的候选排在自然资源之前，名称仅用于提供稳定且本地化的次级排序。
            bool leftCraftable = GetCompatibleProducers(left.Resource, requirement).Count > 0;
            bool rightCraftable = GetCompatibleProducers(right.Resource, requirement).Count > 0;
            int craftable = rightCraftable.CompareTo(leftCraftable);
            return craftable != 0
                ? craftable
                : string.Compare(left.Resource.DisplayName, right.Resource.DisplayName, StringComparison.CurrentCulture);
        }
    }
}
