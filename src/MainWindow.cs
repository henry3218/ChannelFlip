using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ChannelFlip
{
    public sealed class MainWindow
    {
        public readonly Window View;
        public readonly AudioSession Session;
        private readonly ComboBox devices, mode, language;
        private readonly DispatcherTimer timer;
        private readonly CancellationTokenSource closing = new CancellationTokenSource();
        private readonly bool simulation;
        private bool updating, dialogOpen;
        private string itemsKey;
        private readonly PreferenceWriter preferenceWriter = new PreferenceWriter();
        private T Get<T>(string name) where T : class { return View.FindName(name) as T; }

        public MainWindow(bool preview) : this(preview ? Scenarios.Create("waiting") : CreateLiveSession(), preview) { }
        public MainWindow(AudioSession session, bool simulated)
        {
            Session = session; simulation = simulated;
            using (Stream xaml = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChannelFlip.MainWindow.xaml"))
                View = (Window)XamlReader.Load(xaml);
            using (Stream icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChannelFlip.Icon"))
                if (icon != null) View.Icon = BitmapFrame.Create(icon, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            View.MinHeight = Math.Min(View.MinHeight, Math.Max(320, SystemParameters.WorkArea.Height - 32));
            View.MinWidth = Math.Min(View.MinWidth, Math.Max(320, SystemParameters.WorkArea.Width - 32));
            View.Height = Math.Min(View.Height, SystemParameters.WorkArea.Height - 32);
            View.Width = Math.Min(View.Width, SystemParameters.WorkArea.Width - 32);
            ApplyLanguage();
            UiTheme.Apply(View, null);
            ApplyTextSize();
            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            View.Activated += delegate { ApplyTextSize(); };
            devices = Get<ComboBox>("Devices"); mode = Get<ComboBox>("SelectionMode"); language = Get<ComboBox>("LanguageChoice");
            language.SelectionChanged += delegate { if (!updating) ChangeLanguage(language.SelectedIndex == 1 ? "en" : "zh-TW"); };
            Session.Changed += Update;
            devices.SelectionChanged += delegate
            {
                if (updating) return;
                Session.Select(devices.SelectedItem as OutputDevice);
            };
            mode.SelectionChanged += delegate
            {
                if (updating) return;
                Session.SetFollow(mode.SelectedIndex == 1);
            };
            Get<Button>("Refresh").Click += delegate { Session.Refresh(); };
            Get<Button>("SetupButton").Click += async delegate { await FocusAfter(Setup(), Get<Button>("TestLeft")); };
            var swapSwitch = Get<ToggleButton>("SwapSwitch");
            // UI Automation's TogglePattern changes IsChecked without raising Click.
            // Listen to state changes so pointer, keyboard and screen readers run the same operation.
            RoutedEventHandler swapChanged = async delegate
            { if (!updating) await FocusAfter(Session.ToggleAsync(swapSwitch.IsChecked == true), swapSwitch); };
            swapSwitch.Checked += swapChanged; swapSwitch.Unchecked += swapChanged;
            Get<Button>("TestLeft").Click += async delegate { await FocusAfter(Session.TestAsync(0, closing.Token), Get<Button>("TestLeft")); };
            Get<Button>("TestRight").Click += async delegate { await FocusAfter(Session.TestAsync(1, closing.Token), Get<Button>("TestRight")); };
            Get<Button>("ConfirmHearing").Click += delegate { Session.Observation.ConfirmHearing(); Update(); };
            Get<Button>("Advanced").Click += async delegate { await FocusAfter(Advanced(), Get<Button>("Advanced")); };
            Get<Button>("SoundSettings").Click += delegate
            {
                if (simulation) { Session.SetMessage(L10n.M("模擬模式不會開啟或修改 Windows 音效。")); Update(); return; }
                try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); }
                catch (Exception ex) { Session.ErrorDetail = ex.Message; Update(); }
            };
            Get<Button>("Help").Click += delegate { ShowHelp(); };
            Get<Button>("CopyError").Click += delegate
            {
                try { Clipboard.SetText(Get<TextBox>("ErrorText").Text); }
                catch (Exception ex) { Session.Message = L10n.T("無法複製：") + ex.Message; Update(); }
            };
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += delegate
            {
                ApplyTextSize();
                if (dialogOpen || Session.Busy) return;
                if (devices.IsDropDownOpen || mode.IsDropDownOpen || language.IsDropDownOpen) Update();
                else Session.Refresh();
            };
            View.Closed += delegate { timer.Stop(); closing.Cancel(); Session.Changed -= Update; SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; };
            View.Loaded += delegate { timer.Start(); devices.Focus(); };
            if (Session.Selected == null && Session.Devices.Count == 0) Session.Refresh();
            Update();
        }
        private static AudioSession CreateLiveSession()
        {
            DevicePreference preference; string error = null;
            try { preference = DevicePreference.Load(Program.DataDirectory); }
            catch (Exception ex) { preference = new DevicePreference(); error = L10n.T("無法讀取裝置偏好：") + ex.Message; }
            var session = new AudioSession(new WindowsAudioBackend(), preference);
            session.Refresh(); if (error != null) session.ErrorDetail = error;
            return session;
        }
        private async System.Threading.Tasks.Task FocusAfter(System.Threading.Tasks.Task operation, Control origin)
        {
            await operation;
            if (!dialogOpen && View.IsActive && Keyboard.FocusedElement == View && origin.IsVisible && origin.IsEnabled) origin.Focus();
        }
        private void SavePreference()
        {
            if (simulation) return;
            preferenceWriter.Save(Session.Preference, p => p.Save(Program.DataDirectory));
        }
        private void OnSystemParametersChanged(object sender, PropertyChangedEventArgs e)
        { View.Dispatcher.BeginInvoke((Action)delegate { UiTheme.Apply(View, null); ApplyTextSize(); Update(); }); }
        private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        { View.Dispatcher.BeginInvoke((Action)ApplyTextSize); }
        private void ApplyTextSize() { UiTypography.Apply(View.Resources, simulation ? 1.0 : UiTypography.SystemScale()); }
        private void ApplyLanguage()
        {
            L10n.Apply(View.Resources);
            View.Language = XmlLanguage.GetLanguage(L10n.Culture.Name);
            View.Title = L10n.T("左右聲道互換 · Channel Flip") + (simulation ? L10n.T(" · 模擬") : "");
        }
        public void ChangeLanguage(string code)
        {
            if (Session.Busy || dialogOpen || code == L10n.Language) return;
            L10n.SetLanguage(code); Session.Preference.Language = code;
            ApplyLanguage(); itemsKey = null; Update();
        }
        public void Update()
        {
            if (updating) return;
            updating = true;
            try
            {
                SavePreference();
                string key = L10n.Language + "|" + String.Join("\n", Session.Devices.Select(d => d.Id + "|" + d.DisplayName));
                if (itemsKey != key) { devices.ItemsSource = Session.Devices.ToList(); itemsKey = key; }
                devices.SelectedItem = devices.Items.Cast<OutputDevice>().FirstOrDefault(d => Session.Selected != null && d.Id == Session.Selected.Id);
                mode.SelectedIndex = Session.Preference.FollowDefault ? 1 : 0;
                language.SelectedIndex = L10n.Language == "en" ? 1 : 0;
                var p = Session.Presentation; var device = Session.Selected;
                bool busy = Session.Busy || dialogOpen;
                devices.IsEnabled = !busy; mode.IsEnabled = !busy; language.IsEnabled = !busy;
                Get<Button>("Refresh").IsEnabled = !busy;
                Get<TextBlock>("DeviceDetail").Text = Session.Preference.FollowDefault ?
                    L10n.T("操作目標跟隨多媒體預設；各裝置設定獨立保存。") :
                    device == null ? L10n.T("選擇要操作的耳機或喇叭。") : L10n.T("固定此裝置；斷線時保留選取。");
                Get<TextBlock>("SettingValue").Text = L10n.T("設定：") + p.Setting;
                Get<Button>("SetupButton").Visibility = Session.State.Attached ? Visibility.Collapsed : Visibility.Visible;
                Get<Button>("SetupButton").IsEnabled = p.CanSetup && !busy;
                var toggle = Get<ToggleButton>("SwapSwitch");
                toggle.Visibility = Session.State.Attached ? Visibility.Visible : Visibility.Collapsed;
                toggle.IsChecked = p.Swap; toggle.Content = p.Setting;
                toggle.IsEnabled = p.CanToggle && !busy;
                AutomationProperties.SetHelpText(toggle, Session.ReadError != null ? L10n.T("目前無法讀取設定；請重新整理。") :
                    device != null && device.Offline ? L10n.T("設定目前{0}；裝置已離線，重新連接後才能切換。", p.Setting) :
                    p.Swap ? L10n.T("目前開啟；按空白鍵關閉此裝置的互換。") : L10n.T("目前關閉；按空白鍵開啟此裝置的互換。"));
                Get<TextBlock>("RuntimeTitle").Text = Session.Busy ? L10n.T("正在處理…") : (p.Warning ? "！ " : p.Success ? "✓ " : "") + p.Title;
                Get<TextBlock>("RuntimeTitle").SetResourceReference(TextBlock.ForegroundProperty, p.Warning ? "Warning" : p.Success ? "SuccessText" : "Text");
                Get<TextBlock>("RuntimeDetail").Text = Session.Busy ? (Session.Message ?? L10n.T("請稍候。")) : p.Detail;
                bool unknown = device == null || Session.ReadError != null;
                Get<TextBlock>("LeftMapping").Text = unknown ? L10n.T("來源左 → 待確認") : p.Swap ? L10n.T("來源左 → 右耳") : L10n.T("來源左 → 左耳");
                Get<TextBlock>("RightMapping").Text = unknown ? L10n.T("來源右 → 待確認") : p.Swap ? L10n.T("來源右 → 左耳") : L10n.T("來源右 → 右耳");
                Get<Button>("TestLeft").IsEnabled = p.CanTest && !busy;
                Get<Button>("TestRight").IsEnabled = p.CanTest && !busy;
                var observation = Session.Observation;
                string evidence = observation.HearingConfirmed ? L10n.T("你已確認左右方向符合（{0}）。", observation.TestUtc.ToLocalTime().ToString("HH:mm:ss")) :
                    observation.TestUtc == DateTime.MinValue ? (p.CanTest ? L10n.T("左右測試可確認實際聽到的方向。") : L10n.T("裝置可用後，才能測試左右方向。")) :
                    observation.TestFailure != null ? L10n.T("上次測試未通過 · ") + observation.TestUtc.ToLocalTime().ToString("HH:mm:ss") :
                    L10n.T("上次測試完成 · {0}；實際方向待你確認。", observation.TestUtc.ToLocalTime().ToString("HH:mm:ss"));
                Get<TextBlock>("TestEvidence").Text = evidence;
                Get<Button>("ConfirmHearing").Visibility = observation.TestedChannels == 3 && !observation.HearingConfirmed ? Visibility.Visible : Visibility.Collapsed;
                Get<Button>("ConfirmHearing").IsEnabled = !busy && p.CanTest;
                Get<TextBlock>("Message").Text = Session.Busy ? "" : Session.Message ?? "";
                Get<TextBlock>("Message").Visibility = !Session.Busy && !String.IsNullOrEmpty(Session.Message) ? Visibility.Visible : Visibility.Collapsed;
                Get<TextBlock>("Message").SetResourceReference(TextBlock.ForegroundProperty, Session.MessageError ? "Warning" : "Muted");
                string operationError = Session.LastOperation != null && Session.LastOperation.Failed ? Session.LastOperation.ErrorText : Session.ErrorDetail;
                string detail = String.Join("\n\n", new[] { Session.ReadError, operationError, Session.EnumerationError,
                    preferenceWriter.Error == null ? null : L10n.T("裝置偏好未能儲存：") + preferenceWriter.Error,
                    Session.State.Error == 0 ? null : L10n.T("核心錯誤：0x") + Session.State.Error.ToString("X8"), Session.ScopeError == null ? null : L10n.T("全域設定：") + Session.ScopeError }.Where(s => !String.IsNullOrEmpty(s)));
                Get<TextBox>("ErrorText").Text = detail;
                Get<Expander>("ErrorExpander").Visibility = detail.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
                Get<Button>("Advanced").IsEnabled = !busy;
            }
            finally { updating = false; }
        }
        private async System.Threading.Tasks.Task Setup()
        {
            if (Session.Busy || !Session.Presentation.CanSetup || Session.Selected == null) return;
            var target = Session.Selected;
            bool needs;
            try { needs = Session.Backend.NeedsHostChange(); }
            catch (Exception ex) { Session.ErrorDetail = ex.Message; Update(); return; }
            string text = L10n.T("設定裝置：") + target.Name + L10n.T("\n\n本程式會備份此裝置的音效設定、接入內建音訊核心，並開啟左右互換。\n\n電腦音訊服務會重新啟動，所有裝置的聲音會短暫中斷；完成時會播放兩聲短音檢查核心。\n\n");
            text += needs ? L10n.T("此預覽版核心尚未取得 Microsoft 音訊簽章。首次設定需調整受保護音訊宿主設定（DisableProtectedAudioDG=1），作用於整台電腦，部分 DRM 音訊可能受影響。\n\n") :
                L10n.T("此電腦目前已允許載入此音訊核心；本次會沿用現有的音訊宿主設定。\n\n");
            text += L10n.T("關閉互換或視窗會保留系統設定。日後可在進階設定使用「移除所有裝置的設定」還原本程式的變更。");
            if (Dialog(L10n.T("設定此裝置"), text, L10n.T("設定並開啟互換")) != "accept") return;
            await Session.SetupAsync(target.Id, needs);
        }
        private async System.Threading.Tasks.Task Advanced()
        {
            if (Session.Busy) return;
            Session.Refresh();
            if (Session.ScopeError != null) { Dialog(L10n.T("無法讀取進階設定"), Session.ScopeError, null); return; }
            var scope = Session.Scope;
            var active = Session.Devices.Where(d => !d.Offline).Select(d => d.Guid).ToArray();
            string names = String.Join("\n", scope.Devices.Select(d => "• " + d.Name + (active.Contains(d.Id, StringComparer.OrdinalIgnoreCase) ? "" : L10n.T("（目前離線）"))));
            string text = L10n.T("本程式已設定 {0} 個裝置。\n", scope.Devices.Length) + names +
                L10n.T("\n\n關閉所有互換：只恢復這些裝置的左右方向，保留核心與系統設定。\n\n移除所有裝置的設定：還原本程式管理的裝置音效、核心登記及音訊宿主設定，包含離線裝置。外部程式已修改的設定會保留。\n\n重新啟動或移除時，整台電腦的音訊會短暫中斷。");
            string choice = Dialog(L10n.T("進階設定"), text, null, scope);
            if (choice == null) return;
            string confirmation = choice == "remove" ? L10n.T("將移除上述所有裝置的設定，並還原本程式管理的系統變更。") :
                choice == "off" ? L10n.T("將關閉上述所有裝置的互換，保留核心與系統設定。") : L10n.T("將重新啟動整台電腦的音訊服務。");
            if (choice != "off") confirmation += L10n.T("\n\n所有音訊輸出的聲音會短暫中斷。");
            if (Dialog(choice == "remove" ? L10n.T("移除所有裝置的設定") : choice == "off" ? L10n.T("關閉所有裝置的互換") : L10n.T("重新啟動電腦音訊服務"),
                confirmation + L10n.T("\n\n影響的已設定裝置：\n") + (names.Length == 0 ? L10n.T("沒有裝置；仍可能有全域設定。") : names),
                choice == "remove" ? L10n.T("還原並移除設定") : L10n.T("確認執行")) != "accept") return;
            await Session.GlobalAsync(choice, scope.Signature);
        }
        public string Dialog(string title, string text, string accept, SetupScope scope = null)
        {
            dialogOpen = true; Update(); string result = null;
            var previousFocus = Keyboard.FocusedElement;
            var window = new Window { Owner = View.IsVisible ? View : null, Title = title, Width = Math.Min(560, SystemParameters.WorkArea.Width - 32),
                Height = Math.Min(580, SystemParameters.WorkArea.Height - 32), WindowStartupLocation = View.IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Resources = View.Resources, FontFamily = View.FontFamily, FontSize = 14, ShowInTaskbar = false, ResizeMode = ResizeMode.CanResize };
            window.SetResourceReference(Window.BackgroundProperty, "Page"); window.SetResourceReference(Window.ForegroundProperty, "Text");
            window.SetResourceReference(Window.FontSizeProperty, "Font14");
            var layout = new DockPanel { Margin = new Thickness(22) };
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
            var cancel = new Button { Content = accept == null && scope == null ? L10n.T("關閉視窗") : L10n.T("取消"), IsCancel = true, Margin = new Thickness(6, 0, 0, 0) };
            cancel.Click += delegate { window.Close(); }; buttons.Children.Add(cancel);
            if (accept != null)
            {
                var ok = new Button { Content = accept, Margin = new Thickness(8, 0, 0, 0) };
                ok.SetResourceReference(Button.BackgroundProperty, "Accent"); ok.SetResourceReference(Button.ForegroundProperty, "AccentText");
                ok.Click += delegate { result = "accept"; window.Close(); }; buttons.Children.Add(ok);
            }
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            if (scope != null)
            {
                string[] actions = { "off", "restart", "remove" };
                string[] labels = { L10n.T("關閉所有已設定裝置的互換"), L10n.T("重新啟動電腦音訊服務"), L10n.T("移除所有裝置的設定") };
                for (int i = 0; i < actions.Length; i++)
                {
                    string action = actions[i];
                    var button = new Button { Content = labels[i], Margin = new Thickness(0, 12, 0, 0), IsEnabled = action == "restart" || (action == "off" ? scope.Devices.Length > 0 : scope.HasChanges) };
                    button.Click += delegate { result = action; window.Close(); }; body.Children.Add(button);
                }
            }
            layout.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            window.Content = layout;
            window.Loaded += delegate { cancel.Focus(); };
            try { window.ShowDialog(); }
            finally { dialogOpen = false; Update(); if (previousFocus != null) Keyboard.Focus(previousFocus); }
            return result;
        }
        private void ShowHelp()
        {
            Dialog(L10n.T("使用說明 · Channel Flip ") + Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                L10n.T("1. 選擇耳機或喇叭。首次按「設定此裝置」；完成後使用左右互換開關。\n\n2. 分別測試來源左、右聲道，再依聽到的方向確認。設定開啟不代表每個播放程式都已通過核心；運作狀態以近期處理證據顯示。\n\n3. 固定裝置會保留離線目標。跟隨系統預設只更新視窗的操作目標，各裝置設定獨立保存，新裝置仍須先設定。\n\n關閉視窗後，已接入裝置的互換設定會保留。進階設定可以關閉所有互換、重新啟動電腦音訊服務，或移除所有裝置的設定。\n\n作用範圍是所選裝置的 Windows 共用模式音訊。獨佔模式、ASIO、RAW 及停用音效強化可能繞過核心；藍牙免持模式可能是另一個端點。\n\n本程式採 MIT 授權，內建自己的音訊核心，無須 Equalizer APO。未簽章預覽版的音訊宿主設定影響整台電腦，部分 DRM 音訊可能受影響。"), null);
        }
        public void RenderPreview(string path) { RenderPreview(path, 660, 720, 96, false); }
        public void RenderPreview(string path, int width, int height, int dpi, bool contrast, double textScale = 1)
        {
            if (!simulation) throw new InvalidOperationException(L10n.T("離線預覽只能使用模擬資料。"));
            UiTheme.Apply(View, contrast ? (bool?)true : null); UiTypography.Apply(View.Resources, textScale); Update();
            var content = (FrameworkElement)View.Content; View.Content = null;
            var frame = new Border { Background = View.Background, Child = content, Width = width, Height = height, Resources = View.Resources, Language = View.Language };
            System.Windows.Documents.TextElement.SetForeground(frame, View.Foreground);
            System.Windows.Documents.TextElement.SetFontFamily(frame, View.FontFamily);
            System.Windows.Documents.TextElement.SetFontSize(frame, View.FontSize);
            VisualTreeHelper.SetRootDpi(frame, new DpiScale(dpi / 96.0, dpi / 96.0));
            frame.Measure(new Size(width, height)); frame.Arrange(new Rect(0, 0, width, height)); frame.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * dpi / 96.0), (int)Math.Ceiling(height * dpi / 96.0), dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(frame); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(path)) encoder.Save(output);
        }
    }
}
