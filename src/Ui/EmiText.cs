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
    }
}
