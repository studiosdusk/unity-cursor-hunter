using System;
using System.Reflection;
using CursorHunter.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Tests
{
    public sealed class LocalizationTests
    {
        private string _language;
        [SetUp] public void Setup() { _language = LocalizationCatalog.Language; }
        [TearDown] public void Cleanup() { LocalizationCatalog.SetLanguage(_language); }

        [Test]
        public void NewLanguagesNormalizeAndNotifyOnlyWhenChanged()
        {
            LocalizationCatalog.SetLanguage("ko");
            int calls = 0;
            Action handler = () => calls++;
            LocalizationCatalog.Changed += handler;
            try
            {
                LocalizationCatalog.SetLanguage(" zh_Hans_CN ");
                Assert.That(LocalizationCatalog.Language, Is.EqualTo("zh-hans-cn"));
                LocalizationCatalog.SetLanguage("ZH-hans-CN");
                Assert.That(calls, Is.EqualTo(1));
                Assert.Throws<ArgumentException>(() => LocalizationCatalog.SetLanguage("bad locale"));
                Assert.That(LocalizationCatalog.Language, Is.EqualTo("zh-hans-cn"));
            }
            finally { LocalizationCatalog.Changed -= handler; }
        }

        [Test]
        public void ExistingEnglishAndKoreanUiEntriesKeepTheirFallbacks()
        {
            LocalizationCatalog.SetLanguage("en");
            string english = LocalizationCatalog.Get("ui.attack", "missing");
            Assert.That(english, Is.Not.EqualTo("missing"));
            LocalizationCatalog.SetLanguage("en-US");
            Assert.That(LocalizationCatalog.Get("ui.attack", "missing"), Is.EqualTo(english));
            LocalizationCatalog.SetLanguage("ko");
            string korean = LocalizationCatalog.Get("ui.attack", "missing");
            LocalizationCatalog.SetLanguage("ja");
            Assert.That(LocalizationCatalog.Get("ui.attack", "missing"), Is.EqualTo(korean));
            Assert.That(LocalizationCatalog.Get("missing.key", "fallback"), Is.EqualTo("fallback"));
        }

        [Test]
        public void AdditionalUiTranslationsCanBeAddedAsData()
        {
            FieldInfo entries = typeof(LocalizationCatalog).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Static);
            object original = entries.GetValue(null);
            try
            {
                entries.SetValue(null, new[] { new LocalizationCatalog.Entry
                {
                    key = "test", ko = "공격력", en = "Attack",
                    translations = new[] { new LocalizedText { locale = "ja", text = "攻撃力" } }
                } });
                LocalizationCatalog.SetLanguage("ja-JP");
                Assert.That(LocalizationCatalog.Get("test", "missing"), Is.EqualTo("攻撃力"));
                LocalizationCatalog.SetLanguage("fr");
                Assert.That(LocalizationCatalog.Get("test", "missing"), Is.EqualTo("공격력"));
            }
            finally { entries.SetValue(null, original); }
        }

        [Test]
        public void LocaleFontsUseRegionalParentThenDefaultFont()
        {
            var settings = ScriptableObject.CreateInstance<GameTypographySettings>();
            var fallback = new Font();
            var japanese = new Font();
            try
            {
                settings.sourceFont = fallback;
                settings.localeFonts = new[] { new GameTypographySettings.LocaleFont { locale = "ja", sourceFont = japanese } };
                Assert.That(settings.GetSourceFont("ja-JP"), Is.SameAs(japanese));
                Assert.That(settings.GetSourceFont("fr"), Is.SameAs(fallback));
            }
            finally { Object.DestroyImmediate(settings); Object.DestroyImmediate(fallback); Object.DestroyImmediate(japanese); }
        }

        [Test]
        public void LocalePrimaryFontRetainsKoreanFontForUntranslatedNames()
        {
            var settings = Resources.Load<GameTypographySettings>("GameData/TypographySettings");
            var original = settings.localeFonts;
            try
            {
                Font latin = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
                Assert.That(latin, Is.Not.Null);
                settings.localeFonts = new[] { new GameTypographySettings.LocaleFont { locale = "fr", sourceFont = latin } };
                LocalizationCatalog.SetLanguage("fr-CA");
                var primary = LocalizedTypography.GetFont(null);
                Assert.That(primary.sourceFontFile, Is.SameAs(latin));
                Assert.That(primary.fallbackFontAssetTable, Does.Contain(LocalizedTypography.GetFont(settings.sourceFont)));
                Assert.That(LocalizedTypography.GetFont(null), Is.SameAs(primary), "Repeated selection reuses the cached font.");
            }
            finally { settings.localeFonts = original; }
        }
    }
}
