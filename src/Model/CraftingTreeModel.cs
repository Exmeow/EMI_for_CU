using System;
using System.Collections.Generic;

namespace EMI
{
    /// <summary>
    /// 合成树中的一个需求节点。节点保存用户选择和由配方推导出的子需求，但不读取玩家库存。
    /// </summary>
    internal sealed class CraftingTreeNode
    {
        private sealed class RequirementGroup
        {
            public RecipeItem Requirement;
            public int Count;
        }

        private CraftingTreeNode(
            CraftingTreeNode parent,
            RecipeItem requirement,
            int requirementMultiplicity,
            int parentCraftRuns,
            int depth)
        {
            Parent = parent;
            Requirement = requirement;
            RequirementMultiplicity = Math.Max(1, requirementMultiplicity);
            ParentCraftRuns = Math.Max(1, parentCraftRuns);
            Depth = depth;
            Children = new List<CraftingTreeNode>();

            if (requirement != null && requirement.specific)
            {
                Resource = new ResourceKey(requirement.specificId, requirement.isLiquid);
            }

            UpdateRequiredQuantity();
        }

        public CraftingTreeNode Parent { get; }

        public RecipeItem Requirement { get; }

        public int RequirementMultiplicity { get; }

        public int ParentCraftRuns { get; }

        public int Depth { get; }

        public ResourceKey? Resource { get; private set; }

        public ResourceCandidate SelectedCandidate { get; private set; }

        public Recipe SelectedRecipe { get; private set; }

        public List<CraftingTreeNode> Children { get; }

        public int RequiredItemCount { get; private set; }

        public float RequiredLiquidAmount { get; private set; }

        public bool IsCycleBoundary { get; internal set; }

        public bool IsCandidateLocked { get; internal set; }

        public bool IsRecipeLocked { get; internal set; }

        public bool IsRoot => Parent == null;

        public bool IsQualityRequirement => Requirement != null && !Requirement.specific;

        public bool UsesDurability => DurabilityRequirement.AppliesTo(Requirement);

        public bool CanShowChildren =>
            SelectedRecipe != null && !IsCycleBoundary;

        public int CraftRuns
        {
            get
            {
                if (SelectedRecipe == null)
                {
                    return 0;
                }

                if (IsRoot)
                {
                    return 1;
                }

                // 液体按体积计算次数；耐久工具按单件可用次数计算；普通物品按产出数量计算。
                if (Resource.HasValue && Resource.Value.IsLiquid)
                {
                    float outputPerCraft = SelectedRecipe.result.resultCondition *
                                           Math.Max(1, SelectedRecipe.result.amount);
                    return outputPerCraft > 0f
                        ? Math.Max(1, (int)Math.Ceiling(RequiredLiquidAmount / outputPerCraft))
                        : 1;
                }

                if (UsesDurability)
                {
                    int usesPerCraft = DurabilityRequirement.GetUsesPerCraft(
                        SelectedRecipe.result,
                        Requirement,
                        RequiredItemCount);
                    return usesPerCraft > 0
                        ? Math.Max(1, (int)Math.Ceiling((double)RequiredItemCount / usesPerCraft))
                        : 1;
                }

                int itemOutput = Math.Max(1, SelectedRecipe.result.amount);
                return Math.Max(1, (int)Math.Ceiling((double)RequiredItemCount / itemOutput));
            }
        }

        public static CraftingTreeNode CreateRoot(Recipe recipe)
        {
            CraftingTreeNode root = new CraftingTreeNode(null, null, 1, 1, 0)
            {
                Resource = new ResourceKey(recipe.result.id, recipe.result.isLiquid)
            };
            root.SetRecipe(recipe, true);
            return root;
        }

        public void SelectCandidate(ResourceCandidate candidate)
        {
            SelectedCandidate = candidate;
            Resource = candidate?.Resource;
            UpdateRequiredQuantity();
            SetRecipe(null, false);
        }

        public float RequiredLiquidAmountFor(ResourceCandidate candidate)
        {
            if (Requirement == null || !Requirement.isLiquid)
            {
                return 0f;
            }

            float amountPerRequirement = Requirement.specific
                ? Requirement.minimumCondition
                : (candidate?.RequiredLiquidAmount ?? 0f);
            return amountPerRequirement * RequirementMultiplicity * ParentCraftRuns;
        }

