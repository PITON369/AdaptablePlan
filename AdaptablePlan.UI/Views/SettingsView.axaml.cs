using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AdaptablePlan.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnBrowseSound(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm
            || TopLevel.GetTopLevel(this) is not { } top)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose sound file",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Audio files") { Patterns = ["*.wav", "*.mp3"] }],
        });
        if (files.Count > 0)
            vm.SoundFile = files[0].Path.LocalPath;
    }
}
