namespace EMI
{
    internal sealed class ResourceCandidate
    {
        public ResourceCandidate(ResourceKey resource, float requiredLiquidAmount = 0f)
        {
            Resource = resource;
            RequiredLiquidAmount = requiredLiquidAmount;
        }

        public ResourceKey Resource { get; }

        public float RequiredLiquidAmount { get; }
    }
}
