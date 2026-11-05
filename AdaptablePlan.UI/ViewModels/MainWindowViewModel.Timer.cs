using AdaptablePlan.UI.Localization;
using AdaptablePlan.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace AdaptablePlan.UI.ViewModels;

// Таймер текущей таски: отсчёт до отображаемого (со сдвигом) конца таски,
// по истечении - оверлей "Time is up". Оверлей, а не отдельное окно, чтобы
// одинаково работать и на телефоне. DispatcherTimer тикает на UI-потоке.
public partial class MainWindowViewModel
{
    private DispatcherTimer? _taskTimer;
    private DateTime _timerTarget;

    [ObservableProperty]
    private string _timeLeftText = string.Empty;

    [ObservableProperty]
    private bool _isTimeUpOpen;

    [ObservableProperty]
    private string _timeUpTaskName = string.Empty;

    // Отображаемый конец = база конца + сдвиг по базовому старту - та же
    // арифметика, что в FinishRunningTaskAsync (парсить текст нельзя:
    // при уходе за полночь он "завёрнут"). Вызывать после RegenerateSchedule,
    // чтобы сдвиг уже включал только что добавленное событие старта.
    private void StartTaskTimer(ScheduleItem item)
    {
        StopTaskTimer();
        var (baseStart, baseEnd) = BaseInterval(item);
        if (baseStart == null || baseEnd == null || _dayOffset == null)
            return;

        var startMin = (int)baseStart.Value.TotalMinutes;
        var endMin = (int)baseEnd.Value.TotalMinutes + _dayOffset.CumulativeShift(startMin);
        var startDate = item.Date.Date.AddMinutes(startMin);
        var endDate = item.Date.Date.AddMinutes(endMin);
        if (endDate <= startDate)
            endDate = endDate.AddDays(1); // таска перешла за полночь
        _timerTarget = endDate;

        _taskTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => OnTimerTick());
        UpdateTimeLeft();
        _taskTimer.Start();
    }

    private void StopTaskTimer()
    {
        _taskTimer?.Stop();
        _taskTimer = null;
        TimeLeftText = string.Empty;
    }

    private void UpdateTimeLeft()
    {
        var left = _timerTarget - DateTime.Now;
        TimeLeftText = left > TimeSpan.Zero
            ? Strings.Get("TimeLeft") + (left >= TimeSpan.FromHours(1)
                ? left.ToString(@"h\:mm\:ss")
                : left.ToString(@"mm\:ss"))
            : Strings.Get("TimeLeft") + "00:00";
    }

    private void OnTimerTick()
    {
        if (_timerTarget - DateTime.Now > TimeSpan.Zero)
        {
            UpdateTimeLeft();
            return;
        }
        StopTaskTimer();
        SoundPlayer.Play(SoundFile);
        TimeUpTaskName = RunningItem()?.Template.Name ?? string.Empty;
        IsTimeUpOpen = true;
    }

    // OK в окне "Time is up": заканчиваем таску; при AutoStartNextTask
    // FinishRunningTaskAsync сам стартует следующую и её таймер.
    [RelayCommand]
    private async Task OkTimeUp()
    {
        IsTimeUpOpen = false;
        if (IsTaskRunning)
        {
            await FinishRunningTaskAsync();
            OnPropertyChanged(nameof(TaskActionText));
            OnPropertyChanged(nameof(CanToggleTask));
        }
    }
}
