using System;
using System.Collections.Generic;

namespace EMI
{
    /// <summary>
    /// 集中封装原版非消耗性材料的耐久规则，供树节点次数计算与虚拟库存分配共同使用。
    /// </summary>
    internal static class DurabilityRequirement
    {
        private const float ConditionEpsilon = 0.0001f;

        public static bool AppliesTo(RecipeItem requirement)
        {
            return requirement != null && !requirement.isLiquid && !requirement.destroyItem;
        }

        public static bool IsQualityTool(RecipeItem requirement)
        {
            if (!AppliesTo(requirement) || requirement.specific || requirement.quality == null)
            {
                return false;
            }

            string qualityId = requirement.quality.id;
            return qualityId == "cutting" || qualityId == "hammering";
        }

        public static float GetConditionCost(
            RecipeItem requirement,
            List<CraftingQuality> actualQualities)
        {
            if (!AppliesTo(requirement))
            {
                return 0f;
            }

            if (requirement.specific)
            {
                return Math.Max(0f, requirement.minimumCondition);
            }

            if (requirement.quality == null || actualQualities == null)
            {
                return float.PositiveInfinity;
            }

            CraftingQuality actual = Item.GetQualityThatMeetsCriteria(
                requirement.quality,
                actualQualities);
            if (actual == null || actual.amount <= ConditionEpsilon)
            {
                return float.PositiveInfinity;
            }

            return Math.Max(0f, requirement.quality.amount / actual.amount);
        }

        public static int GetUseCapacity(
            float condition,
            RecipeItem requirement,
            List<CraftingQuality> actualQualities,
            int maximumUses)
        {
            if (!AppliesTo(requirement) || maximumUses <= 0 ||
                condition <= ConditionEpsilon || condition < requirement.minimumCondition)
            {
                return 0;
            }

            float conditionCost = GetConditionCost(requirement, actualQualities);
            if (float.IsInfinity(conditionCost) || float.IsNaN(conditionCost))
            {
                return 0;
            }

            if (conditionCost <= ConditionEpsilon)
            {
                return maximumUses;
            }

            // 逐次扣除可精确复现原版“使用前满足最低耐久”的边界，避免浮点除法产生多算一次。
            int uses = 0;
            float remainingCondition = condition;
            while (uses < maximumUses &&
                   remainingCondition > ConditionEpsilon &&
                   remainingCondition >= requirement.minimumCondition)
            {
                uses++;
                remainingCondition -= conditionCost;
            }

            return uses;
        }

        public static int GetUsesPerCraft(
            RecipeResult result,
            RecipeItem requirement,
            int maximumUses)
        {
            if (result == null || maximumUses <= 0 || result.isLiquid ||
                string.IsNullOrEmpty(result.id))
            {
                return 0;
            }

            List<CraftingQuality> qualities = null;
            if (Item.GlobalItems != null &&
                Item.GlobalItems.TryGetValue(result.id, out ItemInfo itemInfo))
            {
                qualities = itemInfo?.qualities;
            }

            int usesPerItem = GetUseCapacity(
                result.resultCondition,
                requirement,
                qualities,
                maximumUses);
            if (usesPerItem <= 0)
            {
                return 0;
            }

            long totalUses = (long)usesPerItem * Math.Max(1, result.amount);
            return (int)Math.Min(maximumUses, totalUses);
        }
    }
}
