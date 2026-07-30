using System;
using System.Collections.Generic;
using System.Linq;

namespace EMI
{
    /// <summary>
    /// 性质图鉴中的一个可选资源，以及该资源提供的性质强度。
    /// </summary>
    internal sealed class QualityResourceEntry
    {
        public QualityResourceEntry(ResourceKey resource, float amount)
        {
            Resource = resource;
            Amount = amount;
        }

        public ResourceKey Resource { get; }

        public float Amount { get; }
    }

    /// <summary>
    /// 为图鉴预先建立资源、性质、生产配方和使用配方索引，避免 UI 渲染时反复扫描游戏数据。
    /// </summary>
    internal static class CompendiumCatalog
    {
        private static readonly List<ResourceKey> Resources = new List<ResourceKey>();
        private static readonly List<string> QualityIds = new List<string>();
        private static readonly Dictionary<ResourceKey, List<Recipe>> Producers =
            new Dictionary<ResourceKey, List<Recipe>>();
        private static readonly Dictionary<ResourceKey, List<Recipe>> Consumers =
            new Dictionary<ResourceKey, List<Recipe>>();

        public static bool IsReady { get; private set; }

        public static IReadOnlyList<ResourceKey> AllResources => Resources;

        public static IReadOnlyList<string> AllQualityIds => QualityIds;

        public static void Rebuild()
        {
            // 所有集合均保存本次运行时的游戏对象；原版重建注册表时必须整体清空再生成。
            Resources.Clear();
            QualityIds.Clear();
            Producers.Clear();
            Consumers.Clear();
            IsReady = false;

            HashSet<string> qualities = new HashSet<string>(StringComparer.Ordinal);
            if (Item.GlobalItems != null)
            {
                foreach (KeyValuePair<string, ItemInfo> entry in Item.GlobalItems)
                {
                    ResourceKey resource = new ResourceKey(entry.Key, false);
                    Resources.Add(resource);
                    AddQualityIds(qualities, entry.Value?.qualities);
                }
            }

            if (Liquids.Registry != null)
            {
                foreach (KeyValuePair<string, LiquidType> entry in Liquids.Registry)
                {
                    ResourceKey resource = new ResourceKey(entry.Key, true);
                    Resources.Add(resource);
                    AddQualityIds(qualities, entry.Value?.qualities);
                }
            }

            if (Recipes.recipes != null)
            {
                foreach (Recipe recipe in Recipes.recipes)
                {
                    if (recipe?.result == null || string.IsNullOrEmpty(recipe.result.id))
                    {
                        continue;
                    }

                    ResourceKey result = new ResourceKey(recipe.result.id, recipe.result.isLiquid);
                    GetOrAdd(Producers, result).Add(recipe);
                    if (recipe.items != null)
                    {
                        foreach (RecipeItem requirement in recipe.items)
                        {
                            if (requirement != null && !requirement.specific)
                            {
                                if (!string.IsNullOrEmpty(requirement.quality?.id))
                                {
                                    qualities.Add(requirement.quality.id);
                                }
                            }
                        }
                    }
                }

                // 抽象性质需求可能匹配许多资源，消费者索引必须通过完整兼容性规则建立。
                // 这段扫描只在目录重建时发生，换取图鉴页面切换时的常量时间查询。
                foreach (ResourceKey resource in Resources)
                {
                    foreach (Recipe recipe in Recipes.recipes)
                    {
                        if (RecipeUsesResource(recipe, resource))
                        {
                            GetOrAdd(Consumers, resource).Add(recipe);
                        }
                    }
                }
            }

            Resources.Sort((left, right) =>
            {
                int type = left.IsLiquid.CompareTo(right.IsLiquid);
                return type != 0
                    ? type
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCulture);
            });
            QualityIds.AddRange(qualities.OrderBy(
                quality => new CraftingQuality(quality).LocaleName,
                StringComparer.CurrentCulture));

            foreach (List<Recipe> recipes in Producers.Values)
            {
                SortRecipes(recipes);
            }

            foreach (List<Recipe> recipes in Consumers.Values)
            {
                SortRecipes(recipes);
            }

            IsReady = true;
            EmiPlugin.Log?.LogInfo(
                $"[EMI] Compendium catalog ready. Resources={Resources.Count}, " +
                $"Qualities={QualityIds.Count}, Recipes={Recipes.recipes?.Count ?? 0}");
        }

        public static IReadOnlyList<Recipe> GetProducers(ResourceKey resource)
        {
            return Producers.TryGetValue(resource, out List<Recipe> recipes)
                ? recipes
                : Array.Empty<Recipe>();
        }

        public static IReadOnlyList<Recipe> GetConsumers(ResourceKey resource)
        {
            return Consumers.TryGetValue(resource, out List<Recipe> recipes)
                ? recipes
                : Array.Empty<Recipe>();
        }

