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

        public List<RemainingMaterial> LiquidPreparations { get; } = new List<RemainingMaterial>();
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
            public bool DestroyAtZeroCondition;
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
            public float AmountPerUse;
            public bool RequiresLiquidPreparation;

            public RecipeItem Requirement => Template.Requirement;

            public bool IsLiquid => Requirement != null && Requirement.isLiquid;

            public bool UsesDurability => Template.UsesDurability;

            public bool IsAbstractQuality => Template.IsQualityRequirement && !Template.Resource.HasValue;

            public bool IsSatisfied => IsLiquid
                ? RemainingAmount <= AmountEpsilon
                : RemainingItems <= 0;
        }

        // 这里只按最终清单可见身份分组。ignoredId 和性质强度已在逐需求分配时处理，
        // 不应拆分显示相同的剩余材料；完整约束仍保留在需求分组与 QualityRequirementKey 中。
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
                NoMaxCondition = requirement?.noMaxCondition ?? false;
            }

            private RemainingMaterialKind Kind { get; }

            private ResourceKey Resource { get; }

            private bool HasResource { get; }

            private string QualityId { get; }

            private float MinimumCondition { get; }

            private bool UsesDurability { get; }

            private bool NoMaxCondition { get; }

            public bool Equals(RemainingMaterialKey other)
            {
                return Kind == other.Kind &&
                       HasResource == other.HasResource &&
                       (!HasResource || Resource == other.Resource) &&
                       MinimumCondition.Equals(other.MinimumCondition) &&
                       UsesDurability == other.UsesDurability &&
                       NoMaxCondition == other.NoMaxCondition &&
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
                    hashCode = (hashCode * 397) ^ NoMaxCondition.GetHashCode();
                    return hashCode;
                }
            }
        }

        public static CraftingPlanResult Calculate(CraftingTreeNode root, Body body)
        {
            return root == null || body == null
                ? new CraftingPlanResult()
                : CalculateWithInventory(root, ScanAvailableItems(body));
        }

        private static CraftingPlanResult CalculateWithInventory(
            CraftingTreeNode root, List<InventoryEntry> inventory)
        {
            CraftingPlanResult result = new CraftingPlanResult();
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
            List<Demand> allDemands = new List<Demand>(level);

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
                        int craftRuns = RequiredCraftRuns(demand, producer, inventory);
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

                allDemands.AddRange(nextLevel);
                level = nextLevel;
            }

            foreach (Demand demand in pendingLeaves)
            {
                if (demand.IsSatisfied)
                {
                    continue;
                }

                // 不足一份的余量只在最终叶节点扣抵，不能提前把半份需求当成可执行制作。
                ReserveLiquidRemainder(demand, inventory);
                if (demand.IsSatisfied)
                {
                    continue;
                }
                AddLeaf(result.RemainingMaterials, materialIndices, demand);
                RegisterLeafProducers(result.LeafProducerRecipes, demand);
            }

            foreach (Demand demand in allDemands)
            {
                if (demand.RequiresLiquidPreparation)
                {
                    result.LiquidPreparations.Add(new RemainingMaterial
                    {
                        Kind = GetKind(demand),
                        Resource = demand.Template.Resource,
                        Requirement = demand.Requirement,
                        Amount = demand.AmountPerUse
                    });
                }
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
                demand.AmountPerUse = amountPerRequirement;
            }
            else if (requirement.quality != null)
            {
                demand.AmountPerUse = requirement.quality.amount;
            }

            demand.RemainingAmount = demand.AmountPerUse * template.RequirementMultiplicity * runs;
            return demand;
        }

        private static int RequiredCraftRuns(Demand demand, Recipe producer, List<InventoryEntry> inventory)
        {
            if (demand.IsLiquid)
            {
                float outputPerCraft = producer.result.resultCondition * Math.Max(1, producer.result.amount);
                float missingAmount = Math.Max(0f, demand.RemainingAmount -
                    AvailableLiquidContribution(demand, inventory, producer.result.id));
                return outputPerCraft > AmountEpsilon
                    ? Math.Max(1, (int)Math.Ceiling(missingAmount / outputPerCraft))
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

            // 先让所有需求尝试单容器份额，避免小瓶余量抢走另一分支可直接使用的库存。
            foreach (Demand demand in demands)
            {
                AllocatePooledLiquid(demand, inventory);
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

            AllocatePooledLiquid(demand, inventory);
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
                entry.RemainingCondition = entry.RemainingCondition - conditionCost;
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
            if (!ItemConditionRules.Matches(condition, requirement, entry.DestroyAtZeroCondition))
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
            bool allocated = false;
            foreach (LiquidPool pool in entry.Liquids)
            {
                float contribution = ContributionPerMilliliter(pool.Id, demand);
                if (contribution <= 0f || demand.IsSatisfied || demand.AmountPerUse <= AmountEpsilon)
                {
                    continue;
                }

                // 一次制作必须由同一容器内的一种液体满足；批量制作允许逐份换容器。
                int uses = (int)Math.Min(
                    Math.Floor((pool.RemainingAmount * contribution + AmountEpsilon) / demand.AmountPerUse),
                    Math.Floor((demand.RemainingAmount + AmountEpsilon) / demand.AmountPerUse));
                if (uses <= 0)
                {
                    continue;
                }

                float usedContribution = uses * demand.AmountPerUse;
                ConsumeLiquidPool(pool, usedContribution / contribution);
                demand.RemainingAmount = Math.Max(0f, demand.RemainingAmount - usedContribution);
                allocated = true;
            }

            return allocated;
        }

        private static bool CanAllocateLiquid(InventoryEntry entry, Demand demand)
        {
            return !entry.Consumed && !entry.DurabilityAllocated && CanAllocate(entry, demand);
        }

        private static float AvailableLiquidContribution(
            Demand demand, List<InventoryEntry> inventory, string liquidId)
        {
            float contribution = ContributionPerMilliliter(liquidId, demand);
            float total = 0f;
            foreach (InventoryEntry entry in inventory)
            {
                if (!CanAllocateLiquid(entry, demand))
                {
                    continue;
                }

                foreach (LiquidPool pool in entry.Liquids)
                {
                    if (pool.Id == liquidId)
                    {
                        total += pool.RemainingAmount * contribution;
                    }
                }
            }

            return total;
        }

        private static HashSet<string> GetLiquidIds(Demand demand, List<InventoryEntry> inventory)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (InventoryEntry entry in inventory)
            {
                if (CanAllocateLiquid(entry, demand))
                {
                    foreach (LiquidPool pool in entry.Liquids)
                    {
                        if (pool.RemainingAmount > AmountEpsilon && ContributionPerMilliliter(pool.Id, demand) > 0f)
                        {
                            ids.Add(pool.Id);
                        }
                    }
                }
            }

            return ids;
        }

        private static void AllocatePooledLiquid(Demand demand, List<InventoryEntry> inventory)
        {
            if (!demand.IsLiquid || demand.IsSatisfied || demand.AmountPerUse <= AmountEpsilon)
            {
                return;
            }

            foreach (string liquidId in GetLiquidIds(demand, inventory))
            {
                float total = AvailableLiquidContribution(demand, inventory, liquidId);
                int uses = (int)Math.Min(
                    Math.Floor((total + AmountEpsilon) / demand.AmountPerUse),
                    Math.Floor((demand.RemainingAmount + AmountEpsilon) / demand.AmountPerUse));
                if (uses > 0)
                {
                    ReserveLiquid(demand, inventory, liquidId, uses * demand.AmountPerUse);
                    demand.RequiresLiquidPreparation = true;
                }
            }
        }

        private static void ReserveLiquidRemainder(Demand demand, List<InventoryEntry> inventory)
        {
            if (!demand.IsLiquid || demand.IsSatisfied)
            {
                return;
            }

            // 不同液体的性质值不能凑成一份。余量只选择贡献最多的同种液体扣抵缺口。
            string bestId = null;
            float best = 0f;
            foreach (string liquidId in GetLiquidIds(demand, inventory))
            {
                float total = AvailableLiquidContribution(demand, inventory, liquidId);
                if (total > best)
                {
                    best = total;
                    bestId = liquidId;
                }
            }

            if (bestId != null)
            {
                ReserveLiquid(demand, inventory, bestId, Math.Min(best, demand.RemainingAmount));
                demand.RequiresLiquidPreparation = true;
            }
        }

        private static void ReserveLiquid(
            Demand demand, List<InventoryEntry> inventory, string liquidId, float contributionNeeded)
        {
            float contribution = ContributionPerMilliliter(liquidId, demand);
            foreach (InventoryEntry entry in inventory)
            {
                if (!CanAllocateLiquid(entry, demand))
                {
                    continue;
                }

                foreach (LiquidPool pool in entry.Liquids)
                {
                    if (pool.Id != liquidId || contributionNeeded <= AmountEpsilon)
                    {
                        continue;
                    }

                    float used = Math.Min(pool.RemainingAmount * contribution, contributionNeeded);
                    ConsumeLiquidPool(pool, used / contribution);
                    contributionNeeded -= used;
                    demand.RemainingAmount = Math.Max(0f, demand.RemainingAmount - used);
                    entry.LiquidAllocated |= used > 0f;
                }
            }
        }

        private static void ConsumeLiquidPool(LiquidPool pool, float amount)
        {
            pool.RemainingAmount = Math.Max(0f, pool.RemainingAmount - amount);
            if (pool.RemainingAmount < 0.5f)
            {
                pool.RemainingAmount = 0f;
            }
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
                DestroyAtZeroCondition = item.Stats.destroyAtZeroCondition,
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
                float outputAmount = producer.result.resultCondition;
                if (outputAmount <= AmountEpsilon)
                {
                    return;
                }

                for (long index = 0; index < (long)outputPerCraft * craftRuns; index++)
                {
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
                }
                return;
            }

            ItemInfo itemInfo = null;
            Item.GlobalItems?.TryGetValue(producer.result.id, out itemInfo);

            long outputCount = (long)craftRuns * outputPerCraft;
            int safeOutputCount = (int)Math.Min(int.MaxValue, outputCount);
            for (int index = 0; index < safeOutputCount; index++)
            {
                InventoryEntry output = new InventoryEntry
                {
                    Id = producer.result.id,
                    Qualities = itemInfo?.qualities,
                    DestroyAtZeroCondition = itemInfo?.destroyAtZeroCondition ?? true,
                    RemainingCondition = ItemConditionRules.ProducedCondition(producer.result, itemInfo),
                    DurabilityAllocated = reserveForDurability,
                    PlannedBy = source,
                    PlannedDependencies = dependencies
                };
                if (producer.result.dontDrainResultLiquid && itemInfo is LiquidItemInfo liquidInfo &&
                    liquidInfo.defaultContents != null)
                {
                    foreach (LiquidStack stack in liquidInfo.defaultContents)
                    {
                        output.Liquids.Add(new LiquidPool { Id = stack.liquidId, RemainingAmount = stack.amount });
                    }
                }

                inventory.Add(output);
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
