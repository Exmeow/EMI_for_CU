using System;
using System.Collections.Generic;
using UnityEngine;

namespace EMI
{
    /// <summary>
    /// 剩余材料的展示类型。具体资源与抽象性质分开记录，物品数量与液体体积也分别处理。
    /// </summary>
    internal enum RemainingMaterialKind
    {
        ConcreteItem,
        ConcreteLiquid,
        QualityItem,
        QualityLiquid
    }

    /// <summary>
    /// 一条最终无法继续展开、需要玩家实际收集的材料。
    /// </summary>
    internal sealed class RemainingMaterial
    {
        public RemainingMaterialKind Kind { get; set; }

        public ResourceKey? Resource { get; set; }

        public RecipeItem Requirement { get; set; }

        public int ItemCount { get; set; }

        public float Amount { get; set; }

        public bool UsesDurability => DurabilityRequirement.AppliesTo(Requirement);
    }

    /// <summary>
    /// 一次规划计算的结果：材料清单，以及用于原版配方栏高亮的配方集合。
    /// </summary>
    internal sealed class CraftingPlanResult
    {
        public List<RemainingMaterial> RemainingMaterials { get; } =
            new List<RemainingMaterial>();

        public HashSet<Recipe> RequiredRecipes { get; } = new HashSet<Recipe>();

        public HashSet<Recipe> LeafProducerRecipes { get; } = new HashSet<Recipe>();
    }

    /// <summary>
    /// 在不修改游戏状态的前提下，用虚拟库存模拟整棵树的材料消耗与批量产出。
    /// </summary>
    internal static class RemainingMaterialsCalculator
    {
        private const float AmountEpsilon = 0.0001f;

        private sealed class LiquidPool
        {
            public string Id;
            public float RemainingAmount;
        }

        private sealed class InventoryEntry
        {
            public string Id;
            public List<CraftingQuality> Qualities;
            public bool ContainsItems;
            public bool Consumed;
            public bool DurabilityAllocated;
            public bool LiquidAllocated;
            public float RemainingCondition;
            public CraftingTreeNode PlannedBy;
            public HashSet<ResourceKey> PlannedDependencies;
            public readonly List<LiquidPool> Liquids = new List<LiquidPool>();
        }

        private sealed class Demand
        {
            public CraftingTreeNode Template;
            public int RemainingItems;
            public float RemainingAmount;

            public RecipeItem Requirement => Template.Requirement;

            public bool IsLiquid => Requirement != null && Requirement.isLiquid;

            public bool UsesDurability => Template.UsesDurability;

            public bool IsAbstractQuality => Template.IsQualityRequirement && !Template.Resource.HasValue;

            public bool IsSatisfied => IsLiquid
                ? RemainingAmount <= AmountEpsilon
                : RemainingItems <= 0;
        }

