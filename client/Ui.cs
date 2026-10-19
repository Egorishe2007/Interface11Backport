using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interface10
{
    // Building blocks: rects, the game's Bender faces, the game's sprites by name, 1.0's sprites from ui/<name>.png.
    internal static class Ui
    {
        private static TMP_FontAsset _regular;
        private static bool _fontsLooked;
        private static readonly Dictionary<string, Sprite> GameSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> FileSprites = new Dictionary<string, Sprite>();

        // The game's language (ru, en, ge, fr, es, es-mx, it, pl, cz, sk, hu, ro, po, tu, ch, jp, kr).
        internal static string Culture
        {
            get
            {
                var culture = EFT.LocalizationManager.Instance?.Culture;
                return string.IsNullOrEmpty(culture) ? "en" : culture.ToLowerInvariant();
            }
        }

        // A text given per language: the game's, else English.
        internal static string Pick(IDictionary<string, string> byLanguage) =>
            byLanguage.TryGetValue(Culture, out var text) || byLanguage.TryGetValue("en", out text) ? text : string.Empty;

        internal static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return (RectTransform)go.transform;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static T Layout<T>(GameObject go, TextAnchor alignment, int left = 0, int right = 0, int top = 0, int bottom = 0, float spacing = 0f)
            where T : HorizontalOrVerticalLayoutGroup
        {
            var layout = go.AddComponent<T>();
            layout.childAlignment = alignment;
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        internal static LayoutElement Element(GameObject go, float minWidth = -1f, float minHeight = -1f, float preferredWidth = -1f,
            float preferredHeight = -1f, bool ignore = false)
        {
            var element = go.AddComponent<LayoutElement>();
            element.minWidth = minWidth;
            element.minHeight = minHeight;
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.ignoreLayout = ignore;
            return element;
        }

        internal static TextMeshProUGUI Text(Transform parent, string name, float size, Color color)
        {
            FindFonts();
            var rect = Rect(parent, name);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_regular != null)
            {
                text.font = _regular;
            }

            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.richText = true;
            return text;
        }

        internal static Image Image(Transform parent, string name, Color color, Sprite sprite = null, Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // A sprite of the game's own UI by name, null when it is not loaded.
        internal static Sprite GameSprite(string name)
        {
            if (!GameSprites.TryGetValue(name, out var sprite) || sprite == null)
            {
                sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == name);
                GameSprites[name] = sprite;
            }

            return sprite;
        }

        // 1.0's sprites shipped with the mod: ui/<name>.png.
        internal static Sprite FileSprite(string name) => FileSprite(name, Vector4.zero);

        // The same with 1.0's 9-slice borders (left, bottom, right, top), for sliced and tiled images.
        internal static Sprite FileSprite(string name, Vector4 border)
        {
            var key = name + border;
            if (FileSprites.TryGetValue(key, out var sprite))
            {
                return sprite;
            }

            var path = Path.Combine(Interface10Plugin.Dir, "ui", name + ".png");
            if (File.Exists(path))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                if (texture.LoadImage(File.ReadAllBytes(path)))
                {
                    texture.name = name;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
                        SpriteMeshType.FullRect, border);
                    sprite.name = name;
                }
            }
            else
            {
                Interface10Plugin.Log.LogWarning($"Sprite missing: {path}");
            }

            FileSprites[key] = sprite;
            return sprite;
        }

        // One of the game's TMP fonts by its asset name, e.g. "Jovanny Lemonad - Bender Shadowed SDF".
        internal static TMP_FontAsset Font(string name) =>
            Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == name);

        // A RectTransform set to 1.0's values as the UI dumps list them: anchor min x, y, anchor max x, y,
        // position x, y, size x, y, pivot x, y.
        internal static void Place(RectTransform rect, float[] v)
        {
            rect.anchorMin = new Vector2(v[0], v[1]);
            rect.anchorMax = new Vector2(v[2], v[3]);
            rect.pivot = new Vector2(v[8], v[9]);
            rect.sizeDelta = new Vector2(v[6], v[7]);
            rect.anchoredPosition = new Vector2(v[4], v[5]);
        }

        // 1.0's layout of a screen's objects, by their paths under the screen; the ones 0.16 does not have are skipped.
        internal static void Place(Transform screen, IEnumerable<KeyValuePair<string, float[]>> layout)
        {
            foreach (var entry in layout)
            {
                if (screen.Find(entry.Key) is RectTransform rect)
                {
                    Place(rect, entry.Value);
                }
                else
                {
                    Interface10Plugin.Log.LogWarning($"{screen.name}: no {entry.Key}");
                }
            }
        }

        // The game's Bender, found among the loaded TMP fonts; Chinese, Japanese and Korean through the game's faces.
        private static void FindFonts()
        {
            if (_fontsLooked)
            {
                return;
            }

            _fontsLooked = true;
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            _regular = fonts.FirstOrDefault(f => f.name.IndexOf("Bender", StringComparison.OrdinalIgnoreCase) >= 0
                                                 && f.name.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) < 0
                                                 && f.name.IndexOf("Italic", StringComparison.OrdinalIgnoreCase) < 0)
                       ?? TMP_Settings.defaultFontAsset;
            if (_regular == null || (_regular.HasCharacter('中', true) && _regular.HasCharacter('한', true) && _regular.HasCharacter('の', true)))
            {
                return;
            }

            var first = Culture == "jp" ? "Japanese" : Culture == "kr" ? "Korean" : "Chinese";
            _regular.fallbackFontAssetTable = _regular.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
            foreach (var face in fonts.Where(f => f.name.Contains("Japanese") || f.name.Contains("Korean") || f.name.Contains("Chinese"))
                         .OrderBy(f => f.name.Contains(first) ? 0 : 1)
                         .Where(f => !_regular.fallbackFontAssetTable.Contains(f)))
            {
                _regular.fallbackFontAssetTable.Add(face);
            }
        }
    }
}
