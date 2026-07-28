using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EMI
{
    internal static class RecipeFingerprint
    {
        public static string Create(Recipe recipe)
        {
            if (recipe?.result == null)
            {
                return string.Empty;
            }

            List<string> ingredients = new List<string>();
            if (recipe.items != null)
            {
                foreach (RecipeItem item in recipe.items)
                {
                    ingredients.Add(IngredientToken(item));
                }
            }

            ingredients.Sort(StringComparer.Ordinal);

            // Recipe indexes and localized text are not stable across sessions or game updates.
            RecipeResult result = recipe.result;
            StringBuilder canonical = new StringBuilder();
            canonical.Append("v1|result|")
                .Append(result.id ?? string.Empty).Append('|')
                .Append(result.isLiquid ? '1' : '0').Append('|')
                .Append(result.amount.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(FloatToken(result.resultCondition)).Append('|')
                .Append(result.dontDrainResultLiquid ? '1' : '0').Append('|')
                .Append(recipe.isRepair ? '1' : '0');

            foreach (string ingredient in ingredients)
            {
                canonical.Append("|ingredient|").Append(ingredient);
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static string IngredientToken(RecipeItem item)
        {
            if (item == null)
            {
                return "null";
            }

            StringBuilder token = new StringBuilder();
            token.Append(item.specific ? 'S' : 'Q').Append('|')
                .Append(item.isLiquid ? '1' : '0').Append('|')
                .Append(item.destroyItem ? '1' : '0').Append('|')
                .Append(FloatToken(item.minimumCondition)).Append('|');

            if (item.specific)
            {
                token.Append(item.specificId ?? string.Empty);
            }
            else
            {
                token.Append(item.quality?.id ?? string.Empty).Append('|')
                    .Append(FloatToken(item.quality?.amount ?? 0f));
            }

            return token.ToString();
        }

        private static string FloatToken(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
