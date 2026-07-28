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
        public static string StopHere => IsChinese ? "停止展开" : "STOP HERE";
        public static string Reusable => IsChinese ? "可复用" : "REUSABLE";
        public static string SharedReusable => IsChinese ? "已在上游计入" : "SHARED TOOL";
        public static string CycleBoundary => IsChinese ? "循环终点" : "CYCLE BOUNDARY";
        public static string RawMaterial => IsChinese ? "无生产配方" : "NO PRODUCER";
        public static string Root => IsChinese ? "最终产物" : "FINAL PRODUCT";
        public static string Items => IsChinese ? "项材料" : "ingredients";
        public static string Close => IsChinese ? "关闭" : "CLOSE";
    }
}
