using System;
using System.Collections.Generic;

namespace EMI
{
    /// <summary>
    /// 由随附 Demo/正式版 Recipes.SetUpRecipes 数据逐项核对的 v1 指纹映射。
    /// 同时锁定旧指纹、资源身份和正式版指纹；后续版本再变动时保留收藏，等待重新确认。
    /// </summary>
    internal static class LegacyRecipeMigration
    {
        private static readonly Dictionary<string, string[]> KnownChanges =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["woodscraps"] = new[] { "433934992DCE59DC69DABBAE98AF58077FC0D5E597063FCE090165543A24814D", "AAA8DF08D90FE53A46F0631B32BB1E5B3D5DF4C65B48E450425753FED54F5603" },
                ["nails"] = new[] { "B12828EC7E819604F709A070D668B65B40E427BC8EFCEE4F775449383BD68743", "FA7B1F3D06CE20AF2F5C8CBD3916F1DCE2B8E75F438678F32D4351B240384B8C" },
                ["stick"] = new[] { "E0F736C284766092146DD858D9E9845683F9B62376A5102C3F72E3C64A227506", "41BEF1F7DD7806BFBAB8A5E0DB69CAB60BF61FD8CE6E8E04807D56C7CAA4875D" },
                ["flimsyknife"] = new[] { "B2E5FEF4B5A84DE931FB867C98AE7F3179EEE5013AA9F7F2D77855CB83D33AEE", "1A3421C910217F76983DF0F64769F9F88C8726367D7CB5C2CB60DE1087EAB51D" },
                ["titaniumsheet"] = new[] { "D99ED1EF74B467DDD33119D2CC8CEAEB794B88EEA6C9E06E76D4884E8FCA44F0", "AE8BA8E03378D7CBDDE6EEB2D5AD44F255F175CBBEA1C0EFE4ACE8A5209F7871" },
                ["titaniumrod"] = new[] { "C0997A51FB27C4B6AAF925E2B0CFBBDC60E2CFAD251D28BC5C568839327F3B90", "3295F8A1BC7051B8E88719E7B789D83D65574D4675B0A58A6071DF6591443E78" },
                ["lightbulb"] = new[] { "4EDC3E74C841B0C222E637DFBAAD93DC05F2FE20EB2D3F4D13FFB2775C7C48A0", "95ACB4520A065076376260B245FB52C9E5EB82561558FFEC9D3F42141B35E699" },
                ["scaffoldingpack"] = new[] { "6D19618EF0887A74287CD8E9F192D21C43E8603F782D35D8E31CFEE1DBAD0F5B", "4AE5DC01D173A023F71FE9C84C9EF2336BB539B8CEF5123F259256432A767608" },
                ["drillrepairkit"] = new[] { "02612E0BC963DBAB68B4841565C7E6223266A1BC8F29F75E2A32F2C5C6AB779A", "315B0F7FC3E398A21111D70167A22FCB835C8A948E6C74EB04F5D0CDDF503D36" },
                ["campfire"] = new[] { "39987019CD03E586AEBE361A64003749BA8B4D02003AF9EFAFB48B307EBCED2F", "5169DAD141EEC00E63431EDBDC560EEE10937B5FFA24AFD783E2FE3EBB4E2516" }
            };

        public static Recipe Resolve(ResourceKey resource, string legacyFingerprint, IReadOnlyList<Recipe> producers)
        {
            if (resource.IsLiquid || !KnownChanges.TryGetValue(resource.Id, out string[] change) ||
                !string.Equals(legacyFingerprint, change[0], StringComparison.Ordinal))
            {
                return null;
            }

            Recipe match = null;
            foreach (Recipe candidate in producers)
            {
                if (candidate?.result == null || candidate.isRepair || candidate.result.isLiquid ||
                    candidate.result.id != resource.Id || RecipeFingerprint.Create(candidate) != change[1])
                {
                    continue;
                }

                if (match != null)
                {
                    return null; // 即便结构相同，多个候选也不自动替用户选择。
                }

                match = candidate;
            }

            return match;
        }
    }
}
