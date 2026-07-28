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
                if (!IsLiquid)
                {
                    if (Item.GlobalItems != null &&
                        Item.GlobalItems.TryGetValue(Id, out ItemInfo item) &&
                        !string.IsNullOrEmpty(item?.fullName))
                    {
                        return item.fullName;
                    }

                    return Locale.GetItem(Id);
                }

                if (Liquids.Registry != null &&
                    Liquids.Registry.TryGetValue(Id, out LiquidType liquid) &&
                    liquid != null)
                {
                    return liquid.localeFromItem
                        ? Locale.GetItem(Id)
                        : Locale.GetOther(liquid.localeName);
                }

                return Locale.GetOther(Id);
            }
        }

        public string Description
        {
            get
            {
                if (!IsLiquid)
                {
                    if (Item.GlobalItems != null &&
                        Item.GlobalItems.TryGetValue(Id, out ItemInfo item) &&
                        !string.IsNullOrEmpty(item?.description))
                    {
                        return item.description;
                    }

                    return Locale.GetItem(Id + "dsc");
                }

                if (Liquids.Registry != null &&
                    Liquids.Registry.TryGetValue(Id, out LiquidType liquid) &&
                    liquid != null)
                {
                    string localeId = liquid.localeFromItem ? Id : liquid.localeName;
                    return Locale.GetOther(localeId + "dsc");
                }

                return Locale.GetOther(Id + "dsc");
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
