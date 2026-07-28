using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EMI
{
    internal sealed class CraftingTreeHud : MonoBehaviour
    {
        private sealed class IconVisual
        {
            public Sprite Sprite;
            public Color Color;
        }

        private static readonly Dictionary<ResourceKey, IconVisual> IconCache =
            new Dictionary<ResourceKey, IconVisual>();

        private readonly CraftingTreeModel _model = new CraftingTreeModel();
        private readonly List<GameObject> _treeRows = new List<GameObject>();
        private readonly List<GameObject> _popupRows = new List<GameObject>();

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
        private TextMeshProUGUI _normalTabText;
        private TextMeshProUGUI _treeTabText;
        private bool _treeViewActive;
        private bool _initialized;

        public static CraftingTreeHud Active { get; private set; }

        public static void Attach(PlayerCamera player)
        {
            EmiPlugin.Log?.LogInfo(
                $"[EMI] HUD Attach entered. PlayerPresent={player != null}, " +
                $"CraftingPanelPresent={player != null && player.craftingPanel != null}, ActiveHudPresent={Active != null}");

            if (player == null || player.craftingPanel == null)
            {
                EmiPlugin.Log?.LogError("[EMI] HUD Attach stopped because PlayerCamera or craftingPanel is null.");
                return;
            }

            RectTransform panelRect = player.craftingPanel.transform as RectTransform;
            EmiPlugin.Log?.LogInfo(
                $"[EMI] Crafting panel state: Name={player.craftingPanel.name}, " +
                $"ActiveSelf={player.craftingPanel.activeSelf}, ActiveInHierarchy={player.craftingPanel.activeInHierarchy}, " +
                $"RectSize={(panelRect != null ? panelRect.rect.size.ToString() : "not-a-RectTransform")}");

            if (Active != null)
            {
                if (Active._player == player)
                {
                    EmiPlugin.Log?.LogWarning("[EMI] HUD Attach skipped because this PlayerCamera already has an EMI HUD.");
                    return;
                }

                EmiPlugin.Log?.LogInfo("[EMI] Destroying HUD attached to an older PlayerCamera instance.");
                Destroy(Active.gameObject);
            }

            EmiPlugin.Log?.LogInfo("[EMI] Creating HUD host RectTransform.");
            RectTransform host = UiFactory.CreateRect("EMI", player.craftingPanel.transform);
            UiFactory.Stretch(host);
            EmiPlugin.Log?.LogInfo(
                $"[EMI] HUD host created. SiblingIndex={host.GetSiblingIndex()}, " +
                $"ActiveSelf={host.gameObject.activeSelf}, ActiveInHierarchy={host.gameObject.activeInHierarchy}");
            CraftingTreeHud hud = host.gameObject.AddComponent<CraftingTreeHud>();
            hud.Initialize(player);
        }

        private void Initialize(PlayerCamera player)
        {
            EmiPlugin.Log?.LogInfo("[EMI] HUD Initialize entered.");
            Active = this;
            _player = player;
            _font = player.pinRecipeText != null
                ? player.pinRecipeText.font
                : player.craftingPanel.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
            EmiPlugin.Log?.LogInfo($"[EMI] HUD font resolved. FontPresent={_font != null}");

            if (player.pinRecipeText != null)
            {
                player.pinRecipeText.enabled = false;
                EmiPlugin.Log?.LogInfo("[EMI] Original pinRecipeText renderer disabled.");
            }

            EmiPlugin.Log?.LogInfo("[EMI] Creating HUD interface objects.");
            CreateInterface();
            EmiPlugin.Log?.LogInfo("[EMI] HUD interface objects created.");
            HandlePinnedRecipeChanged(player);
            SetTreeView(false, false);
            _initialized = true;
            LogVisibility("after-initialize");
            EmiPlugin.Log?.LogInfo("[EMI] Crafting tree UI attached to PlayerCamera.");
        }

        private void OnEnable()
        {
            if (_initialized)
            {
                LogVisibility("on-enable");
            }
        }

        private void LogVisibility(string stage)
        {
            EmiPlugin.Log?.LogInfo(
                $"[EMI] HUD visibility ({stage}): ActiveSelf={gameObject.activeSelf}, " +
                $"ActiveInHierarchy={gameObject.activeInHierarchy}, ChildCount={transform.childCount}, " +
                $"TreeOverlayActive={_treeOverlay != null && _treeOverlay.gameObject.activeSelf}, " +
                $"NormalTabActive={_normalTabImage != null && _normalTabImage.gameObject.activeInHierarchy}, " +
                $"TreeTabActive={_treeTabImage != null && _treeTabImage.gameObject.activeInHierarchy}");
        }

        private void OnDestroy()
        {
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
            IconCache.Clear();
            _model.Clear();
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

            if (_treeViewActive)
            {
                RenderTree();
            }
        }

        private void CreateInterface()
        {
            CreateTabs();
            CreateTreeOverlay();
            CreatePopup();
        }

        private void CreateTabs()
        {
            Button normal = UiFactory.CreateButton(
                "NormalTab",
                transform,
                _font,
                EmiText.NormalTab,
                () => SetTreeView(false),
                out _normalTabImage,
                out _normalTabText);
            UiFactory.Anchor(
                normal.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(8f, -18f),
                new Vector2(112f, 40f));
            UiFactory.AddTooltip(normal.gameObject, EmiText.NormalTab, EmiText.NormalTab);

            Button tree = UiFactory.CreateButton(
                "TreeTab",
                transform,
                _font,
                EmiText.TreeTab,
                () => SetTreeView(true),
                out _treeTabImage,
                out _treeTabText);
            UiFactory.Anchor(
                tree.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(8f, -64f),
                new Vector2(112f, 40f));
            UiFactory.AddTooltip(tree.gameObject, EmiText.TreeTab, "EMI");
        }

        private void CreateTreeOverlay()
        {
            Image panel = UiFactory.CreatePanel("TreeOverlay", transform, UiFactory.Black, true);
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
            UiFactory.AddTooltip(close.gameObject, EmiText.Close, string.Empty);

            ScrollRect scroll = UiFactory.CreateScrollView("Options", _popup, out _popupContent);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(5f, 5f);
            scrollRect.offsetMax = new Vector2(-5f, -56f);

            _popup.gameObject.SetActive(false);
        }

        private void SetTreeView(bool active, bool playSound = true)
        {
            _treeViewActive = active;
            _treeOverlay.gameObject.SetActive(active);
            if (!active)
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
                !active);
            UiFactory.SetActiveSprite(
                _treeTabImage,
                _player.uiNano,
                _player.darkenedUiNano,
                active);
            _normalTabText.color = active ? UiFactory.White : UiFactory.Green;
            _treeTabText.color = active ? UiFactory.Green : UiFactory.White;

            if (playSound)
            {
                _player.PlayUISound(PlayerCamera.UISoundType.MiniClick, 1f);
            }
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
                node.IsRoot
                    ? new Color(0.06f, 0.16f, 0.08f, 1f)
                    : (node.Depth % 2 == 0 ? UiFactory.RaisedBlack : UiFactory.Black),
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
                UiFactory.AddTooltip(choose.gameObject, EmiText.ChooseRecipe, GetNodeName(node));
            }

            return background.gameObject;
        }

        private bool CanChoose(CraftingTreeNode node)
        {
            if (node.IsRoot || node.IsCycleBoundary || node.IsSharedReusable)
            {
                return false;
            }

            if (node.IsQualityRequirement)
            {
                return RecipeCatalog.GetCandidates(node.Requirement).Count > 0 || node.Resource.HasValue;
            }

            return node.Resource.HasValue &&
                   (node.SelectedRecipe != null || RecipeCatalog.GetProducers(node.Resource.Value).Count > 0);
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
            List<ResourceCandidate> candidates = RecipeCatalog.GetCandidates(node.Requirement);

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

                        if (!node.IsCycleBoundary && !node.IsSharedReusable &&
                            RecipeCatalog.GetProducers(candidate.Resource).Count > 0)
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
                AddCommandRow(EmiText.ChangeMaterial, () => OpenCandidateChoices(node));
            }

            if (node.SelectedRecipe != null)
            {
                AddCommandRow(EmiText.StopHere, () =>
                {
                    _model.StopExpansion(node);
                    ClosePopup();
                    RenderTree();
                });
            }

            IReadOnlyList<Recipe> producers = RecipeCatalog.GetProducers(node.Resource.Value);
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

        private void AddCommandRow(string label, Action action)
        {
            AddPopupRow(null, label, string.Empty, action, UiFactory.Yellow);
        }

        private void AddInfoRow(string label)
        {
            AddPopupRow(null, label, string.Empty, null, UiFactory.Muted);
        }

        private void AddPopupRow(
            ResourceKey? resource,
            string label,
            string detail,
            Action action,
            Color? accent = null)
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
                string kind;
                if (node.Requirement.isLiquid)
                {
                    kind = Locale.GetOther("craftanyliquid");
                }
                else if (node.Requirement.quality.id == "cutting" || node.Requirement.quality.id == "hammering")
                {
                    kind = Locale.GetOther("craftanytool");
                }
                else
                {
                    kind = Locale.GetOther("craftanyitem");
                }

                name = kind + " | " + node.Requirement.quality.LocaleName;
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
                parts.Add("INT " + node.SelectedRecipe.INT.ToString(CultureInfo.InvariantCulture));
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
                    parts.Add(requirement.quality.LocaleName + " >= " +
                              requirement.quality.amount.ToString("0.#", CultureInfo.InvariantCulture));
                }
            }

            if (node.IsReusable)
            {
                parts.Add(EmiText.Reusable);
            }

            if (node.SelectedRecipe != null && !node.IsRoot)
            {
                parts.Add("<- " + node.SelectedRecipe.simpleName);
            }

            if (node.IsCycleBoundary)
            {
                parts.Add(EmiText.CycleBoundary);
            }
            else if (node.IsSharedReusable)
            {
                parts.Add(EmiText.SharedReusable);
            }
            else if (!node.IsRoot && node.Resource.HasValue && node.SelectedRecipe == null &&
                     RecipeCatalog.GetProducers(node.Resource.Value).Count == 0)
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

            int producers = RecipeCatalog.GetProducers(candidate.Resource).Count;
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
            return "INT " + recipe.INT.ToString(CultureInfo.InvariantCulture) +
                   " | " + output +
                   " | " + recipe.items.Count.ToString(CultureInfo.InvariantCulture) + " " + EmiText.Items;
        }

        private static void ApplyIcon(Image image, ResourceKey? resource)
        {
            if (!resource.HasValue)
            {
                image.sprite = null;
                image.color = new Color(0.12f, 0.14f, 0.13f, 1f);
                return;
            }

            IconVisual visual = GetIcon(resource.Value);
            image.sprite = visual.Sprite;
            image.color = visual.Sprite != null ? visual.Color : new Color(0.12f, 0.14f, 0.13f, 1f);
            image.preserveAspect = true;
        }

        private static IconVisual GetIcon(ResourceKey resource)
        {
            if (IconCache.TryGetValue(resource, out IconVisual cached))
            {
                return cached;
            }

            IconVisual visual = new IconVisual
            {
                Color = Color.white
            };

            try
            {
                if (resource.IsLiquid)
                {
                    visual.Sprite = Resources.Load<Sprite>("Sprites/droplet");
                    if (Liquids.Registry != null &&
                        Liquids.Registry.TryGetValue(resource.Id, out LiquidType liquid))
                    {
                        visual.Color = liquid.color;
                    }
                }
                else
                {
                    GameObject prefab = Resources.Load<GameObject>(resource.Id);
                    SpriteRenderer renderer = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
                    visual.Sprite = renderer != null ? renderer.sprite : null;
                }
            }
            catch (Exception exception)
            {
                EmiPlugin.Log?.LogWarning($"Could not load icon for {resource}: {exception.Message}");
            }

            IconCache[resource] = visual;
            return visual;
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