        public void SetRecipe(Recipe recipe, bool createChildren)
        {
            SelectedRecipe = recipe;
            Children.Clear();

            if (!createChildren || recipe?.items == null)
            {
                return;
            }

            int childParentCraftRuns = CraftRuns;
            // 相同需求先合并为一个节点，避免配方数据中的重复行让树和数量统计膨胀。
            foreach (RequirementGroup group in GroupRequirements(recipe.items))
            {
                Children.Add(new CraftingTreeNode(
                    this,
                    group.Requirement,
                    group.Count,
                    childParentCraftRuns,
                    Depth + 1));
            }
        }

        private void UpdateRequiredQuantity()
        {
            RequiredItemCount = 0;
            RequiredLiquidAmount = 0f;

            if (Requirement == null)
            {
                RequiredItemCount = 1;
                return;
            }

            if (Requirement.isLiquid)
            {
                RequiredLiquidAmount = RequiredLiquidAmountFor(SelectedCandidate);
                return;
            }

            RequiredItemCount = RequirementMultiplicity * ParentCraftRuns;
        }

        private static List<RequirementGroup> GroupRequirements(List<RecipeItem> requirements)
        {
            List<RequirementGroup> groups = new List<RequirementGroup>();
            foreach (RecipeItem requirement in requirements)
            {
                if (requirement == null)
                {
                    continue;
                }

                RequirementGroup matching = null;
                foreach (RequirementGroup group in groups)
                {
                    if (RequirementsMatch(group.Requirement, requirement))
                    {
                        matching = group;
                        break;
                    }
                }

                if (matching == null)
                {
                    groups.Add(new RequirementGroup
                    {
                        Requirement = requirement,
                        Count = 1
                    });
                }
                else
                {
                    matching.Count++;
                }
            }

            return groups;
        }

        private static bool RequirementsMatch(RecipeItem left, RecipeItem right)
        {
            if (left.specific != right.specific ||
                left.isLiquid != right.isLiquid ||
                left.destroyItem != right.destroyItem ||
                left.minimumCondition != right.minimumCondition)
            {
                return false;
            }

            if (left.specific)
            {
                return string.Equals(left.specificId, right.specificId, StringComparison.Ordinal);
            }

            if (left.quality == null || right.quality == null)
            {
                return left.quality == right.quality;
            }

            return string.Equals(left.quality.id, right.quality.id, StringComparison.Ordinal) &&
                   left.quality.amount == right.quality.amount;
        }
    }

    /// <summary>
    /// 管理整棵树的选择状态与重新求值。UI 只提交选择，不直接修改节点的派生锁定或边界标记。
    /// </summary>
    internal sealed class CraftingTreeModel
    {
        private readonly Dictionary<ResourceKey, Recipe> _selectedRecipes =
            new Dictionary<ResourceKey, Recipe>();

        private readonly Dictionary<QualityRequirementKey, ResourceCandidate> _selectedCandidates =
            new Dictionary<QualityRequirementKey, ResourceCandidate>();

        public CraftingTreeNode Root { get; private set; }

        public Recipe RootRecipe => Root?.SelectedRecipe;

        public void SetRoot(Recipe recipe)
        {
            _selectedRecipes.Clear();
            _selectedCandidates.Clear();
            Root = recipe == null ? null : CraftingTreeNode.CreateRoot(recipe);

            if (Root?.Resource != null)
            {
                _selectedRecipes[Root.Resource.Value] = recipe;
            }

            EvaluateBoundaries();
        }

        public void Clear()
        {
            Root = null;
            _selectedRecipes.Clear();
            _selectedCandidates.Clear();
        }

        public void ResetSelections()
        {
            if (RootRecipe != null)
            {
                SetRoot(RootRecipe);
            }
        }

        public List<ResourceCandidate> GetSharedCandidates(CraftingTreeNode node)
        {
            List<ResourceCandidate> candidates = RecipeCatalog.GetCandidates(node?.Requirement);
            if (Root == null || node == null ||
                !QualityRequirementKey.TryCreate(node.Requirement, out QualityRequirementKey key))
            {
                return candidates;
            }

            for (int index = candidates.Count - 1; index >= 0; index--)
            {
                if (!CandidateAllowedInTree(Root, key, candidates[index]))
                {
                    candidates.RemoveAt(index);
                }
            }

            return candidates;
        }

