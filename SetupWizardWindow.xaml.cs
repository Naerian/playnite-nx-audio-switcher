using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace PlayniteAudioSwitcher
{
    public partial class SetupWizardWindow : Window
    {
        private const int StepCount = 4;
        private readonly AudioSwitcherPlugin plugin;
        private readonly SetupWizardDraft draft;
        private int step;
        private bool suppressRecenter;
        private bool userMovedWindow;

        public SetupWizardWindow(AudioSwitcherPlugin sourcePlugin, SetupWizardDraft workingCopy)
        {
            if (sourcePlugin == null)
            {
                throw new ArgumentNullException(nameof(sourcePlugin));
            }

            if (workingCopy == null)
            {
                throw new ArgumentNullException(nameof(workingCopy));
            }

            plugin = sourcePlugin;
            draft = workingCopy;
            InitializeComponent();
            Title = Loc("LOCAS_SetupWizardTitle");
            LoadLabels();
            LoadDeviceOptions();
            LoadDraftIntoControls();
            ShowStep(0);
        }

        public SetupWizardDraft Draft => draft;

        private string Loc(string key)
        {
            return plugin.Loc(key);
        }

        private void LoadLabels()
        {
            SkipButton.Content = Loc("LOCAS_SetupWizardSkip");
            BackButton.Content = Loc("LOCAS_SetupWizardBack");
            WelcomeBody.Text = Loc("LOCAS_SetupWizardWelcomeBody");
            PreferredOutputLabel.Text = Loc("LOCAS_PreferredOutputDevice");
            PreferredOutputHelp.Text = Loc("LOCAS_PreferredDeviceHelp");
            PreferredInputLabel.Text = Loc("LOCAS_PreferredInputDevice");
            PreferredInputHelp.Text = Loc("LOCAS_PreferredDeviceHelp");
            DesktopTopBarCheck.Content = Loc("LOCAS_ShowDesktopAudioSwitcher");
            DesktopTopBarHelp.Text = Loc("LOCAS_SetupWizardDesktopHelp");
            QuickSwitchCheck.Content = Loc("LOCAS_EnableQuickSwitch");
            QuickSwitchHelp.Text = Loc("LOCAS_EnableQuickSwitchHelp");
            SummaryHelp.Text = Loc("LOCAS_SetupWizardSummaryHelp");
        }

        private void LoadDeviceOptions()
        {
            var settings = plugin.Settings;
            settings?.RefreshDevices();
            PreferredOutputBox.ItemsSource = settings?.PreferredPlaybackDeviceOptions;
            PreferredInputBox.ItemsSource = settings?.PreferredRecordingDeviceOptions;
        }

        private void LoadDraftIntoControls()
        {
            PreferredOutputBox.SelectedValue = draft.PreferredOutputDeviceId ?? string.Empty;
            PreferredInputBox.SelectedValue = draft.PreferredInputDeviceId ?? string.Empty;
            DesktopTopBarCheck.IsChecked = draft.ShowDesktopBatteryIndicator;
            QuickSwitchCheck.IsChecked = draft.QuickSwitchEnabled;
        }

        private void CommitControlsToDraft()
        {
            draft.PreferredOutputDeviceId = PreferredOutputBox.SelectedValue as string ?? string.Empty;
            draft.PreferredInputDeviceId = PreferredInputBox.SelectedValue as string ?? string.Empty;
            draft.ShowDesktopBatteryIndicator = DesktopTopBarCheck.IsChecked == true;
            draft.QuickSwitchEnabled = QuickSwitchCheck.IsChecked == true;
        }

        private void ShowStep(int index)
        {
            step = Math.Max(0, Math.Min(StepCount - 1, index));
            StepWelcome.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
            StepDevices.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            StepDesktop.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            StepSummary.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

            StepLabel.Text = string.Format(Loc("LOCAS_SetupWizardStep"), step + 1, StepCount);
            BackButton.Visibility = step == 0 ? Visibility.Collapsed : Visibility.Visible;
            NextButton.Content = step == StepCount - 1
                ? Loc("LOCAS_SetupWizardFinish")
                : Loc("LOCAS_SetupWizardNext");

            switch (step)
            {
                case 0:
                    StepTitle.Text = Loc("LOCAS_SetupWizardWelcomeTitle");
                    StepHelp.Text = Loc("LOCAS_SetupWizardWelcomeHelp");
                    break;
                case 1:
                    StepTitle.Text = Loc("LOCAS_SetupWizardDevicesTitle");
                    StepHelp.Text = Loc("LOCAS_SetupWizardDevicesHelp");
                    break;
                case 2:
                    StepTitle.Text = Loc("LOCAS_SetupWizardAccessTitle");
                    StepHelp.Text = Loc("LOCAS_SetupWizardAccessHelp");
                    break;
                default:
                    CommitControlsToDraft();
                    StepTitle.Text = Loc("LOCAS_SetupWizardSummaryTitle");
                    StepHelp.Text = string.Empty;
                    RebuildSummaryRows();
                    break;
            }

            Dispatcher.BeginInvoke(new Action(CenterInOwnerOrScreen), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs args)
        {
            CenterInOwnerOrScreen();
            Dispatcher.BeginInvoke(
                new Action(CenterInOwnerOrScreen),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void RebuildSummaryRows()
        {
            SummaryRows.Children.Clear();
            AddSummaryRow(Loc("LOCAS_PreferredOutputDevice"), ResolveDeviceName(draft.PreferredOutputDeviceId, false));
            AddSummaryRow(Loc("LOCAS_PreferredInputDevice"), ResolveDeviceName(draft.PreferredInputDeviceId, true));
            AddSummaryRow(
                Loc("LOCAS_SetupWizardSummaryLabelDesktop"),
                draft.ShowDesktopBatteryIndicator ? Loc("LOCAS_SetupWizardSummaryOn") : Loc("LOCAS_SetupWizardSummaryOff"));
            AddSummaryRow(
                Loc("LOCAS_SetupWizardSummaryLabelQuickSwitch"),
                draft.QuickSwitchEnabled ? Loc("LOCAS_SetupWizardSummaryOn") : Loc("LOCAS_SetupWizardSummaryOff"));
        }

        private string ResolveDeviceName(string deviceId, bool input)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return Loc("LOCAS_KeepWindowsDefault");
            }

            var options = input
                ? plugin.Settings?.PreferredRecordingDeviceOptions
                : plugin.Settings?.PreferredPlaybackDeviceOptions;
            var match = options?.Find(device =>
                string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            return match?.SettingsDisplayName ?? Loc("LOCAS_UnknownDevice");
        }

        private void AddSummaryRow(string label, string value)
        {
            var border = new Border { Style = (Style)FindResource("WizardSummaryRow") };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Style = (Style)FindResource("WizardSummaryLabel")
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                Style = (Style)FindResource("WizardSummaryValue")
            });
            border.Child = stack;
            SummaryRows.Children.Add(border);
        }

        private void NextClick(object sender, RoutedEventArgs args)
        {
            if (step < StepCount - 1)
            {
                if (step == 1 || step == 2)
                {
                    CommitControlsToDraft();
                }

                ShowStep(step + 1);
                return;
            }

            CommitControlsToDraft();
            draft.SetupWizardCompleted = true;
            DialogResult = true;
        }

        private void BackClick(object sender, RoutedEventArgs args)
        {
            if (step > 0)
            {
                ShowStep(step - 1);
            }
        }

        private void SkipClick(object sender, RoutedEventArgs args)
        {
            draft.SetupWizardCompleted = true;
            DialogResult = false;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                SkipClick(sender, args);
            }
        }

        private void OnDragAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
        {
            if (args.ChangedButton == MouseButton.Left)
            {
                try
                {
                    userMovedWindow = true;
                    DragMove();
                }
                catch
                {
                }
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs args)
        {
            if (suppressRecenter || userMovedWindow || !IsLoaded)
            {
                return;
            }

            if (!args.HeightChanged && !args.WidthChanged)
            {
                return;
            }

            CenterInOwnerOrScreen();
        }

        private void CenterInOwnerOrScreen()
        {
            if (userMovedWindow)
            {
                return;
            }

            suppressRecenter = true;
            try
            {
                UpdateLayout();
                var width = ActualWidth;
                var height = ActualHeight;
                if (width < 100 || height < 100 || double.IsNaN(width) || double.IsNaN(height))
                {
                    return;
                }

                var anchor = GetCenteringAnchor();
                Point? centerDip = null;
                if (anchor == null || anchor.WindowState != WindowState.Maximized)
                {
                    centerDip = TryGetWindowCenterDip(anchor);
                }

                double left;
                double top;

                if (centerDip.HasValue)
                {
                    left = centerDip.Value.X - (width / 2.0);
                    top = centerDip.Value.Y - (height / 2.0);
                }
                else
                {
                    var workArea = GetWorkAreaDip(anchor);
                    left = workArea.Left + ((workArea.Width - width) / 2.0);
                    top = workArea.Top + ((workArea.Height - height) / 2.0);
                }

                var clampArea = GetWorkAreaDip(anchor);
                if (width <= clampArea.Width)
                {
                    left = Math.Min(Math.Max(left, clampArea.Left), clampArea.Right - width);
                }
                else
                {
                    left = clampArea.Left;
                }

                if (height <= clampArea.Height)
                {
                    top = Math.Min(Math.Max(top, clampArea.Top), clampArea.Bottom - height);
                }
                else
                {
                    top = clampArea.Top;
                }

                if (!double.IsNaN(left) && !double.IsNaN(top) &&
                    !double.IsInfinity(left) && !double.IsInfinity(top))
                {
                    Left = left;
                    Top = top;
                }
            }
            finally
            {
                suppressRecenter = false;
            }
        }

        private Window GetCenteringAnchor()
        {
            try
            {
                var main = Application.Current != null ? Application.Current.MainWindow : null;
                if (main != null &&
                    main.IsVisible &&
                    main.WindowState != WindowState.Minimized &&
                    main.ActualWidth > 0 &&
                    main.ActualHeight > 0)
                {
                    return main;
                }
            }
            catch
            {
            }

            return Owner;
        }

        private Point? TryGetWindowCenterDip(Window window)
        {
            if (window == null ||
                !window.IsVisible ||
                window.WindowState == WindowState.Minimized ||
                window.ActualWidth <= 0 ||
                window.ActualHeight <= 0)
            {
                return null;
            }

            try
            {
                var centerPx = window.PointToScreen(new Point(
                    window.ActualWidth / 2.0,
                    window.ActualHeight / 2.0));
                var fromDevice = GetTransformFromDevice(this) ?? GetTransformFromDevice(window);
                if (fromDevice == null)
                {
                    return new Point(centerPx.X, centerPx.Y);
                }

                return fromDevice.Value.Transform(new Point(centerPx.X, centerPx.Y));
            }
            catch
            {
                return null;
            }
        }

        private Rect GetWorkAreaDip(Window anchor)
        {
            try
            {
                var screen = GetScreenForWindow(anchor) ?? GetScreenForWindow(this) ?? Forms.Screen.PrimaryScreen;
                if (screen == null)
                {
                    return SystemParameters.WorkArea;
                }

                var pixel = screen.WorkingArea;
                var fromDevice = GetTransformFromDevice(anchor) ?? GetTransformFromDevice(this);
                if (fromDevice == null)
                {
                    return new Rect(pixel.Left, pixel.Top, pixel.Width, pixel.Height);
                }

                var topLeft = fromDevice.Value.Transform(new Point(pixel.Left, pixel.Top));
                var bottomRight = fromDevice.Value.Transform(new Point(pixel.Right, pixel.Bottom));
                return new Rect(topLeft, bottomRight);
            }
            catch
            {
                return SystemParameters.WorkArea;
            }
        }

        private static Forms.Screen GetScreenForWindow(Window window)
        {
            if (window == null)
            {
                return null;
            }

            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    return Forms.Screen.FromHandle(handle);
                }

                if (!double.IsNaN(window.Left) && !double.IsNaN(window.Top))
                {
                    var px = GetTransformToDevice(window);
                    if (px != null)
                    {
                        var point = px.Value.Transform(new Point(window.Left + 8, window.Top + 8));
                        return Forms.Screen.FromPoint(new System.Drawing.Point(
                            (int)Math.Round(point.X),
                            (int)Math.Round(point.Y)));
                    }

                    return Forms.Screen.FromPoint(new System.Drawing.Point(
                        (int)Math.Round(window.Left + 8),
                        (int)Math.Round(window.Top + 8)));
                }
            }
            catch
            {
            }

            return null;
        }

        private static Matrix? GetTransformFromDevice(Window window)
        {
            var source = GetPresentationSource(window);
            if (source == null || source.CompositionTarget == null)
            {
                return null;
            }

            return source.CompositionTarget.TransformFromDevice;
        }

        private static Matrix? GetTransformToDevice(Window window)
        {
            var source = GetPresentationSource(window);
            if (source == null || source.CompositionTarget == null)
            {
                return null;
            }

            return source.CompositionTarget.TransformToDevice;
        }

        private static PresentationSource GetPresentationSource(Window window)
        {
            if (window == null)
            {
                return null;
            }

            var source = PresentationSource.FromVisual(window);
            if (source != null)
            {
                return source;
            }

            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    return HwndSource.FromHwnd(handle);
                }
            }
            catch
            {
            }

            return null;
        }
    }

    public sealed class SetupWizardDraft
    {
        public string PreferredOutputDeviceId { get; set; } = string.Empty;
        public string PreferredInputDeviceId { get; set; } = string.Empty;
        public bool ShowDesktopBatteryIndicator { get; set; }
        public bool QuickSwitchEnabled { get; set; }
        public bool SetupWizardCompleted { get; set; }
    }
}
