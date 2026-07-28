using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EMI
{
    internal static class ResourceIconProvider
    {
        private sealed class IconVisual
        {
            public Sprite Sprite;
            public Color Color;
        }

        private static readonly Dictionary<ResourceKey, IconVisual> Cache =
            new Dictionary<ResourceKey, IconVisual>();

        public static void Clear()
        {
            Cache.Clear();
        }

        public static void Apply(Image image, ResourceKey? resource)
        {
            if (!resource.HasValue)
            {
                image.sprite = null;
                image.color = new Color(0.12f, 0.14f, 0.13f, 1f);
                return;
            }

            IconVisual visual = Get(resource.Value);
            image.sprite = visual.Sprite;
            image.color = visual.Sprite != null
                ? visual.Color
                : new Color(0.12f, 0.14f, 0.13f, 1f);
            image.preserveAspect = true;
        }

        private static IconVisual Get(ResourceKey resource)
        {
            if (Cache.TryGetValue(resource, out IconVisual cached))
            {
                return cached;
            }

            IconVisual visual = new IconVisual
            {
                Color = Color.white
            };

            try
            {
                if (resource.IsLiquid)
                {
                    visual.Sprite = Resources.Load<Sprite>("Sprites/droplet");
                    if (Liquids.Registry != null &&
                        Liquids.Registry.TryGetValue(resource.Id, out LiquidType liquid))
                    {
                        visual.Color = liquid.color;
                    }
                }
                else
                {
                    GameObject prefab = Resources.Load<GameObject>(resource.Id);
                    SpriteRenderer renderer = prefab != null
                        ? prefab.GetComponent<SpriteRenderer>()
                        : null;
                    visual.Sprite = renderer != null ? renderer.sprite : null;
                }
            }
            catch (Exception exception)
            {
                EmiPlugin.Log?.LogWarning(
                    $"[EMI] Could not load icon for {resource}: {exception.Message}");
            }

            Cache[resource] = visual;
            return visual;
        }
    }
}