        public static List<QualityResourceEntry> GetQualityResources(string qualityId)
        {
            List<QualityResourceEntry> entries = new List<QualityResourceEntry>();
            if (string.IsNullOrEmpty(qualityId))
            {
                return entries;
            }

            if (Item.GlobalItems != null)
            {
                foreach (KeyValuePair<string, ItemInfo> entry in Item.GlobalItems)
                {
                    float amount = MaxQualityAmount(entry.Value?.qualities, qualityId);
                    if (amount > 0f)
                    {
                        entries.Add(new QualityResourceEntry(
                            new ResourceKey(entry.Key, false),
                            amount));
                    }
                }
            }

            if (Liquids.Registry != null)
            {
                foreach (KeyValuePair<string, LiquidType> entry in Liquids.Registry)
                {
                    float amount = MaxQualityAmount(entry.Value?.qualities, qualityId);
                    if (amount > 0f)
                    {
                        entries.Add(new QualityResourceEntry(
                            new ResourceKey(entry.Key, true),
                            amount));
                    }
                }
            }

            entries.Sort((left, right) =>
            {
                int type = left.Resource.IsLiquid.CompareTo(right.Resource.IsLiquid);
                if (type != 0)
                {
                    return type;
                }

                int amount = right.Amount.CompareTo(left.Amount);
                return amount != 0
                    ? amount
                    : string.Compare(
                        left.Resource.DisplayName,
                        right.Resource.DisplayName,
                        StringComparison.CurrentCulture);
            });
            return entries;
        }

        public static bool ResourceHasQuality(ResourceKey resource, string qualityId)
        {
            if (resource.IsLiquid)
            {
                return Liquids.Registry != null &&
                       Liquids.Registry.TryGetValue(resource.Id, out LiquidType liquid) &&
                       MaxQualityAmount(liquid?.qualities, qualityId) > 0f;
            }

            return Item.GlobalItems != null &&
                   Item.GlobalItems.TryGetValue(resource.Id, out ItemInfo item) &&
                   MaxQualityAmount(item?.qualities, qualityId) > 0f;
        }

        private static bool RecipeUsesResource(Recipe recipe, ResourceKey resource)
        {
            if (recipe?.items == null)
            {
                return false;
            }

            foreach (RecipeItem requirement in recipe.items)
            {
                if (RequirementUsesResource(requirement, resource))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RequirementUsesResource(RecipeItem requirement, ResourceKey resource)
        {
            if (requirement == null || requirement.isLiquid != resource.IsLiquid)
            {
                return false;
            }

            if (!resource.IsLiquid &&
                !string.IsNullOrEmpty(requirement.ignoredId) &&
                string.Equals(requirement.ignoredId, resource.Id, StringComparison.Ordinal))
            {
                return false;
            }

            if (requirement.specific)
            {
                return string.Equals(requirement.specificId, resource.Id, StringComparison.Ordinal);
            }

            if (requirement.quality == null)
            {
                return false;
            }

            if (resource.IsLiquid)
            {
                return Liquids.Registry != null &&
                       Liquids.Registry.TryGetValue(resource.Id, out LiquidType liquid) &&
                       MaxQualityAmount(liquid?.qualities, requirement.quality.id) > 0f;
            }

            return Item.GlobalItems != null &&
                   Item.GlobalItems.TryGetValue(resource.Id, out ItemInfo item) &&
                   MaxQualityAmount(item?.qualities, requirement.quality.id) >= requirement.quality.amount;
        }

        private static void AddQualityIds(HashSet<string> ids, List<CraftingQuality> qualities)
        {
            if (qualities == null)
            {
                return;
            }

            foreach (CraftingQuality quality in qualities)
            {
                if (!string.IsNullOrEmpty(quality?.id))
                {
                    ids.Add(quality.id);
                }
            }
        }

        private static float MaxQualityAmount(List<CraftingQuality> qualities, string qualityId)
        {
            float amount = 0f;
            if (qualities == null)
            {
                return amount;
            }

            foreach (CraftingQuality quality in qualities)
            {
                if (quality != null && quality.id == qualityId)
                {
                    amount = Math.Max(amount, quality.amount);
                }
            }

            return amount;
        }

        private static List<Recipe> GetOrAdd(
            Dictionary<ResourceKey, List<Recipe>> dictionary,
            ResourceKey key)
        {
            if (!dictionary.TryGetValue(key, out List<Recipe> recipes))
            {
                recipes = new List<Recipe>();
                dictionary.Add(key, recipes);
            }

            return recipes;
        }

        private static void SortRecipes(List<Recipe> recipes)
        {
            recipes.Sort((left, right) =>
            {
                int intelligence = left.INT.CompareTo(right.INT);
                return intelligence != 0
                    ? intelligence
                    : string.Compare(left.simpleName, right.simpleName, StringComparison.CurrentCulture);
            });
        }
    }
}
