using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml.Linq;

namespace ChannelFlip
{
    // Chinese source messages are stable translation keys, similar to gettext.
    // The embedded catalog keeps both languages available in a standalone EXE.
    public static class L10n
    {
        private static readonly Dictionary<string, string> catalog = LoadCatalog();
        private static string language = "zh-TW";
        public static string Language { get { return language; } }
        public static CultureInfo Culture { get { return CultureInfo.GetCultureInfo(language == "en" ? "en-US" : "zh-TW"); } }
        public static IEnumerable<KeyValuePair<string, string>> Translations { get { return catalog; } }
        public static bool IsSupported(string code) { return code == "zh-TW" || code == "en"; }
        public static string ForCulture(CultureInfo culture) { return culture.TwoLetterISOLanguageName == "zh" ? "zh-TW" : "en"; }
        public static void SetLanguage(string code)
        {
            if (!IsSupported(code)) throw new ArgumentException("Unsupported language: " + code);
            language = code;
        }
        private static Dictionary<string, string> LoadCatalog()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChannelFlip.Translations.xml"))
            {
                if (stream == null) throw new InvalidOperationException("The embedded translation catalog is missing.");
                return XElement.Load(stream).Elements("text").ToDictionary(e => (string)e.Element("zh"), e => (string)e.Element("en"), StringComparer.Ordinal);
            }
        }
        public static string T(string source, params object[] args)
        {
            string translated;
            if (!catalog.TryGetValue(source, out translated)) throw new KeyNotFoundException("Missing translation: " + source);
            string value = language == "en" ? translated : source;
            return args.Length == 0 ? value : String.Format(Culture, value, args);
        }
        public static LocalizedText M(string source, params object[] args) { return new LocalizedText(source, args); }
        public static void Apply(ResourceDictionary resources)
        {
            foreach (var entry in catalog) resources["Ui." + entry.Key] = language == "en" ? entry.Value : entry.Key;
        }
    }

    // Keep operation results as messages with arguments, so changing language
    // does not discard a completed test or freeze its text in the previous language.
    public sealed class LocalizedText
    {
        private readonly string source, literal;
        private readonly object[] args;
        internal LocalizedText(string source, object[] args) { this.source = source; this.args = args; }
        private LocalizedText(string literal) { this.literal = literal; }
        public override string ToString() { return source == null ? literal : L10n.T(source, args); }
        public static implicit operator LocalizedText(string value) { return value == null ? null : new LocalizedText(value); }
        public static string Render(LocalizedText value) { return value == null ? null : value.ToString(); }
    }
}
