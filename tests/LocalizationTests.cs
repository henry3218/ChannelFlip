using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ChannelFlip;

public static class LocalizationTests
{
    private static string Slots(string value)
    { return String.Join(",", Regex.Matches(value, @"\{([0-9]+)(?:[^}]*)\}").Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(s => s)); }
    public static void Run(Action<bool, string> check, string directory)
    {
        string previous = L10n.Language;
        try
        {
            var catalog = L10n.Translations.ToArray();
            check(catalog.Length > 0 && catalog.All(p => !String.IsNullOrWhiteSpace(p.Value) && Slots(p.Key) == Slots(p.Value)),
                "All translation entries have English text and matching format arguments");
            bool formatsValid = true;
            foreach (string code in new[] { "zh-TW", "en" })
            {
                L10n.SetLanguage(code);
                foreach (var entry in catalog)
                    try { L10n.T(entry.Key, Enumerable.Range(0, 10).Select(i => (object)("arg" + i)).ToArray()); }
                    catch (FormatException) { formatsValid = false; }
            }
            check(formatsValid, "Every translated format string can be rendered in both languages");
            check(L10n.ForCulture(CultureInfo.GetCultureInfo("zh-TW")) == "zh-TW" &&
                L10n.ForCulture(CultureInfo.GetCultureInfo("en-GB")) == "en" && L10n.ForCulture(CultureInfo.GetCultureInfo("fr-FR")) == "en",
                "First-run language uses Chinese for Chinese locales and English otherwise");
            string prefs = Path.Combine(directory, "language-preferences");
            var preference = new DevicePreference { Id = "device-identity", Name = "User-supplied 裝置名稱", FollowDefault = true, Language = "en" };
            preference.Save(prefs); var saved = DevicePreference.Load(prefs);
            check(saved.Language == "en" && saved.Id == preference.Id && saved.Name == preference.Name && saved.FollowDefault,
                "Saved English preference retains endpoint identity, name, and selection mode");
            preference.Language = "zh-TW"; preference.Save(prefs);
            check(DevicePreference.Load(prefs).Language == "zh-TW", "A language change persists across preference reloads");
            File.WriteAllText(Path.Combine(prefs, "preferences.xml"), "<preferences mode=\"fixed\" language=\"unsupported\"><id>kept</id></preferences>");
            saved = DevicePreference.Load(prefs);
            check(saved.Language == null && saved.Id == "kept", "Unknown saved language falls back without discarding the device preference");

            L10n.SetLanguage("zh-TW");
            var session = Scenarios.Create("waiting"); var backend = (SimulationBackend)session.Backend;
            session.TestAsync(0, CancellationToken.None).GetAwaiter().GetResult();
            session.TestAsync(1, CancellationToken.None).GetAwaiter().GetResult(); session.Observation.ConfirmHearing();
            DateTime tested = session.Observation.TestUtc;
            session.Clock = delegate { return tested.AddSeconds(9); }; session.Refresh();
            var window = new MainWindow(session, true);
            try
            {
                string id = session.Selected.Id, name = session.Selected.Name;
                int calls = backend.Calls.Count;
                var language = (ComboBox)window.View.FindName("LanguageChoice");
                language.SelectedIndex = 1;
                var toggle = (ToggleButton)window.View.FindName("SwapSwitch");
                check(L10n.Language == "en" && new ToggleButtonAutomationPeer(toggle).GetName() == "Swap left/right" &&
                    (string)((Button)window.View.FindName("TestLeft")).Content == "Test left source channel" && String.Equals(window.View.Language.IetfLanguageTag, "en-US", StringComparison.OrdinalIgnoreCase),
                    "Language selector updates visible controls, accessible names, and spoken language metadata");
                check(session.Presentation.Title == "No new audio right now" && session.Observation.HearingConfirmed && session.Observation.TestUtc == tested &&
                    session.Message.Contains("right source channel") && session.Message.Contains("left ear"),
                    "English preserves confirmed idle evidence and retranslates the completed tone result with the correct ear");
                check(session.Selected.Id == id && session.Selected.Name == name && session.State.Enabled && backend.Calls.Count == calls,
                    "Language selection preserves the device and swap setting without calling the audio backend");
                language.SelectedIndex = 0;
                check(session.Presentation.Title == "目前沒有新音訊" && session.Message.Contains("左耳") && session.Observation.HearingConfirmed &&
                    AutomationProperties.GetName(toggle) == "左右互換", "Switching back restores Chinese without clearing the test result");
            }
            finally { window.View.Close(); }

            L10n.SetLanguage("en");
            var busy = new MainWindow(Scenarios.Create("busy"), true);
            try
            {
                busy.ChangeLanguage("zh-TW");
                check(!((ComboBox)busy.View.FindName("LanguageChoice")).IsEnabled && L10n.Language == "en", "Pending audio operations block language changes");
            }
            finally { busy.View.Close(); }
            var failed = Scenarios.Create("waiting"); ((SimulationBackend)failed.Backend).NoProcessing = true;
            failed.TestAsync(0, CancellationToken.None).GetAwaiter().GetResult();
            check(failed.Presentation.Code == "test-failed" && failed.Presentation.Detail.Contains("expected core processing") && failed.ErrorDetail.Contains("Processed frames:"),
                "English failures include the test status and processing details");
            L10n.SetLanguage("zh-TW");
            check(failed.Presentation.Detail.Contains("預期的核心處理") && failed.ErrorDetail.Contains("測試處理計數"),
                "Stored application test failures also follow a later language change");

            foreach (string code in new[] { "zh-TW", "en" })
            foreach (string scenario in Scenarios.Names)
            {
                L10n.SetLanguage(code);
                var w = new MainWindow(Scenarios.Create(scenario), true);
                try
                {
                    var content = (FrameworkElement)w.View.Content;
                    content.Measure(new Size(584, 600)); content.Arrange(new Rect(0, 0, 584, 600)); content.UpdateLayout();
                    var test = (Button)w.View.FindName("TestRight");
                    var bounds = test.TransformToAncestor(content).TransformBounds(new Rect(test.RenderSize));
                    check(bounds.Bottom <= 600 && bounds.Right <= 584 && bounds.Top >= 0,
                        code + " " + scenario + ": translated primary test fits the small window");
                    var presentation = w.Session.Presentation;
                    check(code != "en" || !Regex.IsMatch(presentation.Title + presentation.Detail + presentation.Setting, @"[\p{IsCJKUnifiedIdeographs}]"),
                        code + " " + scenario + ": status uses the selected language");
                    UiTypography.Apply(w.View.Resources, 2.25);
                    content.Measure(new Size(584, 600)); content.Arrange(new Rect(0, 0, 584, 600)); content.UpdateLayout();
                    var scroll = (ScrollViewer)w.View.FindName("PageScroll"); scroll.ScrollToBottom(); content.UpdateLayout();
                    var advanced = (Button)w.View.FindName("Advanced");
                    bounds = advanced.TransformToAncestor(content).TransformBounds(new Rect(advanced.RenderSize));
                    check(scroll.ScrollableWidth == 0 && bounds.Bottom <= 600 && bounds.Right <= 584 && bounds.Top >= 0,
                        code + " " + scenario + ": large translated text keeps the bottom action reachable");
                }
                finally { w.View.Close(); }
            }
        }
        finally { L10n.SetLanguage(previous); }
    }
}
