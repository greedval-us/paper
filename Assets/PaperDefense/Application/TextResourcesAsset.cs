using System;
using System.Collections.Generic;
using System.Globalization;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    [CreateAssetMenu(menuName = "Paper Defense/Text Resources", fileName = "ru")]
    public sealed class TextResourcesAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            [TextArea] public string value;
            public Entry() { }
            public Entry(string key, string value) { this.key = key; this.value = value; }
        }
        public string language = "ru";
        public string culture = "ru-RU";
        public List<Entry> entries = new List<Entry>();
        static UserTextResolver defaultResolver;
        public static UserTextResolver Default
        {
            get
            {
                if (defaultResolver != null) return defaultResolver;
                var asset = Resources.Load<TextResourcesAsset>("Text/ru");
                if (asset == null) throw new InvalidOperationException("Missing Resources/Text/ru text resource.");
                var candidate = asset.CreateResolver();
                UserTextContracts.Validate(candidate);
                defaultResolver = candidate;
                return defaultResolver;
            }
        }
        public UserTextResolver CreateResolver()
        {
            if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("Text resource language is required.");
            if (entries == null) throw new ArgumentException("Text resource entries are required.");
            var pairs = new List<KeyValuePair<string, string>>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry == null) throw new ArgumentException("Null text resource entry.");
                pairs.Add(new KeyValuePair<string, string>(entry.key, entry.value));
            }
            return new UserTextResolver(pairs, CultureInfo.GetCultureInfo(culture));
        }
    }
}
