namespace EMI
{
    /// <summary>
    /// 统一管理依赖游戏静态数据的目录初始化顺序。
    /// 配方收藏必须在图鉴目录构建完成后解析，因此调用方不应分别初始化这些模块。
    /// </summary>
    internal static class CatalogCoordinator
    {
        /// <summary>
        /// 原版重新建立配方数据后，完整重建所有派生目录和运行时收藏引用。
        /// </summary>
        public static void Rebuild()
        {
            RecipeCatalog.Rebuild();
            CompendiumCatalog.Rebuild();
            PreferenceStore.ResolveRecipes();
        }

        /// <summary>
        /// 玩家界面启动时补建尚未就绪的目录。
        /// 正常情况下 Recipes.SetUpRecipes 补丁已经完成初始化，此方法仅负责兼容异常加载顺序。
        /// </summary>
        public static void EnsureReady()
        {
            if (!RecipeCatalog.IsReady)
            {
                RecipeCatalog.Rebuild();
            }

            if (!CompendiumCatalog.IsReady)
            {
                CompendiumCatalog.Rebuild();
                PreferenceStore.ResolveRecipes();
            }
        }
    }
}
