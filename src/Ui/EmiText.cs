using System;

namespace EMI
{
    internal static class EmiText
    {
        private static bool IsChinese
        {
            get
            {
                if (!string.IsNullOrEmpty(Locale.currentLangName) &&
                    Locale.currentLangName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string probe = Locale.GetOther("craftanyitem");
                if (string.IsNullOrEmpty(probe))
                {
                    return false;
                }

                foreach (char character in probe)
                {
                    if (character >= 0x3400 && character <= 0x9fff)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static string NormalTab => IsChinese ? "配方" : "RECIPE";
        public static string TreeTab => IsChinese ? "合成树" : "TREE";
        public static string CompendiumTab => IsChinese ? "图鉴" : "CATALOG";
        public static string ResourceCatalog => IsChinese ? "资源" : "RESOURCES";
        public static string QualityCatalog => IsChinese ? "性质" : "QUALITIES";
        public static string Search => IsChinese ? "搜索物品、液体或 ID" : "Search resources or IDs";
        public static string ClearSearch => IsChinese ? "清除搜索" : "CLEAR SEARCH";
        public static string Back => IsChinese ? "返回" : "BACK";
        public static string ProducerRecipes => IsChinese ? "产出配方" : "PRODUCER RECIPES";
        public static string ConsumerRecipes => IsChinese ? "用途配方" : "USAGE RECIPES";
        public static string NoRecipes => IsChinese ? "没有符合条件的配方" : "NO MATCHING RECIPES";
        public static string NoCandidates => IsChinese ? "没有可选资源" : "NO CANDIDATES";
        public static string Favorited => IsChinese ? "已收藏" : "FAVORITED";
        public static string NotFavorited => IsChinese ? "未收藏" : "NOT FAVORITED";
        public static string RepairNotFavorite => IsChinese
            ? "修理配方，不可收藏"
            : "REPAIR RECIPE, CANNOT BE FAVORITED";
        public static string CandidateLocked => IsChinese ? "性质选择已锁定" : "QUALITY CHOICE LOCKED";
        public static string RecipeLocked => IsChinese ? "配方已锁定" : "RECIPE LOCKED";
        public static string Reset => IsChinese ? "重置" : "RESET";
        public static string NoPinnedRecipe => IsChinese
            ? "未收藏配方"
            : "NO PINNED RECIPE";
        public static string ChooseMaterial => IsChinese ? "选择材料" : "SELECT MATERIAL";
        public static string ChooseRecipe => IsChinese ? "选择配方" : "SELECT RECIPE";
        public static string ChangeMaterial => IsChinese ? "更换材料" : "CHANGE MATERIAL";
        public static string UseQualityRequirement => IsChinese ? "按性质计算" : "USE QUALITY REQUIREMENT";
        public static string StopHere => IsChinese ? "停止展开" : "STOP HERE";
        public static string ConsumesDurability => IsChinese ? "消耗耐久" : "USES DURABILITY";
        public static string MadeUsingSelectedRecipe => IsChinese
            ? "由所选配方制作"
            : "Made using the selected recipe";
        public static string CycleBoundary => IsChinese ? "循环终点" : "CYCLE BOUNDARY";
        public static string RawMaterial => IsChinese ? "无生产配方" : "NO PRODUCER";
        public static string Root => IsChinese ? "最终产物" : "FINAL PRODUCT";
        public static string Items => IsChinese ? "项材料" : "ingredients";
        public static string Close => IsChinese ? "关闭" : "CLOSE";
        public static string RemainingMaterials => IsChinese ? "剩余材料" : "REMAINING MATERIALS";
        public static string MaterialsReady => IsChinese ? "材料齐全" : "ALL MATERIALS AVAILABLE";
        public static string QualityAmount => IsChinese ? "性质值" : "quality";
        public static string NormalTabDescription => IsChinese
            ? "查看游戏原有的配方列表"
            : "View the original recipe list";
        public static string TreeTabDescription => IsChinese
            ? "查看当前收藏配方的合成树"
            : "View the crafting tree for the pinned recipe";
        public static string CompendiumTabDescription => IsChinese
            ? "查看全部物品、液体、性质与相关配方"
            : "Browse all items, liquids, qualities, and related recipes";
        public static string ResourceCatalogDescription => IsChinese
            ? "浏览全部物品与液体"
            : "Browse all items and liquids";
        public static string QualityCatalogDescription => IsChinese
            ? "浏览全部合成性质及其候选资源"
            : "Browse crafting qualities and their candidate resources";
        public static string SearchDescription => IsChinese
            ? "按当前语言名称或内部 ID 筛选资源"
            : "Filter resources by localized name or internal ID";
        public static string ClearSearchDescription => IsChinese
            ? "清除当前资源筛选"
            : "Clear the current resource filter";
        public static string BackDescription => IsChinese
            ? "返回上一级图鉴"
            : "Return to the previous catalog view";
        public static string RecipeLockedDescription => IsChinese
            ? "请在图鉴栏目中取消收藏对应配方"
            : "Unfavorite the corresponding recipe in the catalog";
        public static string CandidateLockedDescription => IsChinese
            ? "请在性质图鉴中取消收藏对应选择"
            : "Unfavorite the corresponding choice in the quality catalog";
        public static string ResetDescription => IsChinese
            ? "清除合成树中的材料和配方选择"
            : "Clear material and recipe selections in the tree";
        public static string CloseDescription => IsChinese
            ? "关闭当前选择窗口"
            : "Close the current selection window";
        public static string UseQualityRequirementDescription => IsChinese
            ? "保留抽象性质需求，不指定具体材料"
            : "Keep the quality requirement without choosing a material";
        public static string ChangeMaterialDescription => IsChinese
            ? "为此性质需求选择另一种具体材料"
            : "Choose another material for this quality requirement";
        public static string StopHereDescription => IsChinese
            ? "将此材料保留为叶节点，不再展开生产配方"
            : "Keep this material as a leaf and stop expanding its recipe";

        public static string FormatRequiredUses(int uses)
        {
            return IsChinese
                ? "需要 " + uses + " 次"
                : uses + (uses == 1 ? " use required" : " uses required");
        }

        public static string FormatQualityItem(string qualityName, bool isTool)
        {
            if (IsChinese)
            {
                return "具有" + qualityName + "性质的" + (isTool ? "工具" : "物品");
            }

            return (isTool ? "Tool" : "Item") + " with " + qualityName;
        }

        public static string FormatQualityLiquid(string qualityName)
        {
            return IsChinese
                ? "具有" + qualityName + "性质的液体"
                : "Liquid with " + qualityName;
        }

        public static string FormatQualityLiquidRequirement(string qualityName, string amount)
        {
            return IsChinese
                ? FormatQualityLiquid(qualityName) + "（需要 " + amount + " 性质值）"
                : FormatQualityLiquid(qualityName) + " (" + amount + " quality required)";
        }

        public static string FormatQualityAmountRequired(string amount)
        {
            return IsChinese
                ? "需要 " + amount + " 性质值"
                : amount + " quality required";
        }

        public static string FormatChooseDescription(string materialName, bool chooseMaterial)
        {
            if (IsChinese)
            {
                return chooseMaterial
                    ? "为" + materialName + "选择具体材料"
                    : "为" + materialName + "选择生产配方";
            }

            return (chooseMaterial ? "Choose a material for " : "Choose a producer for ") + materialName;
        }

        public static string FormatCandidateCount(int count)
        {
            return IsChinese
                ? count + " 个可选资源"
                : count + (count == 1 ? " candidate" : " candidates");
        }

        public static string FormatQualityValue(float amount)
        {
            return IsChinese
                ? "性质值 " + amount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                : "Quality " + amount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatQualityPerMilliliter(float amount)
        {
            return IsChinese
                ? "每 mL 性质值 " + amount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                : amount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " quality per mL";
        }

        public static string FormatCraftingIntWarning(int requiredInt, bool impossible)
        {
            if (IsChinese)
            {
                return (impossible
                    ? "当前制作无法完成"
                    : "当前制作可能产生失误") +
                    "（需要智力等级 " + requiredInt + "）";
            }

            return (impossible
                ? "The current crafting plan cannot be completed"
                : "The current crafting plan may fail") +
                " (requires INT level " + requiredInt + ")";
        }
    }
}
