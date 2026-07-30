using System;
using System.Collections.Generic;

namespace EMI
{
    /// <summary>
    /// 在 EMI 移动高亮行之前，复现原版可见配方的排列顺序。
    /// 将这段逻辑放在 Harmony 补丁之外，可以明确记录这项容易受原版更新影响的顺序约定。
    /// </summary>
    internal static class RecipeListOrdering
    {
        private sealed class Entry
        {
            public Recipe Recipe;
            public bool Available;
            public int OriginalOrder;
        }

        public static List<Recipe> Build(
            PlayerCamera player,
            Item itemFilter,
            Recipes.RecipeCategory? category)
        {
            List<Recipe> visibleRecipes = Recipes.GetVisibleRecipes(itemFilter);
            List<Entry> entries = new List<Entry>(visibleRecipes.Count);
            for (int index = 0; index < visibleRecipes.Count; index++)
            {
                Recipe recipe = visibleRecipes[index];
                entries.Add(new Entry
                {
                    Recipe = recipe,
                    Available = recipe.GetItemsForRecipe() != null,
                    OriginalOrder = index
                });
            }

            // 原版分别对可制作与不可制作的配方按智力排序；智力相同时保留数据源顺序。
            entries.Sort((left, right) =>
            {
                int intelligence = left.Recipe.INT.CompareTo(right.Recipe.INT);
                return intelligence != 0
                    ? intelligence
                    : left.OriginalOrder.CompareTo(right.OriginalOrder);
            });

            List<Recipe> displayed = new List<Recipe>(entries.Count);
            AddGroup(displayed, entries, true, player, itemFilter, category);
            AddGroup(displayed, entries, false, player, itemFilter, category);
            return displayed;
        }

        private static void AddGroup(
            List<Recipe> displayed,
            List<Entry> entries,
            bool available,
            PlayerCamera player,
            Item itemFilter,
            Recipes.RecipeCategory? category)
        {
            foreach (Entry entry in entries)
            {
                if (entry.Available != available ||
                    !MatchesVanillaFilter(entry.Recipe, player, itemFilter, category))
                {
                    continue;
                }

                displayed.Add(entry.Recipe);
            }
        }

        private static bool MatchesVanillaFilter(
            Recipe recipe,
            PlayerCamera player,
            Item itemFilter,
            Recipes.RecipeCategory? category)
        {
            // GetVisibleRecipes 已处理物品筛选；启用物品筛选时，原版不会再应用分类和搜索条件。
            if (itemFilter != null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(player.recipeFilter))
            {
                return recipe.simpleName.IndexOf(
                           player.recipeFilter,
                           StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return !category.HasValue || recipe.category == category.Value;
        }
    }
}
