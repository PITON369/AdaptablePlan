using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
using AdaptablePlan.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
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

    // Конец таймера = отображаемый конец таски: тот же сдвиг от событий дня
    // и та же обрезка/обтекание против FixedTime, что рисует RegenerateSchedule
    // (парсить текст нельзя: при уходе за полночь он "завёрнут"). Вызывать
    // после RegenerateSchedule, чтобы сдвиг уже включал событие старта.
    private void StartTaskTimer(ScheduleItem item)
    {
        StopTaskTimer();
        var (baseStart, baseEnd) = BaseInterval(item);
        if (baseStart == null || baseEnd == null)
            return;

        var startMin = (int)baseStart.Value.TotalMinutes;
        var endMin = (int)baseEnd.Value.TotalMinutes;
        var pieces = new List<(TimeSpan Start, TimeSpan End)> { (baseStart.Value, baseEnd.Value) };
        if (_dayOffset != null && item.Template.Type != TaskType.FixedTime
            && _dayOffset.Date == item.Date.ToString("yyyy-MM-dd"))
        {
            var shiftedStart = TimeSpan.FromMinutes(startMin + _dayOffset.CumulativeShift(startMin));
            var shiftedEnd = TimeSpan.FromMinutes(endMin + _dayOffset.CumulativeShift(endMin));
            pieces = ScheduleExpander.ResolveFixedOverlap(shiftedStart, shiftedEnd,
                FixedIntervalsForPlanDay(item.PlanDay), WrapAroundFixedTasks);
        }
        var firstStart = pieces[0].Start;
        var lastEnd = pieces.Max(p => p.End);
        var startDate = item.Date.Date.AddMinutes((int)firstStart.TotalMinutes);
        var endDate = item.Date.Date.AddMinutes((int)lastEnd.TotalMinutes);
        // Таска перешла за полночь. Сплюснутая в ноль (строгий режим при
        // наезде на FixedTime) звенит сразу - фиксированная уже началась.
        if (endDate < startDate)
            endDate = endDate.AddDays(1);
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
            CheckFixedCollision();
            UpdateTimeLeft();
            return;
        }
        StopTaskTimer();
        SoundPlayer.Play(SoundFile);
        TimeUpTaskName = RunningItem()?.Template.Name ?? string.Empty;
        IsTimeUpOpen = true;
    }

    // Наезд фиксированной таски на идущую: момент её старта наступил, а
    // таймер текущей таски ещё тикает дальше. Раз на фиксированную за день.
    [ObservableProperty]
    private bool _isFixedCollisionOpen;

    [ObservableProperty]
    private string _fixedCollisionText = string.Empty;

    [RelayCommand]
    private void OkFixedCollision() => IsFixedCollisionOpen = false;

    private DateTime _fixedCheckDate;
    private HashSet<int> _notifiedFixedCollisions = new();

    private void CheckFixedCollision()
    {
        var item = RunningItem();
        if (item == null || item.CustomSource != null || IsTimeUpOpen)
            return;
        if (_fixedCheckDate != item.PlanDay)
        {
            _fixedCheckDate = item.PlanDay;
            _notifiedFixedCollisions.Clear();
        }
        var now = DateTime.Now;
        foreach (var (fs, _) in FixedIntervalsForPlanDay(item.PlanDay))
        {
            // Свой старт идущей фиксированной таски - не наезд.
            if (item.Template.Type == TaskType.FixedTime && ParseTime(item.BaseStartTime) == fs)
                continue;
            var fsMin = (int)fs.TotalMinutes;
            var started = item.PlanDay.Date.AddMinutes(fsMin);
            if (now < started || _timerTarget <= started || !_notifiedFixedCollisions.Add(fsMin))
                continue;
            var fixedName = Schedule.FirstOrDefault(s => s.CustomSource == null
                    && s.Template.Type == TaskType.FixedTime
                    && s.StartTime == ScheduleExpander.FormatTime(fs))?.Template.Name
                ?? Strings.Get("FixedTask");
            FixedCollisionText = Strings.Format("FixedCollision", fixedName, item.Template.Name);
            IsFixedCollisionOpen = true;
        }
    }

    // OK в окне "Time is up": заканчиваем таску; при AutoStartNextTask
    // FinishRunningTaskAsync сам стартует следующую и её таймер.
    [RelayCommand]
    private async Task OkTimeUp()
    {
        IsTimeUpOpen = false;
        SoundPlayer.Stop();
        if (IsTaskRunning)
        {
            await FinishRunningTaskAsync();
            OnPropertyChanged(nameof(TaskActionText));
            OnPropertyChanged(nameof(CanToggleTask));
        }
    }
}
