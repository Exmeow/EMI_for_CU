using System;
using System.Collections.Generic;

namespace EMI
{
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

        public bool IsSharedReusable { get; internal set; }

        public bool IsRoot => Parent == null;

        public bool IsQualityRequirement => Requirement != null && !Requirement.specific;

        public bool IsReusable => Requirement != null && !Requirement.isLiquid && !Requirement.destroyItem;

        public bool CanShowChildren =>
            SelectedRecipe != null && !IsCycleBoundary && !IsSharedReusable;

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

                if (Resource.HasValue && Resource.Value.IsLiquid)
                {
                    float outputPerCraft = SelectedRecipe.result.resultCondition *
                                           Math.Max(1, SelectedRecipe.result.amount);
                    return outputPerCraft > 0f
                        ? Math.Max(1, (int)Math.Ceiling(RequiredLiquidAmount / outputPerCraft))
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

            RequiredItemCount = IsReusable
                ? 1
                : RequirementMultiplicity * ParentCraftRuns;
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

    internal sealed class CraftingTreeModel
    {
        private readonly Dictionary<ResourceKey, Recipe> _selectedRecipes =
            new Dictionary<ResourceKey, Recipe>();

        public CraftingTreeNode Root { get; private set; }

        public Recipe RootRecipe => Root?.SelectedRecipe;

        public void SetRoot(Recipe recipe)
        {
            _selectedRecipes.Clear();
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
        }

        public void ResetSelections()
        {
            if (RootRecipe != null)
            {
                SetRoot(RootRecipe);
            }
        }

        public void SelectCandidate(CraftingTreeNode node, ResourceCandidate candidate)
        {
            if (node == null)
            {
                return;
            }

            node.SelectCandidate(candidate);
            EvaluateBoundaries();
        }

        public void SelectRecipe(CraftingTreeNode node, Recipe recipe)
        {
            if (node?.Resource == null || recipe?.result == null)
            {
                return;
            }

            ResourceKey resource = node.Resource.Value;
            ResourceKey result = new ResourceKey(recipe.result.id, recipe.result.isLiquid);
            if (resource != result)
            {
                return;
            }

            _selectedRecipes[resource] = recipe;
            EvaluateBoundaries();
        }

        public void StopExpansion(CraftingTreeNode node)
        {
            if (node?.Resource == null || node.IsRoot)
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

            ResetDerivedFlags(Root);
            ApplySelectedRecipes(Root, new HashSet<ResourceKey>());
            EvaluateReusableReferences();
        }

        private static void ResetDerivedFlags(CraftingTreeNode node)
        {
            node.IsCycleBoundary = false;
            node.IsSharedReusable = false;
            foreach (CraftingTreeNode child in node.Children)
            {
                ResetDerivedFlags(child);
            }
        }

        private void ApplySelectedRecipes(CraftingTreeNode node, HashSet<ResourceKey> ancestors)
        {
            bool added = false;
            if (node.Resource.HasValue)
            {
                ResourceKey resource = node.Resource.Value;
                if (ancestors.Contains(resource))
                {
                    node.IsCycleBoundary = true;
                    if (_selectedRecipes.TryGetValue(resource, out Recipe cycleRecipe))
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
                    if (_selectedRecipes.TryGetValue(resource, out Recipe selectedRecipe))
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
                ApplySelectedRecipes(child, ancestors);
            }

            if (added)
            {
                ancestors.Remove(node.Resource.Value);
            }
        }

        private void EvaluateReusableReferences()
        {
            HashSet<ResourceKey> seen = new HashSet<ResourceKey>();
            Queue<CraftingTreeNode> queue = new Queue<CraftingTreeNode>();
            queue.Enqueue(Root);

            while (queue.Count > 0)
            {
                CraftingTreeNode node = queue.Dequeue();
                if (node.IsCycleBoundary)
                {
                    continue;
                }

                if (node.IsReusable && node.Resource.HasValue)
                {
                    if (!seen.Add(node.Resource.Value))
                    {
                        node.IsSharedReusable = true;
                        continue;
                    }
                }

                if (node.SelectedRecipe == null)
                {
                    continue;
                }

                foreach (CraftingTreeNode child in node.Children)
                {
                    queue.Enqueue(child);
                }
            }
        }
    }
}
