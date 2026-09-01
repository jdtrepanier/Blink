using System.Windows;
using System.Windows.Controls;

namespace Blink;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // NumericUpDown clamps to its own Minimum/Maximum, so values stay valid.
        IntervalBox.Value = settings.IntervalMinutes;
        BreakBox.Value = settings.BreakSeconds;
        AllowSkipBox.IsChecked = settings.AllowSkip;
        StartEnabledBox.IsChecked = settings.StartEnabled;

        // The registry Run key is the source of truth for auto-start.
        StartWithWindowsBox.IsChecked = StartupManager.IsEnabled();

        PopulateLanguages(settings.Language);
    }

    private void PopulateLanguages(string current)
    {
        // Tag holds the culture code; empty means "follow the OS".
        AddLanguage(Strings.Language_System, "");
        AddLanguage(Strings.Language_English, "en");
        AddLanguage(Strings.Language_French, "fr");
        AddLanguage(Strings.Language_Spanish, "es");

        foreach (ComboBoxItem item in LanguageBox.Items)
        {
            if ((string)item.Tag == current)
            {
                LanguageBox.SelectedItem = item;
                break;
            }
        }

        if (LanguageBox.SelectedItem is null)
            LanguageBox.SelectedIndex = 0;
    }

    private void AddLanguage(string label, string code)
        => LanguageBox.Items.Add(new ComboBoxItem { Content = label, Tag = code });

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _settings.IntervalMinutes = IntervalBox.Value;
        _settings.BreakSeconds = BreakBox.Value;
        _settings.AllowSkip = AllowSkipBox.IsChecked == true;
        _settings.StartEnabled = StartEnabledBox.IsChecked == true;
        _settings.Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

        StartupManager.SetEnabled(StartWithWindowsBox.IsChecked == true);

        DialogResult = true;
        Close();
    }
}
