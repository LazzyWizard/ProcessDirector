using System;
using System.Windows;
using System.Windows.Input;
using ProcessDirector.AppData;
using ProcessDirector.Services;
using ProcessDirector.ViewModels;

namespace ProcessDirector
{
    public partial class SettingsWindow : Window
    {
        private SettingsViewModel _viewModel;
        private bool _isSaved = false;
        private System.Windows.Controls.TextBox _currentHotkeyTextBox = null;

        public SettingsWindow(SettingsModel settings)
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel(settings);
            DataContext = _viewModel;

            LoadHotkeys();
        }

        private void LoadHotkeys()
        {
            SuperF4TextBox.Text = GetHotkeyDisplay(_viewModel.SuperF4Modifiers, _viewModel.SuperF4Key);
            RefreshTextBox.Text = GetHotkeyDisplay(_viewModel.RefreshModifiers, _viewModel.RefreshKey);
            StartLoggingTextBox.Text = GetHotkeyDisplay(_viewModel.StartLoggingModifiers, _viewModel.StartLoggingKey);
            StopLoggingTextBox.Text = GetHotkeyDisplay(_viewModel.StopLoggingModifiers, _viewModel.StopLoggingKey);
            KillProcessTextBox.Text = GetHotkeyDisplay(_viewModel.KillProcessModifiers, _viewModel.KillProcessKey);
        }

        private string GetHotkeyDisplay(int modifiers, string key)
        {
            if (modifiers == 0 || string.IsNullOrEmpty(key) || key == "None")
                return "Не назначено";

            string result = "";
            if ((modifiers & 2) != 0) result += "Ctrl + ";
            if ((modifiers & 1) != 0) result += "Shift + ";
            if ((modifiers & 4) != 0) result += "Alt + ";
            if ((modifiers & 8) != 0) result += "Win + ";

            result += key;
            return result;
        }

        private void SelectLogFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Выберите папку для сохранения логов";
            dialog.SelectedPath = _viewModel.LogFolderPath;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _viewModel.LogFolderPath = dialog.SelectedPath;
            }
        }

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _currentHotkeyTextBox = sender as System.Windows.Controls.TextBox;
            if (_currentHotkeyTextBox != null)
            {
                _currentHotkeyTextBox.Text = "Нажмите сочетание клавиш...";
                _currentHotkeyTextBox.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                if (string.IsNullOrEmpty(textBox.Text) || textBox.Text == "Нажмите сочетание клавиш...")
                {
                    textBox.Text = "Не назначено";
                    textBox.Foreground = System.Windows.Media.Brushes.Gray;
                }
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox == null) return;

            Key key = e.Key;
            ModifierKeys modifiers = Keyboard.Modifiers;

            if (key == Key.Escape)
            {
                textBox.Text = "Не назначено";
                UpdateViewModelHotkey(textBox, 0, "None");
                return;
            }

            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            int mod = 0;
            if ((modifiers & ModifierKeys.Control) != 0) mod |= 2;
            if ((modifiers & ModifierKeys.Shift) != 0) mod |= 1;
            if ((modifiers & ModifierKeys.Alt) != 0) mod |= 4;
            if ((modifiers & ModifierKeys.Windows) != 0) mod |= 8;

            if (mod == 0)
            {
                mod = GetDefaultModifier(key);
            }

            if (mod == 0)
            {
                textBox.Text = "Требуется модификатор (Ctrl/Alt/Shift/Win)";
                return;
            }

            string keyString = key.ToString();
            textBox.Text = GetHotkeyDisplay(mod, keyString);
            UpdateViewModelHotkey(textBox, mod, keyString);
        }

        private int GetDefaultModifier(Key key)
        {
            if (key >= Key.F1 && key <= Key.F12)
                return 2;
            if (key >= Key.A && key <= Key.Z)
                return 2;
            if (key == Key.Delete || key == Key.Insert || key == Key.Home || key == Key.End)
                return 2;
            return 0;
        }

        private void UpdateViewModelHotkey(System.Windows.Controls.TextBox textBox, int modifiers, string key)
        {
            if (textBox == SuperF4TextBox)
            {
                _viewModel.SuperF4Modifiers = modifiers;
                _viewModel.SuperF4Key = key;
            }
            else if (textBox == RefreshTextBox)
            {
                _viewModel.RefreshModifiers = modifiers;
                _viewModel.RefreshKey = key;
            }
            else if (textBox == StartLoggingTextBox)
            {
                _viewModel.StartLoggingModifiers = modifiers;
                _viewModel.StartLoggingKey = key;
            }
            else if (textBox == StopLoggingTextBox)
            {
                _viewModel.StopLoggingModifiers = modifiers;
                _viewModel.StopLoggingKey = key;
            }
            else if (textBox == KillProcessTextBox)
            {
                _viewModel.KillProcessModifiers = modifiers;
                _viewModel.KillProcessKey = key;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_currentHotkeyTextBox != null)
            {
                e.Handled = true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _isSaved = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public SettingsModel GetUpdatedSettings()
        {
            return _viewModel.GetSettings();
        }
    }
}