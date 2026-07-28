using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EMI
{
    internal sealed class CraftingTreeHud : MonoBehaviour
    {
        private enum HudPage
        {
            Normal,
            Tree,
            Compendium
        }

        private sealed class RecipeRowBinding
        {
            public Recipe Recipe;
            public RectTransform RectTransform;
            public Image Background;
            public Image Overlay;
            public Image Accent;
            public Color OriginalBackground;
            public int OriginalOrder;
        }

        private static readonly Color ReadyTreeRecipeColor =
            new Color(0.34f, 0.78f, 0.46f, 1f);

        private static readonly Color ReadyLeafRecipeColor =
            new Color(0.36f, 0.68f, 0.88f, 1f);

        private static readonly Color ExpandableLeafColor =
            new Color(0.16f, 0.32f, 0.42f, 1f);

        private static readonly Color TerminalLeafColor =
            new Color(0.07f, 0.14f, 0.22f, 1f);

        private readonly CraftingTreeModel _model = new CraftingTreeModel();
        private readonly List<GameObject> _treeRows = new List<GameObject>();
        private readonly List<GameObject> _popupRows = new List<GameObject>();
        private readonly List<RecipeRowBinding> _recipeRows = new List<RecipeRowBinding>();
        private readonly HashSet<int> _readyTreeRecipes = new HashSet<int>();
        private readonly HashSet<int> _readyLeafRecipes = new HashSet<int>();
        // Reused by the half-second inventory refresh to avoid per-refresh allocations.
        private readonly HashSet<int> _nextReadyTreeRecipes = new HashSet<int>();
        private readonly HashSet<int> _nextReadyLeafRecipes = new HashSet<int>();
        private readonly Dictionary<Recipe, bool> _recipeAvailability =
            new Dictionary<Recipe, bool>();

        private PlayerCamera _player;
        private TMP_FontAsset _font;
        private RectTransform _treeOverlay;
        private RectTransform _treeContent;
        private RectTransform _popup;
        private RectTransform _popupContent;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _emptyText;
        private TextMeshProUGUI _popupTitle;
        private Image _normalTabImage;
        private Image _treeTabImage;
        private Image _compendiumTabImage;
        private TextMeshProUGUI _normalTabText;
        private TextMeshProUGUI _treeTabText;
        private TextMeshProUGUI _compendiumTabText;
        private CompendiumPanel _compendium;
        private HudPage _page;
        private float _nextRemainingRefreshTime;
        private string _remainingText = string.Empty;
        private bool _remainingCalculationFailed;

        public static CraftingTreeHud Active { get; private set; }

        public static void Attach(PlayerCamera player)
        {
            if (player == null || player.craftingPanel == null)
            {
                EmiPlugin.Log?.LogError("[EMI] HUD Attach stopped because PlayerCamera or craftingPanel is null.");
                return;
            }

            if (Active != null)
            {
                if (Active._player == player)
                {
                    return;
                }

                Destroy(Active.gameObject);
            }

            RectTransform host = UiFactory.CreateRect("EMI", player.craftingPanel.transform);
            UiFactory.Stretch(host);
            CraftingTreeHud hud = host.gameObject.AddComponent<CraftingTreeHud>();
            hud.Initialize(player);
        }

        private void Initialize(PlayerCamera player)
        {
            Active = this;
            _player = player;
            _font = player.pinRecipeText != null
                ? player.pinRecipeText.font
                : player.craftingPanel.GetComponentInChildren<TextMeshProUGUI>(true)?.font;

            if (player.pinRecipeText != null)
            {
                player.pinRecipeText.enabled = true;
            }

            CreateInterface();
            PreferenceStore.Changed += HandlePreferencesChanged;
            HandlePinnedRecipeChanged(player);
            SetPage(HudPage.Normal, false);
            EmiPlugin.Log?.LogInfo("[EMI] HUD attached.");
        }

        private void OnDestroy()
        {
            PreferenceStore.Changed -= HandlePreferencesChanged;
            if (Active == this)
            {
                Active = null;
            }

            if (_player != null && _player.pinRecipeText != null)
            {
                _player.pinRecipeText.enabled = true;
            }
        }

        public void HandleRecipesRebuilt()
        {
            ResourceIconProvider.Clear();
            _model.Clear();
            _compendium?.HandleCatalogRebuilt();
            HandlePinnedRecipeChanged(_player);
        }

        public void HandlePinnedRecipeChanged(PlayerCamera player)
        {
            if (player == null || player != _player || Recipes.recipes == null)
            {
                return;
            }

            int? pinned = player.pinnedRecipe;
            if (!pinned.HasValue || pinned.Value < 0 || pinned.Value >= Recipes.recipes.Count)
            {
                _model.Clear();
            }
            else
            {
                Recipe pinnedRecipe = Recipes.recipes[pinned.Value];
                if (_model.RootRecipe != pinnedRecipe)
                {
                    _model.SetRoot(pinnedRecipe);
                }
            }

            if (_page == HudPage.Tree)
            {
                RenderTree();
            }

            RefreshRemainingMaterials();
        }

        public void HandlePlayerLateUpdate(PlayerCamera player)
        {
            if (player == null || player != _player || player.pinRecipeText == null)
            {
                return;
            }

            if (Time.unscaledTime >= _nextRemainingRefreshTime)
            {
                RefreshRemainingMaterials();
            }

            player.pinRecipeText.enabled = true;
            player.pinRecipeText.text = _remainingText;
        }

        public void HandleRecipeListRefreshed(
            PlayerCamera player,
            IReadOnlyList<Recipe> recipes,
            IReadOnlyList<GameObject> rows)
        {
            if (player == null || player != _player || recipes == null || rows == null)
            {
                return;
            }

            _recipeRows.Clear();
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

                _recipeRows.Add(new RecipeRowBinding
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

            ApplyRecipeListHighlights();
        }

        private void CreateInterface()
        {
            CreateTabs();
            CreateTreeOverlay();
            CreatePopup();
            _compendium = CompendiumPanel.Create(transform, _font, _player);
        }

        private void CreateTabs()
        {
            Button normal = UiFactory.CreateButton(
                "NormalTab",
                transform,
                _font,
                EmiText.NormalTab,
                () => SetPage(HudPage.Normal),
                out _normalTabImage,
                out _normalTabText);
            UiFactory.Anchor(
                normal.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(8f, -18f),
                new Vector2(112f, 40f));
            UiFactory.AddTooltip(
                normal.gameObject,
                EmiText.NormalTab,
                EmiText.NormalTabDescription);

            Button tree = UiFactory.CreateButton(
                "TreeTab",
                transform,
                _font,
                EmiText.TreeTab,
                () => SetPage(HudPage.Tree),
                out _treeTabImage,
                out _treeTabText);
            UiFactory.Anchor(
                tree.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(8f, -64f),
                new Vector2(112f, 40f));
            UiFactory.AddTooltip(
                tree.gameObject,
                EmiText.TreeTab,
                EmiText.TreeTabDescription);

            Button compendium = UiFactory.CreateButton(
                "CompendiumTab",
                transform,
                _font,
                EmiText.CompendiumTab,
                () => SetPage(HudPage.Compendium),
                out _compendiumTabImage,
                out _compendiumTabText);
            UiFactory.Anchor(
                compendium.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(8f, -110f),
                new Vector2(112f, 40f));
            UiFactory.AddTooltip(
                compendium.gameObject,
                EmiText.CompendiumTab,
                EmiText.CompendiumTabDescription);
        }

        private void CreateTreeOverlay()
        {
            Image panel = UiFactory.CreatePanel("TreeOverlay", transform, UiFactory.Black, true);
            UiFactory.BlockTooltipsBehind(panel.gameObject);
            _treeOverlay = panel.rectTransform;
            _treeOverlay.anchorMin = new Vector2(0.505f, 0.018f);
            _treeOverlay.anchorMax = new Vector2(0.992f, 0.988f);
            _treeOverlay.offsetMin = Vector2.zero;
            _treeOverlay.offsetMax = Vector2.zero;

            Image header = UiFactory.CreatePanel("Header", _treeOverlay, UiFactory.RaisedBlack);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 54f);

            _title = UiFactory.CreateText("Title", header.transform, _font, 22f, TextAlignmentOptions.Left);
            UiFactory.Stretch(_title.rectTransform, 14f, 92f, 4f, 4f);
            _title.text = "EMI";

            Button reset = UiFactory.CreateButton(
                "Reset",
                header.transform,
                _font,
                EmiText.Reset,
                ResetTree,
                out _,
                out _);
            UiFactory.Anchor(
                reset.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f),
                new Vector2(78f, 34f));
            UiFactory.AddTooltip(
                reset.gameObject,
                EmiText.Reset,
                EmiText.ResetDescription);

            ScrollRect scroll = UiFactory.CreateScrollView("TreeScroll", _treeOverlay, out _treeContent);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(6f, 6f);
            scrollRect.offsetMax = new Vector2(-6f, -60f);

            _emptyText = UiFactory.CreateText(
                "Empty",
                _treeOverlay,
                _font,
                24f,
                TextAlignmentOptions.Center);
            UiFactory.Stretch(_emptyText.rectTransform, 24f, 24f, 80f, 80f);
            _emptyText.text = EmiText.NoPinnedRecipe;
        }

        private void CreatePopup()
        {
            Image popup = UiFactory.CreatePanel("SelectionPopup", _treeOverlay, UiFactory.Black, true);
            UiFactory.BlockTooltipsBehind(popup.gameObject);
            _popup = popup.rectTransform;
            UiFactory.Stretch(_popup, 8f, 8f, 8f, 8f);

            Image header = UiFactory.CreatePanel("Header", _popup, UiFactory.RaisedBlack);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 50f);

            _popupTitle = UiFactory.CreateText(
                "Title",
                header.transform,
                _font,
                21f,
                TextAlignmentOptions.Left);
            UiFactory.Stretch(_popupTitle.rectTransform, 12f, 48f, 3f, 3f);

            Button close = UiFactory.CreateButton(
                "Close",
                header.transform,
                _font,
                "X",
                ClosePopup,
                out _,
                out _);
            UiFactory.Anchor(
                close.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-7f, 0f),
                new Vector2(36f, 34f));
            UiFactory.AddTooltip(
                close.gameObject,
                EmiText.Close,
                EmiText.CloseDescription);

            ScrollRect scroll = UiFactory.CreateScrollView("Options", _popup, out _popupContent);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(5f, 5f);
            scrollRect.offsetMax = new Vector2(-5f, -56f);

            _popup.gameObject.SetActive(false);
        }

        private void SetPage(HudPage page, bool playSound = true)
        {
            _page = page;
            bool treeActive = page == HudPage.Tree;
            bool compendiumActive = page == HudPage.Compendium;
            _treeOverlay.gameObject.SetActive(treeActive);
            _compendium?.SetVisible(compendiumActive);
            if (!treeActive)
            {
                ClosePopup();
            }
            else
            {
                RenderTree();
            }

            UiFactory.SetActiveSprite(
                _normalTabImage,
                _player.uiNano,
                _player.darkenedUiNano,
                page == HudPage.Normal);
            UiFactory.SetActiveSprite(
                _treeTabImage,
                _player.uiNano,
                _player.darkenedUiNano,
                treeActive);
            UiFactory.SetActiveSprite(
                _compendiumTabImage,
                _player.uiNano,
                _player.darkenedUiNano,
                compendiumActive);
            _normalTabText.color = page == HudPage.Normal ? UiFactory.Green : UiFactory.White;
            _treeTabText.color = treeActive ? UiFactory.Green : UiFactory.White;
            _compendiumTabText.color = compendiumActive ? UiFactory.Green : UiFactory.White;

            if (playSound)
            {
                _player.PlayUISound(PlayerCamera.UISoundType.MiniClick, 1f);
            }
        }

        private void HandlePreferencesChanged()
        {
            _model.EvaluateBoundaries();
            if (_page == HudPage.Tree)
            {
                RenderTree();
            }

            _compendium?.HandlePreferencesChanged();
            RefreshRemainingMaterials();
        }

        public bool ShouldCaptureCompendiumMouseInteraction()
        {
            return _page == HudPage.Compendium &&
                   _compendium != null &&
                   _compendium.IsPointerOverResourceEntry();
        }

        public bool TryGetForegroundCursor(out int cursor)
        {
            cursor = 0;
            foreach (RaycastResult hit in UIUtil.GetEventSystemRaycastResults(null))
            {
                if (hit.gameObject.layer != LayerMask.NameToLayer("UI"))
                {
                    continue;
                }

                if (!hit.gameObject.transform.IsChildOf(transform))
                {
                    return false;
                }

                Button button = hit.gameObject.GetComponentInParent<Button>();
                CompendiumResourceClickTarget resource =
                    hit.gameObject.GetComponentInParent<CompendiumResourceClickTarget>();
                bool interactive =
                    (resource != null && resource.transform.IsChildOf(transform)) ||
                    (button != null && button.interactable && button.transform.IsChildOf(transform));
                cursor = interactive ? 4 : 3;
                return true;
            }

            return false;
        }

        private void ResetTree()
        {
            _model.ResetSelections();
            ClosePopup();
            RenderTree();
            _player.PlayUISound(PlayerCamera.UISoundType.MiniClick, 1f);
        }

        private void RenderTree()
        {
            ClearObjects(_treeRows);
            ClosePopup();

            CraftingTreeNode root = _model.Root;
            bool hasRoot = root != null;
            _emptyText.gameObject.SetActive(!hasRoot);
            _title.text = hasRoot ? "EMI | " + root.Resource.Value.DisplayName : "EMI";

            if (hasRoot)
            {
                _model.EvaluateBoundaries();
                RenderNode(root);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_treeContent);
            RefreshRemainingMaterials();
        }

        private void RefreshRemainingMaterials()
        {
            _nextRemainingRefreshTime = Time.unscaledTime + 0.5f;
            if (_player == null || _player.pinRecipeText == null)
            {
                return;
            }

            CraftingTreeNode root = _model.Root;
            if (root == null || _player.body == null)
            {
                _remainingText = string.Empty;
                ClearReadyRecipes();
                _player.pinRecipeText.text = _remainingText;
                return;
            }

            try
            {
                _model.EvaluateBoundaries();
                CraftingPlanResult plan = RemainingMaterialsCalculator.Calculate(root, _player.body);
                _remainingText = BuildRemainingText(
                    root,
                    plan,
                    _player.body.skills.INT);
                UpdateReadyRecipes(plan);
                _remainingCalculationFailed = false;
            }
            catch (Exception exception)
            {
                if (!_remainingCalculationFailed)
                {
                    EmiPlugin.Log?.LogError($"[EMI] Remaining materials calculation failed:\n{exception}");
                    _remainingCalculationFailed = true;
                }

                _remainingText = root.SelectedRecipe.fullName + ":";
                ClearReadyRecipes();
            }

            _player.pinRecipeText.enabled = true;
            _player.pinRecipeText.text = _remainingText;
        }

        private void UpdateReadyRecipes(CraftingPlanResult plan)
        {
            _nextReadyTreeRecipes.Clear();
            _nextReadyLeafRecipes.Clear();
            _recipeAvailability.Clear();

            foreach (Recipe recipe in plan.RequiredRecipes)
            {
                if (IsRecipeReady(recipe, _recipeAvailability))
                {
                    _nextReadyTreeRecipes.Add(recipe.index);
                }
            }

            foreach (Recipe recipe in plan.LeafProducerRecipes)
            {
                if (!_nextReadyTreeRecipes.Contains(recipe.index) &&
                    IsRecipeReady(recipe, _recipeAvailability))
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
            ApplyRecipeListHighlights();
        }

        private static bool IsRecipeReady(
            Recipe recipe,
            Dictionary<Recipe, bool> availability)
        {
            if (recipe == null || !recipe.visible)
            {
                return false;
            }

            if (!availability.TryGetValue(recipe, out bool ready))
            {
                ready = recipe.GetItemsForRecipe() != null;
                availability.Add(recipe, ready);
            }

            return ready;
        }

        private void ClearReadyRecipes()
        {
            if (_readyTreeRecipes.Count == 0 && _readyLeafRecipes.Count == 0)
            {
                return;
            }

            _readyTreeRecipes.Clear();
            _readyLeafRecipes.Clear();
            ApplyRecipeListHighlights();
        }

        private void ApplyRecipeListHighlights()
        {
            _recipeRows.Sort((left, right) =>
            {
                int priority = RecipePriority(left.Recipe).CompareTo(RecipePriority(right.Recipe));
                return priority != 0
                    ? priority
                    : left.OriginalOrder.CompareTo(right.OriginalOrder);
            });

            for (int index = 0; index < _recipeRows.Count; index++)
            {
                RecipeRowBinding binding = _recipeRows[index];
                if (binding.RectTransform == null || binding.Background == null ||
                    binding.Overlay == null || binding.Accent == null)
                {
                    continue;
                }

                int priority = RecipePriority(binding.Recipe);
                Color highlight = priority == 0
                    ? ReadyTreeRecipeColor
                    : ReadyLeafRecipeColor;
                bool highlighted = priority < 2;

                Vector2 position = binding.RectTransform.anchoredPosition;
                position.y = -index * 64f;
                binding.RectTransform.anchoredPosition = position;
                binding.Background.color = binding.OriginalBackground;
                binding.Overlay.color = highlighted
                    ? new Color(highlight.r, highlight.g, highlight.b, 0.34f)
                    : Color.clear;
                binding.Accent.color = highlighted ? highlight : Color.clear;
            }
        }

        private int RecipePriority(Recipe recipe)
        {
            if (recipe != null && _readyTreeRecipes.Contains(recipe.index))
            {
                return 0;
            }

            return recipe != null && _readyLeafRecipes.Contains(recipe.index) ? 1 : 2;
        }

        private static string BuildRemainingText(
            CraftingTreeNode root,
            CraftingPlanResult plan,
            int currentInt)
        {
            StringBuilder text = new StringBuilder();
            text.Append(root.SelectedRecipe.fullName)
                .Append(":\n");

            AppendCraftingIntWarning(text, plan.RequiredRecipes, currentInt);

            text.Append("<color=#8D948F>")
                .Append(EmiText.RemainingMaterials)
                .Append("</color>\n");

            if (plan.RemainingMaterials.Count == 0)
            {
                text.Append("<color=#59FF59><sprite index=23>")
                    .Append(EmiText.MaterialsReady)
                    .Append("</color>");
                return text.ToString();
            }

            foreach (RemainingMaterial material in plan.RemainingMaterials)
            {
                text.Append("<color=#FFFFFF><sprite index=24>")
                    .Append(FormatRemainingMaterial(material))
                    .Append("</color>\n");
            }

            return text.ToString().TrimEnd('\n');
        }

        private static void AppendCraftingIntWarning(
            StringBuilder text,
            IEnumerable<Recipe> requiredRecipes,
            int currentInt)
        {
            // RequiredRecipes contains only producers still needed after inventory allocation.
            int requiredInt = currentInt;
            foreach (Recipe recipe in requiredRecipes)
            {
                if (recipe != null && recipe.INT > requiredInt)
                {
                    requiredInt = recipe.INT;
                }
            }

            int deficit = requiredInt - currentInt;
            if (deficit <= 0)
            {
                return;
            }

            bool impossible = deficit > 3;
            text.Append(impossible ? "<color=#FF4740>" : "<color=#FFDB38>")
                .Append(EmiText.FormatCraftingIntWarning(requiredInt, impossible))
                .Append("</color>\n");
        }

        private static string FormatRemainingMaterial(RemainingMaterial material)
        {
            RecipeItem requirement = material.Requirement;
            string name;
            switch (material.Kind)
            {
                case RemainingMaterialKind.ConcreteItem:
                case RemainingMaterialKind.ConcreteLiquid:
                    name = material.Resource.HasValue ? material.Resource.Value.DisplayName : "?";
                    break;

                case RemainingMaterialKind.QualityLiquid:
                    name = EmiText.FormatQualityLiquidRequirement(
                        requirement.quality.LocaleName,
                        material.Amount.ToString("0.#", CultureInfo.InvariantCulture));
                    break;

                default:
                    name = EmiText.FormatQualityItem(
                        requirement.quality.LocaleName,
                        DurabilityRequirement.IsQualityTool(requirement));
                    break;
            }

            if (material.Kind == RemainingMaterialKind.ConcreteLiquid)
            {
                name += " (" + material.Amount.ToString("0.#", CultureInfo.InvariantCulture) + "mL)";
            }
            else if (material.UsesDurability)
            {
                name += " (" + EmiText.FormatRequiredUses(material.ItemCount) + ")";
            }
            else if (material.Kind != RemainingMaterialKind.QualityLiquid && material.ItemCount > 1)
            {
                name += " (x" + material.ItemCount.ToString(CultureInfo.InvariantCulture) + ")";
            }

            if (requirement != null && !requirement.isLiquid && requirement.minimumCondition > 0f)
            {
                name += " [" + (requirement.minimumCondition * 100f)
                    .ToString("0.#", CultureInfo.InvariantCulture) + "%+]";
            }

            if (material.UsesDurability)
            {
                name += " | " + EmiText.ConsumesDurability;
            }

            return name;
        }

        private void RenderNode(CraftingTreeNode node)
        {
            GameObject row = CreateTreeRow(node);
            _treeRows.Add(row);

            if (!node.CanShowChildren)
            {
                return;
            }

            foreach (CraftingTreeNode child in node.Children)
            {
                RenderNode(child);
            }
        }

        private GameObject CreateTreeRow(CraftingTreeNode node)
        {
            Image background = UiFactory.CreatePanel(
                "Node_" + node.Depth.ToString(CultureInfo.InvariantCulture),
                _treeContent,
                GetTreeRowColor(node),
                true);
            LayoutElement layout = background.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 54f;
            layout.preferredHeight = 54f;
            layout.flexibleHeight = 0f;

            float indent = 10f + node.Depth * 24f;
            if (node.Depth > 0)
            {
                Image branch = UiFactory.CreatePanel("Branch", background.transform, UiFactory.Muted);
                UiFactory.Anchor(
                    branch.rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(indent - 10f, 0f),
                    new Vector2(12f, 1f));
            }

            Image icon = UiFactory.CreatePanel("Icon", background.transform, Color.white, true);
            UiFactory.Anchor(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(indent + 4f, 0f),
                new Vector2(38f, 38f));
            ApplyIcon(icon, node.Resource);

            TextMeshProUGUI label = UiFactory.CreateText(
                "Label",
                background.transform,
                _font,
                19f,
                TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(indent + 50f, 3f);
            label.rectTransform.offsetMax = new Vector2(-58f, -3f);
            label.text = GetNodeName(node) + "\n<size=13><color=#8D948F>" + GetNodeDetail(node) + "</color></size>";

            if (CanChoose(node))
            {
                Button choose = UiFactory.CreateButton(
                    "Choose",
                    background.transform,
                    _font,
                    "...",
                    () => OpenNodeChoices(node),
                    out _,
                    out _);
                UiFactory.Anchor(
                    choose.GetComponent<RectTransform>(),
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-7f, 0f),
                    new Vector2(42f, 36f));
                bool choosesMaterial = node.IsQualityRequirement && node.SelectedCandidate == null;
                string tooltipTitle;
                string tooltipDescription;
                if (node.IsRecipeLocked)
                {
                    tooltipTitle = EmiText.RecipeLocked;
                    tooltipDescription = EmiText.RecipeLockedDescription;
                }
                else
                {
                    tooltipTitle = choosesMaterial
                        ? EmiText.ChooseMaterial
                        : EmiText.ChooseRecipe;
                    tooltipDescription = EmiText.FormatChooseDescription(
                        GetNodeName(node),
                        choosesMaterial);
                }

                UiFactory.AddTooltip(
                    choose.gameObject,
                    tooltipTitle,
                    tooltipDescription);
            }

            return background.gameObject;
        }

        private Color GetTreeRowColor(CraftingTreeNode node)
        {
            if (node.IsRoot)
            {
                return new Color(0.06f, 0.16f, 0.08f, 1f);
            }

            bool expanded = node.SelectedRecipe != null &&
                            !node.IsCycleBoundary;
            if (expanded)
            {
                return node.Depth % 2 == 0 ? UiFactory.RaisedBlack : UiFactory.Black;
            }

            return CanExpandLeaf(node) ? ExpandableLeafColor : TerminalLeafColor;
        }

        private bool CanExpandLeaf(CraftingTreeNode node)
        {
            if (node.IsRoot || node.IsCycleBoundary)
            {
                return false;
            }

            if (node.Resource.HasValue)
            {
                return RecipeCatalog.GetCompatibleProducers(
                    node.Resource.Value,
                    node.Requirement).Count > 0;
            }

            if (!node.IsQualityRequirement)
            {
                return false;
            }

            foreach (ResourceCandidate candidate in _model.GetSharedCandidates(node))
            {
                if (RecipeCatalog.GetCompatibleProducers(
                        candidate.Resource,
                        node.Requirement).Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanChoose(CraftingTreeNode node)
        {
            if (node.IsRoot || node.IsCycleBoundary)
            {
                return false;
            }

            if (node.IsQualityRequirement)
            {
                return RecipeCatalog.GetCandidates(node.Requirement).Count > 0 || node.Resource.HasValue;
            }

            return node.Resource.HasValue &&
                   (node.SelectedRecipe != null ||
                    RecipeCatalog.GetCompatibleProducers(node.Resource.Value, node.Requirement).Count > 0);
        }

        private void OpenNodeChoices(CraftingTreeNode node)
        {
            if (node.IsQualityRequirement && node.SelectedCandidate == null)
            {
                OpenCandidateChoices(node);
                return;
            }

            OpenRecipeChoices(node);
        }

        private void OpenCandidateChoices(CraftingTreeNode node)
        {
            OpenPopup(EmiText.ChooseMaterial);
            if (node.IsCandidateLocked)
            {
                AddInfoRow(EmiText.CandidateLocked, EmiText.CandidateLockedDescription);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_popupContent);
                return;
            }

            List<ResourceCandidate> candidates = _model.GetSharedCandidates(node);

            if (node.SelectedCandidate != null)
            {
                AddCommandRow(
                    EmiText.UseQualityRequirement,
                    EmiText.UseQualityRequirementDescription,
                    () =>
                {
                    _model.ClearCandidate(node);
                    ClosePopup();
                    RenderTree();
                });
            }

            foreach (ResourceCandidate candidate in candidates)
            {
                string detail = CandidateDetail(node, candidate);
                AddPopupRow(
                    candidate.Resource,
                    candidate.Resource.DisplayName,
                    detail,
                    () =>
                    {
                        _model.SelectCandidate(node, candidate);
                        RenderTree();

                        if (!node.IsCycleBoundary &&
                            RecipeCatalog.GetCompatibleProducers(candidate.Resource, node.Requirement).Count > 0)
                        {
                            OpenRecipeChoices(node);
                        }
                    });
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_popupContent);
        }

        private void OpenRecipeChoices(CraftingTreeNode node)
        {
            if (!node.Resource.HasValue)
            {
                OpenCandidateChoices(node);
                return;
            }

            OpenPopup(EmiText.ChooseRecipe + " | " + node.Resource.Value.DisplayName);

            if (node.IsQualityRequirement)
            {
                if (node.IsCandidateLocked)
                {
                    AddInfoRow(EmiText.CandidateLocked, EmiText.CandidateLockedDescription);
                }
                else
                {
                    AddCommandRow(
                        EmiText.ChangeMaterial,
                        EmiText.ChangeMaterialDescription,
                        () => OpenCandidateChoices(node));
                }
            }

            if (node.IsRecipeLocked)
            {
                AddInfoRow(EmiText.RecipeLocked, EmiText.RecipeLockedDescription);
                if (node.SelectedRecipe != null)
                {
                    AddPopupRow(
                        node.Resource.Value,
                        node.SelectedRecipe.simpleName,
                        GetRecipeDetail(node.SelectedRecipe),
                        null,
                        UiFactory.Green,
                        EmiText.RecipeLockedDescription);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(_popupContent);
                return;
            }

            if (node.SelectedRecipe != null)
            {
                AddCommandRow(
                    EmiText.StopHere,
                    EmiText.StopHereDescription,
                    () =>
                {
                    _model.StopExpansion(node);
                    ClosePopup();
                    RenderTree();
                });
            }

            IReadOnlyList<Recipe> producers =
                RecipeCatalog.GetCompatibleProducers(node.Resource.Value, node.Requirement);
            foreach (Recipe producer in producers)
            {
                Recipe captured = producer;
                string detail = GetRecipeDetail(captured);
                AddPopupRow(
                    node.Resource.Value,
                    captured.simpleName,
                    detail,
                    () =>
                    {
                        _model.SelectRecipe(node, captured);
                        ClosePopup();
                        RenderTree();
                    });
            }

            if (producers.Count == 0)
            {
                AddInfoRow(EmiText.RawMaterial);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_popupContent);
        }

        private void OpenPopup(string title)
        {
            ClearObjects(_popupRows);
            _popupTitle.text = title;
            _popup.gameObject.SetActive(true);
            _popup.SetAsLastSibling();
        }

        private void ClosePopup()
        {
            if (_popup != null)
            {
                _popup.gameObject.SetActive(false);
            }
        }

        private void AddCommandRow(string label, string description, Action action)
        {
            AddPopupRow(
                null,
                label,
                string.Empty,
                action,
                UiFactory.Yellow,
                description);
        }

        private void AddInfoRow(string label, string description = null)
        {
            AddPopupRow(null, label, string.Empty, null, UiFactory.Muted, description);
        }

        private void AddPopupRow(
            ResourceKey? resource,
            string label,
            string detail,
            Action action,
            Color? accent = null,
            string tooltipDescription = null)
        {
            Image background = UiFactory.CreatePanel("Option", _popupContent, UiFactory.RaisedBlack, true);
            LayoutElement layout = background.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;
            layout.flexibleHeight = 0f;

            if (action != null)
            {
                Button button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => action());
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.7f, 0.8f, 0.72f, 1f);
                colors.pressedColor = new Color(0.32f, 0.85f, 0.38f, 1f);
                button.colors = colors;
            }

            UiFactory.AddTooltip(
                background.gameObject,
                label,
                tooltipDescription ?? detail);

            Image icon = UiFactory.CreatePanel("Icon", background.transform, Color.white, true);
            UiFactory.Anchor(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(9f, 0f),
                new Vector2(40f, 40f));
            ApplyIcon(icon, resource);

            TextMeshProUGUI text = UiFactory.CreateText(
                "Text",
                background.transform,
                _font,
                19f,
                TextAlignmentOptions.Left);
            UiFactory.Stretch(text.rectTransform, 58f, 8f, 3f, 3f);
            text.color = accent ?? UiFactory.White;
            text.text = string.IsNullOrEmpty(detail)
                ? label
                : label + "\n<size=13><color=#8D948F>" + detail + "</color></size>";

            _popupRows.Add(background.gameObject);
        }

        private string GetNodeName(CraftingTreeNode node)
        {
            string name;
            if (node.Resource.HasValue)
            {
                name = node.Resource.Value.DisplayName;
            }
            else if (node.Requirement == null || node.Requirement.quality == null)
            {
                name = "?";
            }
            else
            {
                if (node.Requirement.isLiquid)
                {
                    name = EmiText.FormatQualityLiquid(node.Requirement.quality.LocaleName);
                }
                else if (DurabilityRequirement.IsQualityTool(node.Requirement))
                {
                    name = EmiText.FormatQualityItem(node.Requirement.quality.LocaleName, true);
                }
                else
                {
                    name = EmiText.FormatQualityItem(node.Requirement.quality.LocaleName, false);
                }
            }

            if (node.IsRoot)
            {
                return name;
            }

            if (node.Requirement != null && node.Requirement.isLiquid && node.RequiredLiquidAmount > 0f)
            {
                return name + " (" +
                       node.RequiredLiquidAmount.ToString("0.#", CultureInfo.InvariantCulture) + "mL)";
            }

            if (node.UsesDurability)
            {
                return name + " (" + EmiText.FormatRequiredUses(node.RequiredItemCount) + ")";
            }

            return node.RequiredItemCount > 1
                ? name + " (x" + node.RequiredItemCount.ToString(CultureInfo.InvariantCulture) + ")"
                : name;
        }

        private string GetNodeDetail(CraftingTreeNode node)
        {
            List<string> parts = new List<string>();

            if (node.IsRoot)
            {
                parts.Add(EmiText.Root);
                parts.Add(EmiText.FormatIntLevel(node.SelectedRecipe.INT));
            }
            else if (node.Requirement != null)
            {
                RecipeItem requirement = node.Requirement;
                if (!requirement.isLiquid && requirement.minimumCondition > 0f)
                {
                    parts.Add((requirement.minimumCondition * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%+");
                }

                if (!requirement.specific && requirement.quality != null)
                {
                    if (requirement.isLiquid && !node.Resource.HasValue)
                    {
                        float totalQuality = requirement.quality.amount *
                                             node.RequirementMultiplicity *
                                             node.ParentCraftRuns;
                        parts.Add(EmiText.FormatQualityAmountRequired(
                            totalQuality.ToString("0.#", CultureInfo.InvariantCulture)));
                    }
                    else if (requirement.quality.amount > 1f)
                    {
                        parts.Add(requirement.quality.LocaleName + " >= " +
                                  requirement.quality.amount.ToString("0.#", CultureInfo.InvariantCulture));
                    }
                }
            }

            if (node.UsesDurability)
            {
                parts.Add(EmiText.ConsumesDurability);
            }

            if (node.SelectedRecipe != null && !node.IsRoot)
            {
                parts.Add(EmiText.MadeUsingSelectedRecipe);
            }

            if (node.IsCandidateLocked)
            {
                parts.Add(EmiText.CandidateLocked);
            }

            if (node.IsRecipeLocked)
            {
                parts.Add(EmiText.RecipeLocked);
            }

            if (node.IsCycleBoundary)
            {
                parts.Add(EmiText.CycleBoundary);
            }
            else if (!node.IsRoot && node.Resource.HasValue && node.SelectedRecipe == null &&
                     RecipeCatalog.GetCompatibleProducers(node.Resource.Value, node.Requirement).Count == 0)
            {
                parts.Add(EmiText.RawMaterial);
            }

            return string.Join(" | ", parts);
        }

        private string CandidateDetail(CraftingTreeNode node, ResourceCandidate candidate)
        {
            List<string> parts = new List<string>();
            if (candidate.RequiredLiquidAmount > 0f)
            {
                float totalAmount = node.RequiredLiquidAmountFor(candidate);
                parts.Add(totalAmount.ToString("0.#", CultureInfo.InvariantCulture) + "mL");
            }

            int producers = RecipeCatalog.GetCompatibleProducers(candidate.Resource, node.Requirement).Count;
            parts.Add(producers > 0
                ? producers.ToString(CultureInfo.InvariantCulture) + " " + EmiText.ChooseRecipe
                : EmiText.RawMaterial);
            return string.Join(" | ", parts);
        }

        private static string GetRecipeDetail(Recipe recipe)
        {
            string output = recipe.result.isLiquid
                ? recipe.result.resultCondition.ToString("0.#", CultureInfo.InvariantCulture) + "mL"
                : "x" + recipe.result.amount.ToString(CultureInfo.InvariantCulture);
            return EmiText.FormatIntLevel(recipe.INT) +
                   " | " + output +
                   " | " + recipe.items.Count.ToString(CultureInfo.InvariantCulture) + " " + EmiText.Items;
        }

        private static void ApplyIcon(Image image, ResourceKey? resource)
        {
            ResourceIconProvider.Apply(image, resource);
        }

        private static void ClearObjects(List<GameObject> objects)
        {
            foreach (GameObject gameObject in objects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                gameObject.SetActive(false);
                Destroy(gameObject);
            }

            objects.Clear();
        }
    }
}
