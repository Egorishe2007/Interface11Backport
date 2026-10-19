using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Interface10
{
    // The mod's own texts in the game's 17 languages: lang/<language>.json, { "key": "text" }, a language missing a
    // text falls back to English, then to the English text in the code. The language is the game's own (ru, en, ge,
    // fr, es, es-mx, it, pl, cz, sk, hu, ro, po, tu, ch, jp, kr).
    internal static class Lang
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Texts = new Dictionary<string, Dictionary<string, string>>();

        internal static string Folder => Path.Combine(Interface10Plugin.Dir, "lang");

        internal static string Culture
        {
            get
            {
                // INTERFACE10_LANG: the mod's texts in another language than the game's (tests)
                var culture = Environment.GetEnvironmentVariable("INTERFACE10_LANG") ?? EFT.LocalizationManager.Instance?.Culture;
                return string.IsNullOrEmpty(culture) ? "en" : culture.ToLowerInvariant();
            }
        }

        internal static bool Russian => Culture == "ru";

        // The text of a key in the game's language.
        internal static string Get(string key, string english)
        {
            return Of(Culture).TryGetValue(key, out var text) || Of("en").TryGetValue(key, out text) ? text : english;
        }

        // The text of a key in the game's language only (for texts whose English another mod already shows).
        internal static bool TryGet(string key, out string text) => Of(Culture).TryGetValue(key, out text) && !string.IsNullOrEmpty(text);

        internal static string Format(string key, string english, params object[] args)
        {
            try
            {
                return string.Format(Get(key, english), args);
            }
            catch (FormatException)
            {
                return string.Format(english, args);
            }
        }

        // A text given per language (subtitles, tape names): the game's language, then English, then Russian.
        internal static string Pick(IDictionary<string, string> byLanguage)
        {
            if (byLanguage == null)
            {
                return null;
            }

            foreach (var language in new[] { Culture, "en", "ru" })
            {
                if (byLanguage.TryGetValue(language, out var text) && !string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return null;
        }

        private static Dictionary<string, string> Of(string language)
        {
            if (Texts.TryGetValue(language, out var texts))
            {
                return texts;
            }

            texts = new Dictionary<string, string>();
            var path = Path.Combine(Folder, language + ".json");
            if (File.Exists(path))
            {
                try
                {
                    texts = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path, Encoding.UTF8)) ?? texts;
                }
                catch (Exception ex)
                {
                    Interface10Plugin.Log.LogWarning($"Texts not loaded from {path}: {ex.Message}");
                }
            }

            Texts[language] = texts;
            return texts;
        }
    }
}