        private readonly struct RemainingMaterialKey : IEquatable<RemainingMaterialKey>
        {
            public RemainingMaterialKey(Demand demand)
            {
                RecipeItem requirement = demand.Requirement;
                Kind = GetKind(demand);
                Resource = demand.Template.Resource.GetValueOrDefault();
                HasResource = demand.Template.Resource.HasValue;
                QualityId = HasResource ? string.Empty : requirement?.quality?.id ?? string.Empty;
                MinimumCondition = demand.IsLiquid
                    ? 0f
                    : requirement?.minimumCondition ?? 0f;
                UsesDurability = demand.UsesDurability;
            }

            private RemainingMaterialKind Kind { get; }

            private ResourceKey Resource { get; }

            private bool HasResource { get; }

            private string QualityId { get; }

            private float MinimumCondition { get; }

            private bool UsesDurability { get; }

            public bool Equals(RemainingMaterialKey other)
            {
                return Kind == other.Kind &&
                       HasResource == other.HasResource &&
                       (!HasResource || Resource == other.Resource) &&
                       MinimumCondition.Equals(other.MinimumCondition) &&
                       UsesDurability == other.UsesDurability &&
                       string.Equals(QualityId, other.QualityId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is RemainingMaterialKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (int)Kind;
                    hashCode = (hashCode * 397) ^ HasResource.GetHashCode();
                    hashCode = (hashCode * 397) ^ (HasResource ? Resource.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (QualityId != null ? QualityId.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ MinimumCondition.GetHashCode();
                    hashCode = (hashCode * 397) ^ UsesDurability.GetHashCode();
                    return hashCode;
                }
            }
        }

        public static CraftingPlanResult Calculate(CraftingTreeNode root, Body body)
        {
            CraftingPlanResult result = new CraftingPlanResult();
            if (root == null || body == null)
            {
                return result;
            }

            List<InventoryEntry> inventory = ScanAvailableItems(body);
            Dictionary<RemainingMaterialKey, int> materialIndices =
                new Dictionary<RemainingMaterialKey, int>();

            if (root.SelectedRecipe != null)
            {
                result.RequiredRecipes.Add(root.SelectedRecipe);
            }

            // 按树的层级分配库存，使已有中间产物优先满足靠近成品的需求。
            // 未满足的需求才会展开生产配方，并把产出放回同一虚拟库存供其他分支共享。
            List<Demand> level = CreateChildDemands(root, 1);
            List<Demand> pendingLeaves = new List<Demand>();

            while (level.Count > 0)
            {
                AllocateInventory(level, inventory);
                List<Demand> nextLevel = new List<Demand>();

                foreach (Demand demand in level)
                {
                    AllocateInventory(demand, inventory);
                    if (demand.IsSatisfied)
                    {
                        continue;
                    }

                    CraftingTreeNode template = demand.Template;
                    Recipe producer = template.SelectedRecipe;
                    if (producer != null && !template.IsCycleBoundary &&
                        RecipeCatalog.IsProducerCompatible(producer, demand.Requirement))
                    {
                        result.RequiredRecipes.Add(producer);
                        int craftRuns = RequiredCraftRuns(demand, producer);
                        AddPlannedOutputs(
                            inventory,
                            producer,
                            craftRuns,
                            demand.UsesDurability,
                            template);
                        AllocateInventory(demand, inventory);
                        AllocateInventory(pendingLeaves, inventory);

                        if (demand.IsSatisfied &&
                            (producer.items == null || producer.items.Count == 0))
                        {
                            continue;
                        }

                        if (demand.IsSatisfied && template.Children.Count > 0)
                        {
                            List<Demand> children = CreateChildDemands(template, craftRuns);
                            nextLevel.AddRange(children);
                            continue;
                        }
                    }

                    pendingLeaves.Add(demand);
                }

                level = nextLevel;
            }

            foreach (Demand demand in pendingLeaves)
            {
                if (demand.IsSatisfied)
                {
                    continue;
                }

                AddLeaf(result.RemainingMaterials, materialIndices, demand);
                RegisterLeafProducers(result.LeafProducerRecipes, demand);
            }

            return result;
        }

        private static void RegisterLeafProducers(
            HashSet<Recipe> producers,
            Demand demand)
        {
            if (demand.Template.Resource.HasValue)
            {
                foreach (Recipe producer in RecipeCatalog.GetCompatibleProducers(
                             demand.Template.Resource.Value,
                             demand.Requirement))
                {
                    producers.Add(producer);
                }

                return;
            }

            foreach (Recipe producer in RecipeCatalog.GetQualityProducers(demand.Requirement))
            {
                producers.Add(producer);
            }
        }

        private static List<Demand> CreateChildDemands(CraftingTreeNode parent, int parentCraftRuns)
        {
            List<Demand> demands = new List<Demand>();
            foreach (CraftingTreeNode child in parent.Children)
            {
                Demand demand = CreateDemand(child, parentCraftRuns);
                if (demand == null)
                {
                    continue;
                }

                demands.Add(demand);
            }

            return demands;
        }

        private static Demand CreateDemand(CraftingTreeNode template, int parentCraftRuns)
        {
            RecipeItem requirement = template?.Requirement;
            if (requirement == null)
            {
                return null;
            }

            int runs = Math.Max(1, parentCraftRuns);
            Demand demand = new Demand
            {
                Template = template
            };

            if (!requirement.isLiquid)
            {
                demand.RemainingItems = template.RequirementMultiplicity * runs;
                return demand;
            }

            if (template.Resource.HasValue)
            {
                float amountPerRequirement = requirement.specific
                    ? requirement.minimumCondition
                    : (template.SelectedCandidate?.RequiredLiquidAmount ?? 0f);
                demand.RemainingAmount = amountPerRequirement * template.RequirementMultiplicity * runs;
            }
            else if (requirement.quality != null)
            {
                demand.RemainingAmount = requirement.quality.amount * template.RequirementMultiplicity * runs;
            }

            return demand;
        }

        private static int RequiredCraftRuns(Demand demand, Recipe producer)
        {
            if (demand.IsLiquid)
            {
                float outputPerCraft = producer.result.resultCondition * Math.Max(1, producer.result.amount);
                return outputPerCraft > AmountEpsilon
                    ? Math.Max(1, (int)Math.Ceiling(demand.RemainingAmount / outputPerCraft))
                    : 1;
            }

            if (demand.UsesDurability)
            {
                int usesPerCraft = DurabilityRequirement.GetUsesPerCraft(
                    producer.result,
                    demand.Requirement,
                    demand.RemainingItems);
                return usesPerCraft > 0
                    ? Math.Max(1, (int)Math.Ceiling((double)demand.RemainingItems / usesPerCraft))
                    : 1;
            }

            int itemOutput = Math.Max(1, producer.result.amount);
            return Math.Max(1, (int)Math.Ceiling((double)demand.RemainingItems / itemOutput));
        }

        private static void AllocateInventory(List<Demand> demands, List<InventoryEntry> inventory)
        {
            foreach (InventoryEntry entry in inventory)
            {
                for (int i = 0; i < demands.Count; i++)
                {
                    Demand demand = demands[i];
                    AllocateInventoryEntry(entry, demand);
                    if (entry.Consumed)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 为单个需求重新扫描库存。生产新物品后会频繁调用此重载，避免为此创建临时列表。
        /// </summary>
        private static void AllocateInventory(Demand demand, List<InventoryEntry> inventory)
        {
            foreach (InventoryEntry entry in inventory)
            {
                AllocateInventoryEntry(entry, demand);
                if (demand.IsSatisfied)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 尝试把一个虚拟库存项分配给一个需求。
        /// 液体可以分给多个需求，工具可以按剩余耐久重复使用，普通物品分配后便标记为已消耗。
        /// </summary>
        private static void AllocateInventoryEntry(InventoryEntry entry, Demand demand)
        {
            if (demand.IsSatisfied || !CanAllocate(entry, demand))
            {
                return;
            }

            if (demand.IsLiquid)
            {
                if (!entry.Consumed && !entry.DurabilityAllocated && AllocateLiquid(entry, demand))
                {
                    entry.LiquidAllocated = true;
                }

                return;
            }

            if (entry.Consumed || entry.LiquidAllocated ||
                (!demand.UsesDurability && entry.DurabilityAllocated) ||
                !MatchesItem(entry, demand))
            {
                return;
            }

            if (demand.UsesDurability)
            {
                AllocateDurability(entry, demand);
                return;
            }

            demand.RemainingItems--;
            entry.Consumed = true;
        }

        private static bool CanAllocate(InventoryEntry entry, Demand demand)
        {
            CraftingTreeNode source = entry?.PlannedBy;
            if (source == null || ReferenceEquals(source, demand.Template))
            {
                return true;
            }

            // 规划产物不能反过来满足自身的下游需求，也不能用来启动互相依赖的配方环。
            for (CraftingTreeNode ancestor = demand.Template?.Parent;
                 ancestor != null;
                 ancestor = ancestor.Parent)
            {
                if (ReferenceEquals(ancestor, source))
                {
                    return false;
                }
            }

            CraftingTreeNode consumer = demand.Template?.Parent;
            if (consumer?.Resource != null &&
                entry.PlannedDependencies?.Contains(consumer.Resource.Value) == true)
            {
                return false;
            }

            return true;
        }

        private static void AllocateDurability(InventoryEntry entry, Demand demand)
        {
            float conditionCost = DurabilityRequirement.GetConditionCost(
                demand.Requirement,
                entry.Qualities);
            if (float.IsInfinity(conditionCost) || float.IsNaN(conditionCost))
            {
                return;
            }

            entry.DurabilityAllocated = true;
            if (conditionCost <= AmountEpsilon)
            {
                demand.RemainingItems = 0;
                return;
            }

            while (!demand.IsSatisfied && MatchesItem(entry, demand))
            {
                demand.RemainingItems--;
                entry.RemainingCondition = Math.Max(0f, entry.RemainingCondition - conditionCost);
            }
        }

        private static bool MatchesItem(InventoryEntry entry, Demand demand)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id))
            {
                return false;
            }

            RecipeItem requirement = demand.Requirement;
            if (!string.IsNullOrEmpty(requirement.ignoredId) && entry.Id == requirement.ignoredId)
            {
                return false;
            }

            if (entry.ContainsItems)
            {
                return false;
            }

            float condition = entry.RemainingCondition;
            if (condition < requirement.minimumCondition ||
                (demand.UsesDurability && condition <= AmountEpsilon))
            {
                return false;
            }

            if (demand.Template.Resource.HasValue)
            {
                ResourceKey resource = demand.Template.Resource.Value;
                if (resource.IsLiquid || entry.Id != resource.Id)
                {
                    return false;
                }

                return requirement.specific || requirement.quality == null ||
                       entry.Qualities != null &&
                       Item.GetQualityThatMeetsCriteria(requirement.quality, entry.Qualities) != null;
            }

            return requirement.quality != null && entry.Qualities != null &&
                   Item.GetQualityThatMeetsCriteria(requirement.quality, entry.Qualities) != null;
        }

        private static bool AllocateLiquid(InventoryEntry entry, Demand demand)
        {
            if (entry.Liquids.Count == 0)
            {
                return false;
            }

            bool allocated = false;
            foreach (LiquidPool pool in entry.Liquids)
            {
                if (pool.RemainingAmount <= AmountEpsilon || demand.IsSatisfied)
                {
                    continue;
                }

                float contributionPerMilliliter = ContributionPerMilliliter(pool.Id, demand);
                if (contributionPerMilliliter <= 0f)
                {
                    continue;
                }

                float neededVolume = demand.RemainingAmount / contributionPerMilliliter;
                float usedVolume = Math.Min(pool.RemainingAmount, neededVolume);
                if (usedVolume <= AmountEpsilon)
                {
                    continue;
                }

                pool.RemainingAmount -= usedVolume;
                if (pool.RemainingAmount < 0.5f)
                {
                    pool.RemainingAmount = 0f;
                }

                demand.RemainingAmount = Math.Max(
                    0f,
                    demand.RemainingAmount - usedVolume * contributionPerMilliliter);
                allocated = true;
            }

            return allocated;
        }

        private static float ContributionPerMilliliter(string liquidId, Demand demand)
        {
            if (!demand.IsAbstractQuality)
            {
                return demand.Template.Resource.HasValue &&
                       demand.Template.Resource.Value.IsLiquid &&
                       demand.Template.Resource.Value.Id == liquidId
                    ? 1f
                    : 0f;
            }

            RecipeItem requirement = demand.Requirement;
            if (requirement.quality == null || Liquids.Registry == null ||
                !Liquids.Registry.TryGetValue(liquidId, out LiquidType liquid) || liquid?.qualities == null)
            {
                return 0f;
            }

            foreach (CraftingQuality quality in liquid.qualities)
            {
                if (quality.id == requirement.quality.id && quality.amount > 0f)
                {
                    return quality.amount;
                }
            }

            return 0f;
        }

        private static List<InventoryEntry> ScanAvailableItems(Body body)
        {
            List<InventoryEntry> entries = new List<InventoryEntry>();
            HashSet<int> seen = new HashSet<int>();

            List<Item> heldItems = body.GetAllItemsThorough();
            if (heldItems != null)
            {
                foreach (Item item in heldItems)
                {
                    AddInventoryEntry(item, seen, entries);
                }
            }

            // 与原版制作范围保持一致，并通过实例 ID 去重，防止手持物同时被附近扫描重复计入。
            Collider2D[] nearby = Physics2D.OverlapCircleAll(
                body.transform.position,
                10f,
                LayerMask.GetMask(new[] { "Item" }));
            foreach (Collider2D collider in nearby)
            {
                Item item;
                if (collider != null && collider.TryGetComponent(out item) && body.DoPickupCheck(item, true))
                {
                    AddInventoryEntry(item, seen, entries);
                }
            }

            return entries;
        }

        private static void AddInventoryEntry(
            Item item,
            HashSet<int> seen,
            List<InventoryEntry> entries)
        {
            if (!item || item.favourited || !seen.Add(item.GetInstanceID()))
            {
                return;
            }

            InventoryEntry entry = new InventoryEntry
            {
                Id = item.id,
                Qualities = item.Stats.qualities,
                RemainingCondition = item.condition
            };

            if (item.TryGetComponent(out Container container))
            {
                entry.ContainsItems = container.itemCount > 0;
            }

            if (item.TryGetComponent(out WaterContainerItem waterContainer) && waterContainer.stack != null)
            {
                foreach (LiquidStack stack in waterContainer.stack)
                {
                    if (stack == null || string.IsNullOrEmpty(stack.liquidId) || stack.amount <= AmountEpsilon)
                    {
                        continue;
                    }

                    entry.Liquids.Add(new LiquidPool
                    {
                        Id = stack.liquidId,
                        RemainingAmount = stack.amount
                    });
                }
            }

            entries.Add(entry);
        }

        private static void AddPlannedOutputs(
            List<InventoryEntry> inventory,
            Recipe producer,
            int craftRuns,
            bool reserveForDurability,
            CraftingTreeNode source)
        {
            if (producer?.result == null || craftRuns <= 0)
            {
                return;
            }

            int outputPerCraft = Math.Max(1, producer.result.amount);
            // 记录产物依赖链，CanAllocate 据此阻止副产物在环中“凭空启动”自己的生产过程。
            HashSet<ResourceKey> dependencies = new HashSet<ResourceKey>();
            CollectDependencyResources(source, dependencies);
            if (producer.result.isLiquid)
            {
                float outputAmount = producer.result.resultCondition * outputPerCraft * craftRuns;
                if (outputAmount <= AmountEpsilon)
                {
                    return;
                }

                InventoryEntry liquidOutput = new InventoryEntry
                {
                    Id = producer.result.id,
                    LiquidAllocated = true,
                    PlannedBy = source,
                    PlannedDependencies = dependencies
                };
                liquidOutput.Liquids.Add(new LiquidPool
                {
                    Id = producer.result.id,
                    RemainingAmount = outputAmount
                });
                inventory.Add(liquidOutput);
                return;
            }

            List<CraftingQuality> qualities = null;
            if (Item.GlobalItems != null &&
                Item.GlobalItems.TryGetValue(producer.result.id, out ItemInfo itemInfo))
            {
                qualities = itemInfo?.qualities;
            }

            long outputCount = (long)craftRuns * outputPerCraft;
            int safeOutputCount = (int)Math.Min(int.MaxValue, outputCount);
            for (int index = 0; index < safeOutputCount; index++)
            {
                inventory.Add(new InventoryEntry
                {
                    Id = producer.result.id,
                    Qualities = qualities,
                    RemainingCondition = producer.result.resultCondition,
                    DurabilityAllocated = reserveForDurability,
                    PlannedBy = source,
                    PlannedDependencies = dependencies
                });
            }
        }

        private static void CollectDependencyResources(
            CraftingTreeNode node,
            HashSet<ResourceKey> resources)
        {
            if (node == null)
            {
                return;
            }

            if (node.Resource.HasValue)
            {
                resources.Add(node.Resource.Value);
            }

            foreach (CraftingTreeNode child in node.Children)
            {
                CollectDependencyResources(child, resources);
            }
        }

        private static void AddLeaf(
            List<RemainingMaterial> materials,
            Dictionary<RemainingMaterialKey, int> materialIndices,
            Demand demand)
        {
            // 分配阶段已经处理了界面上不可见的阈值；最终清单按玩家可见身份合并，
            // 让视觉上相同的需求共用一行，同时仍区分耐久工具与消耗品。
            RemainingMaterialKey key = new RemainingMaterialKey(demand);
            if (!materialIndices.TryGetValue(key, out int index))
            {
                RemainingMaterial material = new RemainingMaterial
                {
                    Kind = GetKind(demand),
                    Resource = demand.Template.Resource,
                    Requirement = demand.Requirement,
                    ItemCount = demand.IsLiquid ? 0 : demand.RemainingItems,
                    Amount = demand.IsLiquid ? demand.RemainingAmount : 0f
                };
                materialIndices.Add(key, materials.Count);
                materials.Add(material);
                return;
            }

            RemainingMaterial existing = materials[index];
            if (demand.IsLiquid)
            {
                existing.Amount += demand.RemainingAmount;
            }
            else
            {
                existing.ItemCount += demand.RemainingItems;
            }
        }

        private static RemainingMaterialKind GetKind(Demand demand)
        {
            if (demand.IsLiquid)
            {
                return demand.IsAbstractQuality
                    ? RemainingMaterialKind.QualityLiquid
                    : RemainingMaterialKind.ConcreteLiquid;
            }

            return demand.IsAbstractQuality
                ? RemainingMaterialKind.QualityItem
                : RemainingMaterialKind.ConcreteItem;
        }
    }
}
