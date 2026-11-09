using AdaptablePlan.UI.Localization;
using AdaptablePlan.UI.Models;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace AdaptablePlan.UI.ViewModels;

// Обычный независимый таймер (режим Day): выбрал минуты — запустил;
// по истечении тот же звук, что у конца таски, и оверлей "Timer finished".
// DispatcherTimer тикает на UI-потоке, работает и при смене режима.
public partial class MainWindowViewModel
{
    private DispatcherTimer? _simpleTimer;
    private DateTime _simpleTimerTarget;

    public static int[] SimpleTimerOptions { get; } = { 5, 10, 15, 20, 25, 30, 45, 60, 90, 120 };

    [ObservableProperty]
    private int _simpleTimerMinutes = 25;

    // Ручной ввод минут: переключатель "из списка / вручную" (RadioButton)
    [ObservableProperty]
    private bool _isSimpleTimerManual;

    [ObservableProperty]
    private decimal _simpleTimerManualMinutes = 25;

    // Видимость: комбобокс и поле ввода чередуются по переключателю,
    // и оба показываются только в режиме Day
    public bool IsDayModeAndListMode
        => !IsSimpleTimerManual && Mode == ScheduleMode.Day;

    public bool IsDayModeAndManualMode
        => IsSimpleTimerManual && Mode == ScheduleMode.Day;

    partial void OnIsSimpleTimerManualChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDayModeAndListMode));
        OnPropertyChanged(nameof(IsDayModeAndManualMode));
    }

    [ObservableProperty]
    private string _simpleTimeLeftText = string.Empty;

    [ObservableProperty]
    private bool _isSimpleTimerRunning;

    [ObservableProperty]
    private bool _isSimpleTimerDone;

    [RelayCommand]
    private void StartSimpleTimer()
    {
        StopSimpleTimer();
        var minutes = IsSimpleTimerManual ? (int)SimpleTimerManualMinutes : SimpleTimerMinutes;
        if (minutes <= 0)
            return;
        _simpleTimerTarget = DateTime.Now.AddMinutes(minutes);
        _simpleTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => OnSimpleTimerTick());
        IsSimpleTimerRunning = true;
        UpdateSimpleTimeLeft();
        _simpleTimer.Start();
    }

    [RelayCommand]
    private void CancelSimpleTimer() => StopSimpleTimer();

    [RelayCommand]
    private void OkSimpleTimerDone() => IsSimpleTimerDone = false;

    private void StopSimpleTimer()
    {
        _simpleTimer?.Stop();
        _simpleTimer = null;
        IsSimpleTimerRunning = false;
        SimpleTimeLeftText = string.Empty;
    }

    private void UpdateSimpleTimeLeft()
    {
        var left = _simpleTimerTarget - DateTime.Now;
        SimpleTimeLeftText = left > TimeSpan.Zero
            ? Strings.Get("TimeLeft") + (left >= TimeSpan.FromHours(1)
                ? left.ToString(@"h\:mm\:ss")
                : left.ToString(@"mm\:ss"))
            : Strings.Get("TimeLeft") + "00:00";
    }

    private void OnSimpleTimerTick()
    {
        if (_simpleTimerTarget - DateTime.Now > TimeSpan.Zero)
        {
            UpdateSimpleTimeLeft();
            return;
        }
        StopSimpleTimer();
        SoundPlayer.Play(SoundFile);
        IsSimpleTimerDone = true;
    }
}