        public void SelectCandidate(CraftingTreeNode node, ResourceCandidate candidate)
        {
            if (Root == null || node == null || candidate == null ||
                node.IsCandidateLocked ||
                !QualityRequirementKey.TryCreate(node.Requirement, out QualityRequirementKey key))
            {
                return;
            }

            if (!CandidateAllowedInTree(Root, key, candidate))
            {
                return;
            }

            if (DurabilityRequirement.IsQualityTool(node.Requirement))
            {
                SelectCompatibleToolCandidates(
                    Root,
                    node.Requirement.quality.id,
                    candidate,
                    new HashSet<QualityRequirementKey>());
            }
            else
            {
                _selectedCandidates[key] = candidate;
            }

            EvaluateBoundaries();
        }

        public void ClearCandidate(CraftingTreeNode node)
        {
            if (node == null || node.IsCandidateLocked ||
                !QualityRequirementKey.TryCreate(node.Requirement, out QualityRequirementKey key))
            {
                return;
            }

            _selectedCandidates.Remove(key);
            EvaluateBoundaries();
        }

        public void SelectRecipe(CraftingTreeNode node, Recipe recipe)
        {
            if (node?.Resource == null || recipe?.result == null || node.IsRecipeLocked)
            {
                return;
            }

            ResourceKey resource = node.Resource.Value;
            ResourceKey result = new ResourceKey(recipe.result.id, recipe.result.isLiquid);
            if (resource != result || !RecipeCatalog.IsProducerCompatible(recipe, node.Requirement))
            {
                return;
            }

            _selectedRecipes[resource] = recipe;
            EvaluateBoundaries();
        }

        public void StopExpansion(CraftingTreeNode node)
        {
            if (node?.Resource == null || node.IsRoot || node.IsRecipeLocked)
            {
                return;
            }

            _selectedRecipes.Remove(node.Resource.Value);
            EvaluateBoundaries();
        }

        public void EvaluateBoundaries()
        {
            if (Root == null)
            {
                return;
            }

            // 性质选择改变后，旧选择可能已不再满足新生成的同类节点。
            // 重新应用选择并清理冲突，直到树和共享选择达到稳定状态。
            bool removedIncompatibleSelection;
            do
            {
                ResetDerivedFlags(Root);
                ApplySelections(Root, new HashSet<ResourceKey>());
                removedIncompatibleSelection = RemoveIncompatibleCandidateSelections();
            }
            while (removedIncompatibleSelection);

        }

        private bool RemoveIncompatibleCandidateSelections()
        {
            HashSet<QualityRequirementKey> incompatible = new HashSet<QualityRequirementKey>();
            FindIncompatibleCandidateSelections(Root, incompatible);
            foreach (QualityRequirementKey key in incompatible)
            {
                _selectedCandidates.Remove(key);
            }

            return incompatible.Count > 0;
        }

        private void FindIncompatibleCandidateSelections(
            CraftingTreeNode node,
            HashSet<QualityRequirementKey> incompatible)
        {
            if (QualityRequirementKey.TryCreate(node.Requirement, out QualityRequirementKey key) &&
                _selectedCandidates.TryGetValue(key, out ResourceCandidate candidate) &&
                !RecipeCatalog.IsCandidateCompatible(candidate, node.Requirement))
            {
                incompatible.Add(key);
            }

            foreach (CraftingTreeNode child in node.Children)
            {
                FindIncompatibleCandidateSelections(child, incompatible);
            }
        }

        private static bool CandidateAllowedInTree(
            CraftingTreeNode node,
            QualityRequirementKey key,
            ResourceCandidate candidate)
        {
            if (QualityRequirementKey.TryCreate(node.Requirement, out QualityRequirementKey nodeKey) &&
                key.Equals(nodeKey) &&
                !RecipeCatalog.IsCandidateCompatible(candidate, node.Requirement))
            {
                return false;
            }

            foreach (CraftingTreeNode child in node.Children)
            {
                if (!CandidateAllowedInTree(child, key, candidate))
                {
                    return false;
                }
            }

            return true;
        }

