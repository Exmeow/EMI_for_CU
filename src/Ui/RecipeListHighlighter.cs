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
            public Image Overlay;
            public Image Accent;
            public int OriginalOrder;
        }

        private const float RecipeRowHeight = 64f;

        private static readonly Color RequiredStepColor =
            new Color(0.34f, 0.78f, 0.46f, 1f);

        private static readonly Color LeafProducerColor =
            new Color(0.36f, 0.68f, 0.88f, 1f);

        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private bool _reorderRows;
        private readonly HashSet<int> _readyTreeRecipes = new HashSet<int>();
        private readonly HashSet<int> _readyLeafRecipes = new HashSet<int>();

        // 半秒刷新会反复使用这些集合，复用实例可以避免持续产生短生命周期对象。
        private readonly HashSet<int> _nextReadyTreeRecipes = new HashSet<int>();
        private readonly HashSet<int> _nextReadyLeafRecipes = new HashSet<int>();
        private readonly Dictionary<Recipe, bool> _availability =
            new Dictionary<Recipe, bool>();

        /// <summary>原版按顺序绑定，QoL 按实际写入每行的配方绑定。</summary>
        public void Bind(
            IReadOnlyList<GameObject> rows,
            IReadOnlyList<Recipe> vanillaOrder,
            IReadOnlyDictionary<GameObject, Recipe> capturedRows)
        {
            foreach (RowBinding binding in _rows)
            {
                if (binding.Overlay != null) binding.Overlay.color = Color.clear;
                if (binding.Accent != null) binding.Accent.color = Color.clear;
            }
            _rows.Clear();
            _reorderRows = vanillaOrder != null;
            if (rows == null || (vanillaOrder == null && capturedRows == null))
            {
                return;
            }

            if (vanillaOrder != null && vanillaOrder.Count != rows.Count)
            {
                EmiPlugin.Log?.LogWarning(
                    $"[EMI] Recipe row binding count mismatch. Recipes={vanillaOrder.Count}, Rows={rows.Count}");
                return;
            }

            for (int index = 0; index < rows.Count; index++)
            {
                GameObject row = rows[index];
                if (row == null || !(row.transform is RectTransform rectTransform))
                {
                    continue;
                }

                Recipe recipe;
                if (vanillaOrder != null)
                {
                    recipe = vanillaOrder[index];
                }
                else if (!capturedRows.TryGetValue(row, out recipe))
                {
                    continue;
                }

                // 配方名称、智力和图标的索引保持不变。QoL 重用行时也重用同一套装饰。
                Image overlay = FindDecoration(row, "EMIReadyOverlay");
                if (overlay == null)
                {
                    overlay = UiFactory.CreatePanel("EMIReadyOverlay", row.transform, Color.clear);
                    overlay.raycastTarget = false;
                    UiFactory.Stretch(overlay.rectTransform);
                }

                Image accent = FindDecoration(row, "EMIReadyAccent");
                if (accent == null)
                {
                    accent = UiFactory.CreatePanel("EMIReadyAccent", row.transform, Color.clear);
                    accent.raycastTarget = false;
                    UiFactory.Anchor(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(4f, 0f), new Vector2(7f, 54f));
                }

                _rows.Add(new RowBinding
                {
                    Recipe = recipe,
                    RectTransform = rectTransform,
                    Overlay = overlay,
                    Accent = accent,
                    OriginalOrder = index
                });
            }

            Apply();
        }

        private static Image FindDecoration(GameObject row, string name)
        {
            Transform child = row.transform.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
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
            if (_reorderRows)
            {
                _rows.Sort((left, right) =>
                {
                    int priority = GetPriority(left.Recipe).CompareTo(GetPriority(right.Recipe));
                    return priority != 0
                        ? priority
                        : left.OriginalOrder.CompareTo(right.OriginalOrder);
                });
            }

            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                if (binding.RectTransform == null || binding.Overlay == null || binding.Accent == null)
                {
                    continue;
                }

                HighlightPriority priority = GetPriority(binding.Recipe);
                bool highlighted = priority != HighlightPriority.Normal;
                Color highlight = priority == HighlightPriority.RequiredTreeStep
                    ? RequiredStepColor
                    : LeafProducerColor;

                if (_reorderRows)
                {
                    Vector2 position = binding.RectTransform.anchoredPosition;
                    position.y = -index * RecipeRowHeight;
                    binding.RectTransform.anchoredPosition = position;
                }
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
