namespace EMI
{
    /// <summary>正式版 RecipeItem 的物品状态边界；库存与规划产物共用同一判断。</summary>
    internal static class ItemConditionRules
    {
        public static bool Matches(float condition, RecipeItem requirement, bool destroyAtZeroCondition)
        {
            return requirement != null && condition >= requirement.minimumCondition &&
                   (!requirement.noMaxCondition || condition < 0.991f) &&
                   (!destroyAtZeroCondition || condition > 0f);
        }

        public static float ProducedCondition(RecipeResult result, ItemInfo itemInfo)
        {
            if (!(itemInfo is LiquidItemInfo liquidInfo))
            {
                return result.resultCondition;
            }

            // SpawnResult 排空普通容器；保留内容的容器在 Start 中按体积/容量刷新 condition。
            if (!result.dontDrainResultLiquid)
            {
                return 0f;
            }

            float total = 0f;
            if (liquidInfo.defaultContents != null)
            {
                foreach (LiquidStack stack in liquidInfo.defaultContents)
                {
                    total += stack.amount;
                }
            }

            return total / liquidInfo.capacity;
        }
    }
}
