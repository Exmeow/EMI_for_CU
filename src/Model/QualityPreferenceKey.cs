using System;

namespace EMI
{
    /// <summary>
    /// 持久化性质默认值的键。默认值按性质和资源类型共享，不绑定某一配方的具体强度阈值。
    /// </summary>
    internal readonly struct QualityPreferenceKey : IEquatable<QualityPreferenceKey>
    {
        public QualityPreferenceKey(string qualityId, bool isLiquid)
        {
            QualityId = qualityId ?? string.Empty;
            IsLiquid = isLiquid;
        }

        public string QualityId { get; }

        public bool IsLiquid { get; }

        public bool Equals(QualityPreferenceKey other)
        {
            return IsLiquid == other.IsLiquid &&
                   string.Equals(QualityId, other.QualityId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is QualityPreferenceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((QualityId != null ? QualityId.GetHashCode() : 0) * 397) ^
                       IsLiquid.GetHashCode();
            }
        }
    }
}