        private void SelectCompatibleToolCandidates(
            CraftingTreeNode node,
            string qualityId,
            ResourceCandidate candidate,
            HashSet<QualityRequirementKey> visited)
        {
            if (DurabilityRequirement.IsQualityTool(node.Requirement) &&
                string.Equals(node.Requirement.quality.id, qualityId, StringComparison.Ordinal) &&
                QualityRequirementKey.TryCreate(
                    node.Requirement,
                    out QualityRequirementKey key) &&
                visited.Add(key) &&
                CandidateAllowedInTree(Root, key, candidate))
            {
                // 不同强度阈值拥有不同的键；只有确实兼容的需求组才会同步使用同一工具。
                _selectedCandidates[key] = candidate;
            }

            foreach (CraftingTreeNode child in node.Children)
            {
                SelectCompatibleToolCandidates(child, qualityId, candidate, visited);
            }
        }

        private static void ResetDerivedFlags(CraftingTreeNode node)
        {
            node.IsCycleBoundary = false;
            node.IsCandidateLocked = false;
            node.IsRecipeLocked = false;
            foreach (CraftingTreeNode child in node.Children)
            {
                ResetDerivedFlags(child);
            }
        }

        private void ApplySelections(CraftingTreeNode node, HashSet<ResourceKey> ancestors)
        {
            if (QualityRequirementKey.TryCreate(node.Requirement, out QualityRequirementKey qualityKey))
            {
                ResourceCandidate selectedCandidate;
                if (PreferenceStore.TryGetQualityCandidate(node.Requirement, out selectedCandidate))
                {
                    node.IsCandidateLocked = true;
                }
                else
                {
                    _selectedCandidates.TryGetValue(qualityKey, out selectedCandidate);
                }

                if (!CandidatesMatch(node.SelectedCandidate, selectedCandidate))
                {
                    node.SelectCandidate(selectedCandidate);
                }
            }

            bool added = false;
            if (node.Resource.HasValue)
            {
                ResourceKey resource = node.Resource.Value;
                // 资源再次出现在祖先路径上即为递归边界：保留该节点供展示，但不再创建下游。
                if (ancestors.Contains(resource))
                {
                    node.IsCycleBoundary = true;
                    Recipe cycleRecipe = GetEffectiveRecipe(node, resource, out bool cycleLocked);
                    node.IsRecipeLocked = cycleLocked;
                    if (cycleRecipe != null)
                    {
                        node.SetRecipe(cycleRecipe, false);
                    }
                    else
                    {
                        node.SetRecipe(null, false);
                    }

                    return;
                }

                if (!node.IsRoot)
                {
                    Recipe selectedRecipe = GetEffectiveRecipe(node, resource, out bool recipeLocked);
                    node.IsRecipeLocked = recipeLocked;
                    if (selectedRecipe != null)
                    {
                        bool needsChildren = selectedRecipe.items != null && selectedRecipe.items.Count > 0;
                        if (node.SelectedRecipe != selectedRecipe ||
                            (needsChildren && node.Children.Count == 0))
                        {
                            node.SetRecipe(selectedRecipe, true);
                        }
                    }
                    else if (node.SelectedRecipe != null)
                    {
                        node.SetRecipe(null, false);
                    }
                }

                ancestors.Add(resource);
                added = true;
            }

            foreach (CraftingTreeNode child in node.Children)
            {
                ApplySelections(child, ancestors);
            }

            if (added)
            {
                ancestors.Remove(node.Resource.Value);
            }
        }

        private Recipe GetEffectiveRecipe(
            CraftingTreeNode node,
            ResourceKey resource,
            out bool locked)
        {
            locked = false;

            // 根配方固定选择优先，其次是图鉴中持久化的默认配方，最后才是当前树的临时选择。
            if (Root?.Resource != null && Root.Resource.Value == resource &&
                _selectedRecipes.TryGetValue(resource, out Recipe rootRecipe) &&
                RecipeCatalog.IsProducerCompatible(rootRecipe, node.Requirement))
            {
                return rootRecipe;
            }

            Recipe preferred = PreferenceStore.GetRecipeDefault(resource);
            if (preferred != null && RecipeCatalog.IsProducerCompatible(preferred, node.Requirement))
            {
                locked = true;
                return preferred;
            }

            if (_selectedRecipes.TryGetValue(resource, out Recipe selected) &&
                RecipeCatalog.IsProducerCompatible(selected, node.Requirement))
            {
                return selected;
            }

            return null;
        }

        private static bool CandidatesMatch(ResourceCandidate left, ResourceCandidate right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.Resource == right.Resource &&
                   left.RequiredLiquidAmount.Equals(right.RequiredLiquidAmount);
        }

    }
}
