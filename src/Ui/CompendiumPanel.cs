using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EMI
{
    internal sealed class CompendiumPanel : MonoBehaviour
    {
        private enum CatalogPage
        {
            Resources,
            Qualities
        }

        private sealed class ResourceCell
        {
            public GameObject GameObject;
            public ResourceKey Resource;
        }

        private readonly List<ResourceCell> _resourceCells = new List<ResourceCell>();
        private readonly List<GameObject> _bodyObjects = new List<GameObject>();
        private readonly Dictionary<string, float> _scrollPositions =
            new Dictionary<string, float>();

        private PlayerCamera _player;
        private TMP_FontAsset _font;
        private RectTransform _root;
        private RectTransform _body;
        private TextMeshProUGUI _headerTitle;
        private Image _resourcesTabImage;
        private Image _qualitiesTabImage;
        private TextMeshProUGUI _resourcesTabText;
        private TextMeshProUGUI _qualitiesTabText;
        private TMP_InputField _searchInput;
        private RectTransform _gridContent;
        private ScrollRect _activeScroll;
        private string _activeScrollKey;
        private CatalogPage _page;
        private ResourceKey? _selectedResource;
        private bool _showConsumers;
        private string _selectedQuality;
        private string _searchTerm = string.Empty;

        public static CompendiumPanel Create(
            Transform parent,
            TMP_FontAsset font,
            PlayerCamera player)
        {
            Image panel = UiFactory.CreatePanel("CompendiumOverlay", parent, UiFactory.Black, true);
            UiFactory.BlockTooltipsBehind(panel.gameObject);
            RectTransform root = panel.rectTransform;
            root.anchorMin = new Vector2(0.505f, 0.018f);
            root.anchorMax = new Vector2(0.992f, 0.988f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            CompendiumPanel compendium = panel.gameObject.AddComponent<CompendiumPanel>();
            compendium.Initialize(root, font, player);
            return compendium;
        }

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            if (_root == null)
            {
                return;
            }

            _root.gameObject.SetActive(visible);
            if (visible)
            {
                RenderCurrentPage();
            }
        }

        public void HandleCatalogRebuilt()
        {
            _selectedResource = null;
            _selectedQuality = null;
            _resourceCells.Clear();
            if (IsVisible)
            {
                RenderCurrentPage();
            }
        }

        public void HandlePreferencesChanged()
        {
            if (IsVisible && (_selectedResource.HasValue || !string.IsNullOrEmpty(_selectedQuality)))
            {
                RenderCurrentPage();
            }
        }

        public bool IsPointerOverResourceEntry()
        {
            if (!IsVisible)
            {
                return false;
            }

            foreach (RaycastResult hit in UIUtil.GetEventSystemRaycastResults(null))
            {
                CompendiumResourceClickTarget target =
                    hit.gameObject.GetComponentInParent<CompendiumResourceClickTarget>();
                if (target != null && target.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void Initialize(RectTransform root, TMP_FontAsset font, PlayerCamera player)
        {
            _root = root;
            _font = font;
            _player = player;
            CreateHeader();

            _body = UiFactory.CreateRect("Body", _root);
            UiFactory.Stretch(_body, 6f, 6f, 6f, 60f);

            _page = CatalogPage.Resources;
            _root.gameObject.SetActive(false);
        }

        private void CreateHeader()
        {
            Image header = UiFactory.CreatePanel("Header", _root, UiFactory.RaisedBlack);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 54f);

            Button resources = UiFactory.CreateButton(
                "ResourcesTab",
                header.transform,
                _font,
                EmiText.ResourceCatalog,
                () => SetPage(CatalogPage.Resources),
                out _resourcesTabImage,
                out _resourcesTabText);
            UiFactory.Anchor(
                resources.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(8f, 0f),
                new Vector2(102f, 36f));
            UiFactory.AddTooltip(
                resources.gameObject,
                EmiText.ResourceCatalog,
                EmiText.ResourceCatalogDescription);

            Button qualities = UiFactory.CreateButton(
                "QualitiesTab",
                header.transform,
                _font,
                EmiText.QualityCatalog,
                () => SetPage(CatalogPage.Qualities),
                out _qualitiesTabImage,
                out _qualitiesTabText);
            UiFactory.Anchor(
                qualities.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(116f, 0f),
                new Vector2(102f, 36f));
            UiFactory.AddTooltip(
                qualities.gameObject,
                EmiText.QualityCatalog,
                EmiText.QualityCatalogDescription);

            _headerTitle = UiFactory.CreateText(
                "Title",
                header.transform,
                _font,
                18f,
                TextAlignmentOptions.Right);
            UiFactory.Stretch(_headerTitle.rectTransform, 224f, 10f, 4f, 4f);
            _headerTitle.text = EmiText.CompendiumTab;
        }

        private void SetPage(CatalogPage page)
        {
            _page = page;
            _selectedResource = null;
            _selectedQuality = null;
            RenderCurrentPage();
            PlayClick();
        }

        private void RenderCurrentPage()
        {
            ClearBody();
            UpdateTabs();

            if (_page == CatalogPage.Resources)
            {
                if (_selectedResource.HasValue)
                {
                    RenderRecipeQuery(_selectedResource.Value, _showConsumers);
                }
                else
                {
                    RenderResourceGrid();
                }
            }
            else if (!string.IsNullOrEmpty(_selectedQuality))
            {
                RenderQualityCandidates(_selectedQuality);
            }
            else
            {
                RenderQualityList();
            }
        }

        private void UpdateTabs()
        {
            bool resourcesActive = _page == CatalogPage.Resources;
            UiFactory.SetActiveSprite(
                _resourcesTabImage,
                _player.uiNano,
                _player.darkenedUiNano,
                resourcesActive);
            UiFactory.SetActiveSprite(
                _qualitiesTabImage,
                _player.uiNano,
                _player.darkenedUiNano,
                !resourcesActive);
            _resourcesTabText.color = resourcesActive ? UiFactory.Green : UiFactory.White;
            _qualitiesTabText.color = resourcesActive ? UiFactory.White : UiFactory.Green;
        }

        private void RenderResourceGrid()
        {
            _headerTitle.text = EmiText.ResourceCatalog;

            TMP_InputField search = UiFactory.CreateInputField(
                "Search",
                _body,
                _font,
                EmiText.Search,
                OnSearchChanged,
                out Image searchBackground);
            _searchInput = search;
            _searchInput.SetTextWithoutNotify(_searchTerm);
            UiFactory.Anchor(
                searchBackground.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 40f));
            searchBackground.rectTransform.anchorMin = new Vector2(0f, 1f);
            searchBackground.rectTransform.anchorMax = new Vector2(1f, 1f);
            searchBackground.rectTransform.offsetMin = new Vector2(0f, -40f);
            searchBackground.rectTransform.offsetMax = Vector2.zero;
            UiFactory.AddTooltip(search.gameObject, EmiText.Search, EmiText.SearchDescription);
            Track(search.gameObject);

            Button clear = UiFactory.CreateButton(
                "ClearSearch",
                search.transform,
                _font,
                "X",
                ClearSearch,
                out _,
                out _);
            UiFactory.Anchor(
                clear.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-4f, 0f),
                new Vector2(28f, 28f));
            UiFactory.AddTooltip(clear.gameObject, EmiText.ClearSearch, EmiText.ClearSearchDescription);

            Canvas.ForceUpdateCanvases();
            const float cellSize = 56f;
            const float spacing = 4f;
            const float horizontalPadding = 8f;
            int columns = Mathf.Max(
                1,
                Mathf.FloorToInt(
                    (_body.rect.width - horizontalPadding + spacing) /
                    (cellSize + spacing)));
            ScrollRect grid = UiFactory.CreateGridScrollView(
                "Resources",
                _body,
                columns,
                new Vector2(cellSize, cellSize),
                out _gridContent);
            RectTransform gridRect = grid.GetComponent<RectTransform>();
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = new Vector2(0f, -46f);
            Track(grid.gameObject);

            _resourceCells.Clear();
            foreach (ResourceKey resource in CompendiumCatalog.AllResources)
            {
                CreateResourceCell(resource);
            }

            ApplyResourceSearch();
            RestoreScrollPosition(grid, _gridContent, "resources");
        }

        private void CreateResourceCell(ResourceKey resource)
        {
            Image background = UiFactory.CreatePanel(
                "Resource_" + resource.Id,
                _gridContent,
                UiFactory.RaisedBlack,
                true);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.68f, 0.76f, 0.7f, 1f);
            colors.pressedColor = new Color(0.3f, 0.8f, 0.36f, 1f);
            button.colors = colors;

            Image icon = UiFactory.CreatePanel("Icon", background.transform, Color.white);
            icon.raycastTarget = false;
            UiFactory.Stretch(icon.rectTransform, 6f, 6f, 6f, 6f);
            ResourceIconProvider.Apply(icon, resource);

            CompendiumResourceClickTarget click =
                background.gameObject.AddComponent<CompendiumResourceClickTarget>();
            ResourceKey captured = resource;
            click.Initialize(
                () => OpenResource(captured, false),
                () => OpenResource(captured, true));
            UiFactory.AddTooltip(background.gameObject, resource.DisplayName, resource.Description);

            _resourceCells.Add(new ResourceCell
            {
                GameObject = background.gameObject,
                Resource = resource
            });
        }

        private void OnSearchChanged(string value)
        {
            _searchTerm = value ?? string.Empty;
            ApplyResourceSearch();
        }

        private void ClearSearch()
        {
            _searchTerm = string.Empty;
            _searchInput?.SetTextWithoutNotify(string.Empty);
            ApplyResourceSearch();
            PlayClick();
        }

        private void ApplyResourceSearch()
        {
            string query = _searchTerm.Trim();
            foreach (ResourceCell cell in _resourceCells)
            {
                bool visible = string.IsNullOrEmpty(query) ||
                               ContainsLocalized(cell.Resource.DisplayName, query) ||
                               cell.Resource.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                cell.GameObject.SetActive(visible);
            }

            if (_gridContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_gridContent);
            }
        }

        private void OpenResource(ResourceKey resource, bool consumers)
        {
            _selectedResource = resource;
            _showConsumers = consumers;
            RenderCurrentPage();
            PlayClick();
        }

        private void RenderRecipeQuery(ResourceKey resource, bool consumers)
        {
            _headerTitle.text = resource.DisplayName;
            CreateBackBar(consumers ? EmiText.ConsumerRecipes : EmiText.ProducerRecipes, BackToResources);

            ScrollRect scroll = UiFactory.CreateScrollView("RecipeResults", _body, out RectTransform content);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = new Vector2(0f, -48f);
            Track(scroll.gameObject);

            IReadOnlyList<Recipe> recipes = consumers
                ? CompendiumCatalog.GetConsumers(resource)
                : CompendiumCatalog.GetProducers(resource);
            foreach (Recipe recipe in recipes)
            {
                CreateRecipeRow(content, recipe);
            }

            if (recipes.Count == 0)
            {
                CreateEmptyText(EmiText.NoRecipes);
            }

            RestoreScrollPosition(
                scroll,
                content,
                "recipes:" + resource + ":" + (consumers ? "consumers" : "producers"));
        }

        private void CreateRecipeRow(RectTransform content, Recipe recipe)
        {
            bool selected = PreferenceStore.IsRecipeDefault(recipe);
            Color rowColor = selected
                ? new Color(0.08f, 0.22f, 0.1f, 1f)
                : UiFactory.RaisedBlack;
            Image background = UiFactory.CreatePanel("Recipe", content, rowColor, true);
            LayoutElement layout = background.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 64f;
            layout.preferredHeight = 64f;
            layout.flexibleHeight = 0f;

            if (!recipe.isRepair)
            {
                Button button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => ToggleRecipe(recipe));
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.7f, 0.8f, 0.72f, 1f);
                colors.pressedColor = new Color(0.32f, 0.85f, 0.38f, 1f);
                button.colors = colors;
            }

            ResourceKey result = new ResourceKey(recipe.result.id, recipe.result.isLiquid);
            Image icon = UiFactory.CreatePanel("Icon", background.transform, Color.white, true);
            icon.raycastTarget = false;
            UiFactory.Anchor(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(9f, 0f),
                new Vector2(44f, 44f));
            ResourceIconProvider.Apply(icon, result);

            string state = recipe.isRepair
                ? EmiText.RepairNotFavorite
                : selected ? EmiText.Favorited : EmiText.NotFavorited;
            TextMeshProUGUI text = UiFactory.CreateText(
                "Text",
                background.transform,
                _font,
                18f,
                TextAlignmentOptions.Left);
            UiFactory.Stretch(text.rectTransform, 60f, 8f, 4f, 4f);
            text.color = recipe.isRepair ? UiFactory.Muted : selected ? UiFactory.Green : UiFactory.White;
            text.text = recipe.simpleName +
                        "\n<size=13><color=#8D948F>" +
                        FormatRecipeDetail(recipe) + " | " + state +
                        "</color></size>";
            UiFactory.AddTooltip(background.gameObject, recipe.simpleName, recipe.description);
        }

        private void ToggleRecipe(Recipe recipe)
        {
            if (PreferenceStore.ToggleRecipe(recipe))
            {
                PlayClick();
            }
        }

        private void BackToResources()
        {
            _selectedResource = null;
            RenderCurrentPage();
            PlayClick();
        }

        private void RenderQualityList()
        {
            _headerTitle.text = EmiText.QualityCatalog;
            ScrollRect scroll = UiFactory.CreateScrollView("Qualities", _body, out RectTransform content);
            UiFactory.Stretch(scroll.GetComponent<RectTransform>());
            Track(scroll.gameObject);

            foreach (string qualityId in CompendiumCatalog.AllQualityIds)
            {
                List<QualityResourceEntry> candidates =
                    CompendiumCatalog.GetQualityResources(qualityId);
                string captured = qualityId;
                CreateTextRow(
                    content,
                    new CraftingQuality(qualityId).LocaleName,
                    EmiText.FormatCandidateCount(candidates.Count),
                    () => OpenQuality(captured),
                    UiFactory.White,
                    null);
            }

            RestoreScrollPosition(scroll, content, "qualities");
        }

        private void OpenQuality(string qualityId)
        {
            _selectedQuality = qualityId;
            RenderCurrentPage();
            PlayClick();
        }

        private void RenderQualityCandidates(string qualityId)
        {
            string qualityName = new CraftingQuality(qualityId).LocaleName;
            _headerTitle.text = qualityName;
            CreateBackBar(qualityName, BackToQualities);

            ScrollRect scroll = UiFactory.CreateScrollView("Candidates", _body, out RectTransform content);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = new Vector2(0f, -48f);
            Track(scroll.gameObject);

            List<QualityResourceEntry> candidates =
                CompendiumCatalog.GetQualityResources(qualityId);
            foreach (QualityResourceEntry candidate in candidates)
            {
                CreateQualityCandidateRow(content, qualityId, candidate);
            }

            if (candidates.Count == 0)
            {
                CreateEmptyText(EmiText.NoCandidates);
            }

            RestoreScrollPosition(scroll, content, "quality:" + qualityId);
        }

        private void CreateQualityCandidateRow(
            RectTransform content,
            string qualityId,
            QualityResourceEntry candidate)
        {
            bool selected = PreferenceStore.IsQualityDefault(qualityId, candidate.Resource);
            Color rowColor = selected
                ? new Color(0.08f, 0.22f, 0.1f, 1f)
                : UiFactory.RaisedBlack;
            Image background = UiFactory.CreatePanel("Candidate", content, rowColor, true);
            LayoutElement layout = background.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 60f;
            layout.preferredHeight = 60f;
            layout.flexibleHeight = 0f;

            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => ToggleQuality(qualityId, candidate.Resource));
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.7f, 0.8f, 0.72f, 1f);
            colors.pressedColor = new Color(0.32f, 0.85f, 0.38f, 1f);
            button.colors = colors;

            Image icon = UiFactory.CreatePanel("Icon", background.transform, Color.white, true);
            icon.raycastTarget = false;
            UiFactory.Anchor(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(9f, 0f),
                new Vector2(42f, 42f));
            ResourceIconProvider.Apply(icon, candidate.Resource);

            string amount = candidate.Resource.IsLiquid
                ? EmiText.FormatQualityPerMilliliter(candidate.Amount)
                : EmiText.FormatQualityValue(candidate.Amount);
            string state = selected ? EmiText.Favorited : EmiText.NotFavorited;
            TextMeshProUGUI text = UiFactory.CreateText(
                "Text",
                background.transform,
                _font,
                18f,
                TextAlignmentOptions.Left);
            UiFactory.Stretch(text.rectTransform, 58f, 8f, 4f, 4f);
            text.color = selected ? UiFactory.Green : UiFactory.White;
            text.text = candidate.Resource.DisplayName +
                        "\n<size=13><color=#8D948F>" + amount + " | " + state +
                        "</color></size>";
            UiFactory.AddTooltip(
                background.gameObject,
                candidate.Resource.DisplayName,
                candidate.Resource.Description);
        }

        private void ToggleQuality(string qualityId, ResourceKey resource)
        {
            if (PreferenceStore.ToggleQuality(qualityId, resource))
            {
                PlayClick();
            }
        }

        private void BackToQualities()
        {
            _selectedQuality = null;
            RenderCurrentPage();
            PlayClick();
        }

        private void CreateBackBar(string title, Action backAction)
        {
            Image bar = UiFactory.CreatePanel("BackBar", _body, UiFactory.RaisedBlack);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0f, 42f);
            Track(bar.gameObject);

            Button back = UiFactory.CreateButton(
                "Back",
                bar.transform,
                _font,
                "←",
                backAction,
                out _,
                out _);
            UiFactory.Anchor(
                back.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(4f, 0f),
                new Vector2(34f, 32f));
            UiFactory.AddTooltip(back.gameObject, EmiText.Back, EmiText.BackDescription);

            TextMeshProUGUI text = UiFactory.CreateText(
                "Title",
                bar.transform,
                _font,
                18f,
                TextAlignmentOptions.Left);
            UiFactory.Stretch(text.rectTransform, 46f, 8f, 3f, 3f);
            text.text = title;
        }

        private void CreateTextRow(
            RectTransform parent,
            string title,
            string detail,
            Action action,
            Color color,
            string tooltip)
        {
            Image background = UiFactory.CreatePanel("TextRow", parent, UiFactory.RaisedBlack, true);
            LayoutElement layout = background.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 54f;
            layout.preferredHeight = 54f;
            layout.flexibleHeight = 0f;

            if (action != null)
            {
                Button button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => action());
            }

            TextMeshProUGUI text = UiFactory.CreateText(
                "Text",
                background.transform,
                _font,
                18f,
                TextAlignmentOptions.Left);
            UiFactory.Stretch(text.rectTransform, 10f, 8f, 4f, 4f);
            text.color = color;
            text.text = string.IsNullOrEmpty(detail)
                ? title
                : title + "\n<size=13><color=#8D948F>" + detail + "</color></size>";
            UiFactory.AddTooltip(background.gameObject, title, tooltip ?? detail);
        }

        private void CreateEmptyText(string value)
        {
            TextMeshProUGUI empty = UiFactory.CreateText(
                "Empty",
                _body,
                _font,
                21f,
                TextAlignmentOptions.Center);
            UiFactory.Stretch(empty.rectTransform, 20f, 20f, 60f, 60f);
            empty.color = UiFactory.Muted;
            empty.text = value;
            Track(empty.gameObject);
        }

        private void ClearBody()
        {
            SaveActiveScrollPosition();
            _searchInput = null;
            _gridContent = null;
            _activeScroll = null;
            _activeScrollKey = null;
            _resourceCells.Clear();
            foreach (GameObject bodyObject in _bodyObjects)
            {
                if (bodyObject == null)
                {
                    continue;
                }

                bodyObject.SetActive(false);
                Destroy(bodyObject);
            }

            _bodyObjects.Clear();
        }

        private void RestoreScrollPosition(
            ScrollRect scroll,
            RectTransform content,
            string key)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();

            float position = _scrollPositions.TryGetValue(key, out float saved)
                ? saved
                : 1f;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(position);
            scroll.onValueChanged.AddListener(value => _scrollPositions[key] = value.y);
            _activeScroll = scroll;
            _activeScrollKey = key;
        }

        private void SaveActiveScrollPosition()
        {
            if (_activeScroll != null && !string.IsNullOrEmpty(_activeScrollKey))
            {
                _scrollPositions[_activeScrollKey] =
                    _activeScroll.verticalNormalizedPosition;
            }
        }

        private void Track(GameObject gameObject)
        {
            _bodyObjects.Add(gameObject);
        }

        private void PlayClick()
        {
            _player?.PlayUISound(PlayerCamera.UISoundType.MiniClick, 1f);
        }

        private static bool ContainsLocalized(string value, string query)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(query))
            {
                return false;
            }

            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                       value,
                       query,
                       CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth) >= 0;
        }

        private static string FormatRecipeDetail(Recipe recipe)
        {
            string output = recipe.result.isLiquid
                ? recipe.result.resultCondition.ToString("0.#", CultureInfo.InvariantCulture) + "mL"
                : "x" + recipe.result.amount.ToString(CultureInfo.InvariantCulture);
            return "INT " + recipe.INT.ToString(CultureInfo.InvariantCulture) +
                   " | " + output +
                   " | " + recipe.items.Count.ToString(CultureInfo.InvariantCulture) +
                   " " + EmiText.Items;
        }
    }
}
