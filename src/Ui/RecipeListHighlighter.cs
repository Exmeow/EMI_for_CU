using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EMI
{
    /// <summary>
    /// 管理原版配方行上的 EMI 装饰、排序与高亮状态。
    /// HUD 只需提供规划结果，无须了解原版配方行的具体结构。
    /// </summary>
    internal sealed class RecipeListHighlighter
    {
        private enum HighlightPriority
        {
            RequiredTreeStep,
            LeafProducer,
            Normal
        }

        private sealed class RowBinding
        {
            public Recipe Recipe;
            public RectTransform RectTransform;
            public Image Background;
            public Image Overlay;
            public Image Accent;
            public Color OriginalBackground;
            public int OriginalOrder;
        }

        private const float RecipeRowHeight = 64f;

        private static readonly Color RequiredStepColor =
            new Color(0.34f, 0.78f, 0.46f, 1f);

        private static readonly Color LeafProducerColor =
            new Color(0.36f, 0.68f, 0.88f, 1f);

        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private readonly HashSet<int> _readyTreeRecipes = new HashSet<int>();
        private readonly HashSet<int> _readyLeafRecipes = new HashSet<int>();

        // 半秒刷新会反复使用这些集合，复用实例可以避免持续产生短生命周期对象。
        private readonly HashSet<int> _nextReadyTreeRecipes = new HashSet<int>();
        private readonly HashSet<int> _nextReadyLeafRecipes = new HashSet<int>();
        private readonly Dictionary<Recipe, bool> _availability =
            new Dictionary<Recipe, bool>();

        /// <summary>
        /// 将配方绑定到 PlayerCamera.RefreshRecipeList 创建的界面行。
        /// 两个列表必须保持相同的顺序；该顺序由 RecipeListOrdering 负责复现。
        /// </summary>
        public void Bind(IReadOnlyList<Recipe> recipes, IReadOnlyList<GameObject> rows)
        {
            _rows.Clear();
            if (recipes == null || rows == null)
            {
                return;
            }

            int count = Math.Min(recipes.Count, rows.Count);
            if (recipes.Count != rows.Count)
            {
                EmiPlugin.Log?.LogWarning(
                    $"[EMI] Recipe row binding count mismatch. Recipes={recipes.Count}, Rows={rows.Count}");
            }

            for (int index = 0; index < count; index++)
            {
                GameObject row = rows[index];
                if (row == null)
                {
                    continue;
                }

                Image background = row.GetComponent<Image>();
                RectTransform rectTransform = row.GetComponent<RectTransform>();
                if (background == null || rectTransform == null)
                {
                    continue;
                }

                // 装饰层不接收射线，点击仍由原版 Button 处理，避免遮挡原版交互。
                Image accent = UiFactory.CreatePanel(
                    "EMIReadyAccent",
                    row.transform,
                    Color.clear);
                accent.raycastTarget = false;
                UiFactory.Anchor(
                    accent.rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(4f, 0f),
                    new Vector2(7f, 54f));

                Image overlay = UiFactory.CreatePanel(
                    "EMIReadyOverlay",
                    row.transform,
                    Color.clear);
                overlay.raycastTarget = false;
                UiFactory.Stretch(overlay.rectTransform);
                overlay.transform.SetAsFirstSibling();

                _rows.Add(new RowBinding
                {
                    Recipe = recipes[index],
                    RectTransform = rectTransform,
                    Background = background,
                    Overlay = overlay,
                    Accent = accent,
                    OriginalBackground = background.color,
                    OriginalOrder = index
                });
            }

            Apply();
        }

        /// <summary>
        /// 重新计算规划中的哪些配方当前可制作。
        /// 仅当两类高亮集合发生变化时才重新排序，避免无意义地改动原版列表。
        /// </summary>
        public void Update(CraftingPlanResult plan)
        {
            if (plan == null)
            {
                Clear();
                return;
            }

            _nextReadyTreeRecipes.Clear();
            _nextReadyLeafRecipes.Clear();
            _availability.Clear();

            foreach (Recipe recipe in plan.RequiredRecipes)
            {
                if (IsRecipeReady(recipe))
                {
                    _nextReadyTreeRecipes.Add(recipe.index);
                }
            }

            foreach (Recipe recipe in plan.LeafProducerRecipes)
            {
                if (!_nextReadyTreeRecipes.Contains(recipe.index) && IsRecipeReady(recipe))
                {
                    _nextReadyLeafRecipes.Add(recipe.index);
                }
            }

            if (_readyTreeRecipes.SetEquals(_nextReadyTreeRecipes) &&
                _readyLeafRecipes.SetEquals(_nextReadyLeafRecipes))
            {
                return;
            }

            _readyTreeRecipes.Clear();
            _readyTreeRecipes.UnionWith(_nextReadyTreeRecipes);
            _readyLeafRecipes.Clear();
            _readyLeafRecipes.UnionWith(_nextReadyLeafRecipes);
            Apply();
        }

        public void Clear()
        {
            if (_readyTreeRecipes.Count == 0 && _readyLeafRecipes.Count == 0)
            {
                return;
            }

            _readyTreeRecipes.Clear();
            _readyLeafRecipes.Clear();
            Apply();
        }

        private bool IsRecipeReady(Recipe recipe)
        {
            if (recipe == null || !recipe.visible)
            {
                return false;
            }

            // 同一配方可能同时出现在两个规划集合中，因此每轮刷新只查询一次世界状态。
            if (!_availability.TryGetValue(recipe, out bool ready))
            {
                ready = recipe.GetItemsForRecipe() != null;
                _availability.Add(recipe, ready);
            }

            return ready;
        }

        private void Apply()
        {
            _rows.Sort((left, right) =>
            {
                int priority = GetPriority(left.Recipe).CompareTo(GetPriority(right.Recipe));
                return priority != 0
                    ? priority
                    : left.OriginalOrder.CompareTo(right.OriginalOrder);
            });

            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                if (binding.RectTransform == null || binding.Background == null ||
                    binding.Overlay == null || binding.Accent == null)
                {
                    continue;
                }

                HighlightPriority priority = GetPriority(binding.Recipe);
                bool highlighted = priority != HighlightPriority.Normal;
                Color highlight = priority == HighlightPriority.RequiredTreeStep
                    ? RequiredStepColor
                    : LeafProducerColor;

                // 原版通过坐标而非布局组件排列配方行，因此 EMI 排序时也必须更新 Y 坐标。
                Vector2 position = binding.RectTransform.anchoredPosition;
                position.y = -index * RecipeRowHeight;
                binding.RectTransform.anchoredPosition = position;
                binding.Background.color = binding.OriginalBackground;
                binding.Overlay.color = highlighted
                    ? new Color(highlight.r, highlight.g, highlight.b, 0.34f)
                    : Color.clear;
                binding.Accent.color = highlighted ? highlight : Color.clear;
            }
        }

        private HighlightPriority GetPriority(Recipe recipe)
        {
            if (recipe != null && _readyTreeRecipes.Contains(recipe.index))
            {
                return HighlightPriority.RequiredTreeStep;
            }

            return recipe != null && _readyLeafRecipes.Contains(recipe.index)
                ? HighlightPriority.LeafProducer
                : HighlightPriority.Normal;
        }
    }
}
