using System;

namespace EMI
{
    /// <summary>
    /// 一类完整的抽象性质需求。性质强度、耐久阈值和是否消耗都会影响候选兼容性，必须参与键值。
    /// </summary>
    internal readonly struct QualityRequirementKey : IEquatable<QualityRequirementKey>
    {
        private QualityRequirementKey(
            string qualityId,
            float qualityAmount,
            bool isLiquid,
            float minimumCondition,
            bool destroyItem)
        {
            QualityId = qualityId ?? string.Empty;
            QualityAmount = qualityAmount;
            IsLiquid = isLiquid;
            MinimumCondition = minimumCondition;
            DestroyItem = destroyItem;
        }

        public string QualityId { get; }

        public float QualityAmount { get; }

        public bool IsLiquid { get; }

        public float MinimumCondition { get; }

        public bool DestroyItem { get; }

        public static bool TryCreate(RecipeItem requirement, out QualityRequirementKey key)
        {
            if (requirement == null || requirement.specific || requirement.quality == null)
            {
                key = default;
                return false;
            }

            key = new QualityRequirementKey(
                requirement.quality.id,
                requirement.quality.amount,
                requirement.isLiquid,
                requirement.minimumCondition,
                requirement.destroyItem);
            return true;
        }

        public bool Equals(QualityRequirementKey other)
        {
            return IsLiquid == other.IsLiquid &&
                   DestroyItem == other.DestroyItem &&
                   QualityAmount.Equals(other.QualityAmount) &&
                   MinimumCondition.Equals(other.MinimumCondition) &&
                   string.Equals(QualityId, other.QualityId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is QualityRequirementKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = QualityId != null ? QualityId.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ QualityAmount.GetHashCode();
                hashCode = (hashCode * 397) ^ IsLiquid.GetHashCode();
                hashCode = (hashCode * 397) ^ MinimumCondition.GetHashCode();
                hashCode = (hashCode * 397) ^ DestroyItem.GetHashCode();
                return hashCode;
            }
        }
    }
}
