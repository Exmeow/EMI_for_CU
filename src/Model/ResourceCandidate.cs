namespace EMI
{
    /// <summary>
    /// 抽象性质需求的具体资源候选；液体同时记录满足一次需求所需的实际体积。
    /// </summary>
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
