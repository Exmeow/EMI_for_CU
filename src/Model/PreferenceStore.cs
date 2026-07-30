using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Newtonsoft.Json;

namespace EMI
{
    /// <summary>
    /// 保存图鉴中的默认配方与性质选择，并把跨会话标识解析回本次运行时的游戏对象。
    /// </summary>
    internal static class PreferenceStore
    {
        private const int CurrentSchemaVersion = 1;
        private const string DirectoryName = "EMI";
        private const string FileName = "preferences.json";

        private static readonly Dictionary<ResourceKey, string> RecipeFingerprints =
            new Dictionary<ResourceKey, string>();
        private static readonly Dictionary<ResourceKey, Recipe> ResolvedRecipes =
            new Dictionary<ResourceKey, Recipe>();
        private static readonly Dictionary<QualityPreferenceKey, ResourceKey> QualityDefaults =
            new Dictionary<QualityPreferenceKey, ResourceKey>();

        private static bool _initialized;

        public static event Action Changed;

        private static string PreferenceDirectory => Path.Combine(Paths.PluginPath, DirectoryName);

        private static string PreferencePath => Path.Combine(PreferenceDirectory, FileName);

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Load();
        }

        public static void ResolveRecipes()
        {
            // Recipe 实例只在当前游戏进程有效；载入目录后必须通过稳定指纹重新解析。
            ResolvedRecipes.Clear();
            if (!CompendiumCatalog.IsReady)
            {
                return;
            }

            foreach (KeyValuePair<ResourceKey, string> preference in RecipeFingerprints)
            {
                Recipe recipe = CompendiumCatalog.GetProducers(preference.Key)
                    .FirstOrDefault(candidate =>
                        candidate != null &&
                        !candidate.isRepair &&
                        string.Equals(
                            RecipeFingerprint.Create(candidate),
                            preference.Value,
                            StringComparison.Ordinal));
                if (recipe != null)
                {
                    ResolvedRecipes[preference.Key] = recipe;
                }
                else
                {
                    EmiPlugin.Log?.LogWarning(
                        $"[EMI] Stored recipe preference could not be resolved: {preference.Key}");
                }
            }
        }

        public static Recipe GetRecipeDefault(ResourceKey resource)
        {
            ResolvedRecipes.TryGetValue(resource, out Recipe recipe);
            return recipe;
        }

        public static bool IsRecipeDefault(Recipe recipe)
        {
            if (recipe?.result == null || recipe.isRepair)
            {
                return false;
            }

            ResourceKey resource = new ResourceKey(recipe.result.id, recipe.result.isLiquid);
            return ResolvedRecipes.TryGetValue(resource, out Recipe selected) && selected == recipe;
        }

        public static bool ToggleRecipe(Recipe recipe)
        {
            if (recipe?.result == null || recipe.isRepair)
            {
                return false;
            }

            ResourceKey resource = new ResourceKey(recipe.result.id, recipe.result.isLiquid);
            if (ResolvedRecipes.TryGetValue(resource, out Recipe selected) && selected == recipe)
            {
                RecipeFingerprints.Remove(resource);
                ResolvedRecipes.Remove(resource);
            }
            else
            {
                RecipeFingerprints[resource] = RecipeFingerprint.Create(recipe);
                ResolvedRecipes[resource] = recipe;
            }

            SaveAndNotify();
            return true;
        }

        public static ResourceKey? GetQualityDefault(string qualityId, bool isLiquid)
        {
            QualityPreferenceKey key = new QualityPreferenceKey(qualityId, isLiquid);
            return QualityDefaults.TryGetValue(key, out ResourceKey resource)
                ? resource
                : (ResourceKey?)null;
        }

        public static bool IsQualityDefault(string qualityId, ResourceKey resource)
        {
            ResourceKey? selected = GetQualityDefault(qualityId, resource.IsLiquid);
            return selected.HasValue && selected.Value == resource;
        }

        public static bool ToggleQuality(string qualityId, ResourceKey resource)
        {
            if (string.IsNullOrEmpty(qualityId) ||
                !CompendiumCatalog.ResourceHasQuality(resource, qualityId))
            {
                return false;
            }

            QualityPreferenceKey key = new QualityPreferenceKey(qualityId, resource.IsLiquid);
            if (QualityDefaults.TryGetValue(key, out ResourceKey selected) && selected == resource)
            {
                QualityDefaults.Remove(key);
            }
            else
            {
                QualityDefaults[key] = resource;
            }

            SaveAndNotify();
            return true;
        }

        public static bool TryGetQualityCandidate(
            RecipeItem requirement,
            out ResourceCandidate candidate)
        {
            candidate = null;
            if (requirement == null || requirement.specific || requirement.quality == null)
            {
                return false;
            }

            ResourceKey? resource = GetQualityDefault(
                requirement.quality.id,
                requirement.isLiquid);
            if (!resource.HasValue)
            {
                return false;
            }

            candidate = RecipeCatalog.GetCandidates(requirement)
                .FirstOrDefault(option => option.Resource == resource.Value);
            return candidate != null;
        }

