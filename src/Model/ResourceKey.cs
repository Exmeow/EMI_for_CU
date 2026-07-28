using System;

namespace EMI
{
    internal readonly struct ResourceKey : IEquatable<ResourceKey>
    {
        public ResourceKey(string id, bool isLiquid)
        {
            Id = id ?? string.Empty;
            IsLiquid = isLiquid;
        }

        public string Id { get; }

        public bool IsLiquid { get; }

        public string DisplayName
        {
            get
            {
                return IsLiquid ? Locale.GetOther(Id) : Locale.GetItem(Id);
            }
        }

        public bool Equals(ResourceKey other)
        {
            return IsLiquid == other.IsLiquid && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Id != null ? Id.GetHashCode() : 0) * 397) ^ IsLiquid.GetHashCode();
            }
        }

        public override string ToString()
        {
            return (IsLiquid ? "liquid:" : "item:") + Id;
        }

        public static bool operator ==(ResourceKey left, ResourceKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ResourceKey left, ResourceKey right)
        {
            return !left.Equals(right);
        }
    }
}
