using Ironfield.Core;
using NUnit.Framework;

namespace Ironfield.Tests
{
    /// <summary>Covers the localization system added 2026-09-12 (user: "游戏要改成
    /// 支持英语、法语、日语、德语、西班牙语以及简体中文和繁体中文，默认语言是英语").
    /// The actual translated text isn't something an automated test can judge —
    /// this covers the two things that ARE mechanically checkable: every key has
    /// a non-empty entry for all 7 languages (a partial translation would
    /// otherwise silently fall back to English forever with no signal anything's
    /// missing), and the language setting itself persists/clamps correctly.</summary>
    public class LocTests
    {
        [TearDown]
        public void ResetLanguage() => GameSettings.SetLanguage(Language.English);

        [Test]
        public void Every_loc_key_has_all_seven_languages_translated()
        {
            var missing = new System.Collections.Generic.List<string>(Loc.KeysWithMissingTranslations());
            Assert.IsEmpty(missing, "these keys are missing a translation for at least one language: "
                + string.Join(", ", missing));
        }

        [Test]
        public void Default_language_is_English()
        {
            Assert.AreEqual(Language.English, GameSettings.Language);
        }

        [Test]
        public void Get_switches_text_when_the_language_setting_changes()
        {
            GameSettings.SetLanguage(Language.English);
            string en = Loc.Get("menu.start");
            GameSettings.SetLanguage(Language.ChineseSimplified);
            string zh = Loc.Get("menu.start");

            Assert.AreEqual("Start Mission", en);
            Assert.AreEqual("开始任务", zh);
            Assert.AreNotEqual(en, zh);
        }

        [Test]
        public void Get_falls_back_to_the_key_itself_for_an_unknown_key()
        {
            Assert.AreEqual("this.key.does.not.exist", Loc.Get("this.key.does.not.exist"));
        }

        [Test]
        public void Language_names_array_has_one_entry_per_language_and_matches_enum_order()
        {
            int count = System.Enum.GetValues(typeof(Language)).Length;
            Assert.AreEqual(count, Loc.LanguageNames.Length);
            Assert.AreEqual("English", Loc.LanguageNames[(int)Language.English]);
            Assert.AreEqual("简体中文", Loc.LanguageNames[(int)Language.ChineseSimplified]);
            Assert.AreEqual("繁體中文", Loc.LanguageNames[(int)Language.ChineseTraditional]);
        }
    }
}