        private static void Load()
        {
            RecipeFingerprints.Clear();
            ResolvedRecipes.Clear();
            QualityDefaults.Clear();

            if (!File.Exists(PreferencePath))
            {
                return;
            }

            try
            {
                PreferenceDocument document = JsonConvert.DeserializeObject<PreferenceDocument>(
                    File.ReadAllText(PreferencePath, Encoding.UTF8));
                if (document == null || document.SchemaVersion != CurrentSchemaVersion)
                {
                    EmiPlugin.Log?.LogWarning(
                        $"[EMI] Unsupported preference schema: {document?.SchemaVersion.ToString() ?? "missing"}");
                    return;
                }

                if (document.RecipeDefaults != null)
                {
                    foreach (RecipePreferenceRecord record in document.RecipeDefaults)
                    {
                        if (record == null || string.IsNullOrEmpty(record.ResourceId) ||
                            string.IsNullOrEmpty(record.RecipeFingerprint))
                        {
                            continue;
                        }

                        RecipeFingerprints[new ResourceKey(record.ResourceId, record.IsLiquid)] =
                            record.RecipeFingerprint;
                    }
                }

                if (document.QualityDefaults != null)
                {
                    foreach (QualityPreferenceRecord record in document.QualityDefaults)
                    {
                        if (record == null || string.IsNullOrEmpty(record.QualityId) ||
                            string.IsNullOrEmpty(record.ResourceId))
                        {
                            continue;
                        }

                        QualityPreferenceKey key =
                            new QualityPreferenceKey(record.QualityId, record.IsLiquid);
                        QualityDefaults[key] = new ResourceKey(record.ResourceId, record.IsLiquid);
                    }
                }

                EmiPlugin.Log?.LogInfo(
                    $"[EMI] Preferences loaded. Recipes={RecipeFingerprints.Count}, " +
                    $"Qualities={QualityDefaults.Count}");
            }
            catch (Exception exception)
            {
                EmiPlugin.Log?.LogWarning(
                    $"[EMI] Could not load preferences; empty preferences will be used: {exception}");
                RecipeFingerprints.Clear();
                QualityDefaults.Clear();
            }
        }

        private static void SaveAndNotify()
        {
            Save();
            Changed?.Invoke();
        }

        private static void Save()
        {
            string temporaryPath = PreferencePath + ".tmp";
            try
            {
                Directory.CreateDirectory(PreferenceDirectory);
                PreferenceDocument document = new PreferenceDocument
                {
                    SchemaVersion = CurrentSchemaVersion,
                    RecipeDefaults = RecipeFingerprints
                        .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                        .Select(entry => new RecipePreferenceRecord
                        {
                            ResourceId = entry.Key.Id,
                            IsLiquid = entry.Key.IsLiquid,
                            RecipeFingerprint = entry.Value
                        })
                        .ToList(),
                    QualityDefaults = QualityDefaults
                        .OrderBy(entry => entry.Key.QualityId, StringComparer.Ordinal)
                        .ThenBy(entry => entry.Key.IsLiquid)
                        .Select(entry => new QualityPreferenceRecord
                        {
                            QualityId = entry.Key.QualityId,
                            IsLiquid = entry.Key.IsLiquid,
                            ResourceId = entry.Value.Id
                        })
                        .ToList()
                };

                string json = JsonConvert.SerializeObject(document, Formatting.Indented);
                // 先完整写入临时文件再替换，避免游戏或系统中断时把偏好文件截断。
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                if (File.Exists(PreferencePath))
                {
                    File.Replace(temporaryPath, PreferencePath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, PreferencePath);
                }
            }
            catch (Exception exception)
            {
                EmiPlugin.Log?.LogError($"[EMI] Could not save preferences: {exception}");
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // 保留最初的保存异常；清理临时文件失败不是更有价值的诊断信息。
                }
            }
        }

        private sealed class PreferenceDocument
        {
            [JsonProperty("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonProperty("recipeDefaults")]
            public List<RecipePreferenceRecord> RecipeDefaults { get; set; }

            [JsonProperty("qualityDefaults")]
            public List<QualityPreferenceRecord> QualityDefaults { get; set; }
        }

        private sealed class RecipePreferenceRecord
        {
            [JsonProperty("resourceId")]
            public string ResourceId { get; set; }

            [JsonProperty("isLiquid")]
            public bool IsLiquid { get; set; }

            [JsonProperty("recipeFingerprint")]
            public string RecipeFingerprint { get; set; }
        }

        private sealed class QualityPreferenceRecord
        {
            [JsonProperty("qualityId")]
            public string QualityId { get; set; }

            [JsonProperty("isLiquid")]
            public bool IsLiquid { get; set; }

            [JsonProperty("resourceId")]
            public string ResourceId { get; set; }
        }
    }
}
