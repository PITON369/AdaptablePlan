using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Редактор задач: валидация, конфликты, CRUD шаблонов.
public partial class MainWindowViewModel
{
    // Длительность задаёт конец всем блокам времени: конец = начало + длительность.
    partial void OnDurationTextChanged(string value)
    {
        if (CurrentTask is null
            || CurrentTask.Type is not (TaskType.Recurring or TaskType.Things)
            || !int.TryParse(value.Trim(), out var minutes) || minutes <= 0)
            return;
        foreach (var v in TimeVariants)
        {
            if (v.StartValue is TimeSpan start)
                v.EndValue = start.Add(TimeSpan.FromMinutes(minutes));
        }
    }

    [RelayCommand]
    private void AddTimeVariant()
    {
        TimeVariants.Add(new TaskTimeVariantViewModel());
        UpdateTimeVariantSeparators();
    }

    [RelayCommand]
    private void RemoveTimeVariant(TaskTimeVariantViewModel variant)
    {
        TimeVariants.Remove(variant);
        UpdateTimeVariantSeparators();
    }

    // Полоса-разделитель между блоками времени: последний блок без полосы.
    private void UpdateTimeVariantSeparators()
    {
        var last = TimeVariants.Count - 1;
        for (var i = 0; i <= last; i++)
            TimeVariants[i].IsLast = i == last;
    }

    [RelayCommand]
    private void OpenNewTask()
    {
        CurrentTask = new();
        CurrentTask.Type = TaskType.Recurring;
        ValidationMessage = string.Empty;
        DurationText = string.Empty;
        TimeVariants.Clear();
        TimeVariants.Add(new TaskTimeVariantViewModel());
        IsEditing = false;
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    private void SaveNewTask()
    {
        var error = ValidateTask(CurrentTask);
        if (error != null)
        {
            ValidationMessage = error;
            return;
        }

        CurrentTask.Name = CurrentTask.Name.Trim();
        CurrentTask.DurationMinutes = CurrentTask.Type is TaskType.Recurring or TaskType.Things
            && int.TryParse(DurationText.Trim(), out var minutes) ? minutes : 0;
        CurrentTask.DayTimes = CurrentTask.Type == TaskType.OneTime
            ? new List<TaskTimeVariant>()
            : TimeVariants.Select(v => v.ToModel()).ToList();

        var conflict = FindConflict(CurrentTask, IsEditing ? SelectedTask : null);
        if (conflict != null)
        {
            OfferShiftChain(conflict);
            return;
        }

        ValidationMessage = string.Empty;

        if (IsEditing && SelectedTask != null)
            UpdateTask(SelectedTask);
        else
            InsertTask();

        RegenerateSchedule();
        IsNewTaskOpen = false;
    }

    // Пересечение с соседними тасками: предлагаем одним действием сдвинуть всю
    // цепочку задетых тасок после новой. Таска сдвигается целиком (начало и
    // явный конец вместе, длительность сохраняется), в том числе таска, внутрь
    // которой влезла новая; FixedTime не сдвигается никогда - тогда остаётся
    // ошибка. Сдвиг хранится на варианте (набор дней), поэтому при разных
    // дельтах по дням берётся большая, и вариант двигается на все свои дни.
    [ObservableProperty]
    private bool _isShiftChainOpen;

    public ObservableCollection<string> ShiftChainLines { get; } = new();

    private string _shiftChainConflict = string.Empty;
    private List<(TaskTemplate Task, TaskTimeVariant Variant, int Delta)> _pendingShifts = new();

    private void OfferShiftChain(string conflict)
    {
        var shifts = PlanShiftChain();
        if (shifts == null || shifts.Count == 0)
        {
            ValidationMessage = conflict;
            return;
        }

        _pendingShifts = shifts;
        _shiftChainConflict = conflict;
        ValidationMessage = string.Empty;

        ShiftChainLines.Clear();
        foreach (var (task, variant, delta) in shifts)
            ShiftChainLines.Add(DescribeShift(task, variant, delta));
        IsShiftChainOpen = true;
    }

    private static readonly DayOfWeek[] DayOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    private static string DescribeShift(TaskTemplate task, TaskTimeVariant variant, int delta)
    {
        var (s, e) = ScheduleExpander.VariantInterval(task, variant);
        var days = string.Join("+", DayOrder.Where(variant.Days.Contains).Select(ShortDayName));
        string Range(TimeSpan? start, TimeSpan? end) => start == null || end == null
            ? "?"
            : $"{ScheduleExpander.FormatTime(start.Value)}-{ScheduleExpander.FormatTime(end.Value)}";
        var shift = TimeSpan.FromMinutes(delta);
        return $"{task.Name} ({days}) {Range(s, e)} -> {Range(s?.Add(shift), e?.Add(shift))}";
    }

    // Прогон дня: новая таска занимает свой интервал, каждая задетая - включая
    // таску, внутрь которой влезла новая, - и все последующие до свободного
    // места сдвигаются на конец предыдущей. Возвращает null, если цепочку
    // построить нельзя (неподвижная помеха).
    private List<(TaskTemplate Task, TaskTimeVariant Variant, int Delta)>? PlanShiftChain()
    {
        var exclude = IsEditing ? SelectedTask : null;
        var best = new Dictionary<(TaskTemplate, TaskTimeVariant), int>();

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var (cs, ce) = ScheduleExpander.IntervalForDay(CurrentTask, day);
            if (cs == null || ce == null)
                continue;

            var movable = new List<(TaskTemplate T, TaskTimeVariant V, TimeSpan S, TimeSpan E)>();
            var fixedItems = new List<(TimeSpan S, TimeSpan E)>();
            foreach (var t in TaskTemplates.Concat(OneTimeTasks))
            {
                if (ReferenceEquals(t, exclude) || t.Type == TaskType.OneTime)
                    continue;
                var v = ScheduleExpander.VariantForDay(t, day);
                if (v == null)
                    continue;
                var (s, e) = ScheduleExpander.VariantInterval(t, v);
                if (s == null || e == null)
                    continue;
                if (t.Type == TaskType.FixedTime)
                {
                    fixedItems.Add((s.Value, e.Value));
                    if (s.Value < ce.Value && cs.Value < e.Value)
                        return null;
                    continue;
                }
                movable.Add((t, v, s.Value, e.Value));
            }

            if (movable.All(m => m.S >= ce.Value || m.E <= cs.Value))
                continue;

            movable.Sort((a, b) => a.S.CompareTo(b.S));
            var floor = ce.Value;
            foreach (var m in movable)
            {
                if (m.S >= floor)
                    break;
                if (m.E <= cs.Value)
                    continue;
                var duration = m.E - m.S;
                var ns = floor;
                var jumped = true;
                while (jumped)
                {
                    jumped = false;
                    foreach (var f in fixedItems)
                    {
                        if (ns < f.E && f.S < ns + duration)
                        {
                            ns = f.E;
                            jumped = true;
                        }
                    }
                }
                var delta = (int)(ns - m.S).TotalMinutes;
                var key = (m.T, m.V);
                best[key] = Math.Max(best.GetValueOrDefault(key, 0), delta);
                floor = ns + duration;
            }
        }

        return best.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value)).ToList();
    }

    [RelayCommand]
    private void ConfirmShiftChain()
    {
        IsShiftChainOpen = false;
        foreach (var (task, variant, delta) in _pendingShifts)
        {
            if (!ScheduleExpander.TryParseTime(variant.StartTime, out var s))
                continue;
            variant.StartTime = ScheduleExpander.FormatTime(s + TimeSpan.FromMinutes(delta));
            if (ScheduleExpander.TryParseTime(variant.EndTime, out var e))
                variant.EndTime = ScheduleExpander.FormatTime(e + TimeSpan.FromMinutes(delta));
            if (_db != null)
                _db.TaskTemplates.UpdateAsync(task).GetAwaiter().GetResult();
        }
        _pendingShifts = new();
        SaveNewTask();
    }

    [RelayCommand]
    private void CancelShiftChain()
    {
        IsShiftChainOpen = false;
        _pendingShifts = new();
        ValidationMessage = _shiftChainConflict;
    }

    [RelayCommand]
    private void EditTask()
    {
        if (SelectedTask == null || SelectedItem?.CustomSource != null)
            return;

        CurrentTask = new()
        {
            Name = SelectedTask.Name,
            Type = SelectedTask.Type,
            DurationMinutes = SelectedTask.DurationMinutes,
            Date = SelectedTask.Date,
        };
        DurationText = SelectedTask.DurationMinutes.ToString();
        TimeVariants.Clear();
        foreach (var v in SelectedTask.DayTimes)
            TimeVariants.Add(TaskTimeVariantViewModel.FromModel(v));
        UpdateTimeVariantSeparators();

        ValidationMessage = string.Empty;
        IsEditing = true;
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    private void DeleteTask()
    {
        if (SelectedTask == null || _db == null || SelectedItem?.CustomSource != null)
            return;

        if (SelectedTask.Type == TaskType.OneTime)
            OneTimeTasks.Remove(SelectedTask);
        else
            TaskTemplates.Remove(SelectedTask);

        // Если удалили идущую таску - сбросить её Id, иначе ToggleTask
        // вечно уходит в Finish, который ничего не находит.
        if (SelectedTask.Id == _runningTaskId)
        {
            _runningTaskId = Guid.Empty;
            StopTaskTimer();
        }

        _db.TaskTemplates.DeleteAsync(SelectedTask.Id).GetAwaiter().GetResult();
        SelectedTask = null;
        SelectedItem = null;
        RegenerateSchedule();
    }

    private void UpdateTask(TaskTemplate task)
    {
        task.Name = CurrentTask.Name;
        task.Type = CurrentTask.Type;
        task.DurationMinutes = CurrentTask.DurationMinutes;
        task.Date = CurrentTask.Date;
        task.DayTimes = CurrentTask.DayTimes.ToList();

        if (_db != null)
            _db.TaskTemplates.UpdateAsync(task).GetAwaiter().GetResult();
    }

    // Кнопка в настройках: разовые таски с прошедшей датой, плюс таски без
    // даты (созданные до появления поля даты - в расписании их всё равно нет).
    // Удаление - через оверлей подтверждения со списком; если удалять нечего,
    // показываем сообщение, чтобы нажатие не выглядело "ничего не произошло".
    [RelayCommand]
    private void DeletePastOneTimeTasks()
    {
        var tasks = OneTimeTasks
            .Where(t => t.Date == null || t.Date.Value.Date < PlanToday)
            .ToList();
        if (tasks.Count == 0)
        {
            IsNoOneTimeOpen = true;
            return;
        }
        ShowOldOneTimeConfirm(tasks);
    }

    [ObservableProperty]
    private bool _isNoOneTimeOpen;

    [RelayCommand]
    private void OkNoOneTime() => IsNoOneTimeOpen = false;

    // Автоочистка при загрузке: прошёл год с даты разовой таски. Сразу не
    // удаляем - показываем оверлей со списком и ждём подтверждения.
    [ObservableProperty]
    private bool _isOldOneTimeConfirmOpen;

    [ObservableProperty]
    private string _oldOneTimeNames = string.Empty;

    private List<TaskTemplate> _pendingOldOneTime = new();

    private void CleanupOldOneTimeTasks()
        => ShowOldOneTimeConfirm(OneTimeTasks
            .Where(t => t.Date == null || t.Date.Value.Date < PlanToday.AddYears(-1))
            .ToList());

    private void ShowOldOneTimeConfirm(List<TaskTemplate> tasks)
    {
        if (tasks.Count == 0)
            return;
        _pendingOldOneTime = tasks;
        OldOneTimeNames = string.Join(", ", tasks.Select(t => t.Name));
        IsOldOneTimeConfirmOpen = true;
    }

    [RelayCommand]
    private void ConfirmDeleteOldOneTime()
    {
        IsOldOneTimeConfirmOpen = false;
        DeleteOneTimeList(_pendingOldOneTime);
        _pendingOldOneTime = new();
        RegenerateSchedule();
    }

    [RelayCommand]
    private void CancelDeleteOldOneTime()
    {
        IsOldOneTimeConfirmOpen = false;
        _pendingOldOneTime = new();
    }

    private void DeleteOneTimeList(List<TaskTemplate> tasks)
    {
        foreach (var t in tasks)
        {
            OneTimeTasks.Remove(t);
            if (_db == null)
                continue;
            try
            {
                _db.TaskTemplates.DeleteAsync(t.Id).GetAwaiter().GetResult();
            }
            catch
            {
                // сервер недоступен
            }
        }
    }

    private void InsertTask()
    {
        var task = new TaskTemplate
        {
            Id = Guid.NewGuid(),
            Name = CurrentTask.Name,
            Type = CurrentTask.Type,
            DurationMinutes = CurrentTask.DurationMinutes,
            Date = CurrentTask.Date,
            DayTimes = CurrentTask.DayTimes.ToList(),
        };

        if (task.Type == TaskType.OneTime)
            OneTimeTasks.Add(task);
        else
            TaskTemplates.Add(task);

        if (_db != null)
            _db.TaskTemplates.InsertAsync(task).GetAwaiter().GetResult();
    }

    // --- Validation ---
    private string? ValidateTask(TaskTemplate task)
    {
        if (string.IsNullOrWhiteSpace(task.Name))
            return Strings.Get("ValName");

        // Разовая таска без даты не разворачивается ни на один день расписания.
        if (task.Type == TaskType.OneTime && task.Date == null)
            return Strings.Get("ValDateMissing");

        // Длительность нужна, только если у какого-то интервала нет явного
        // конца: ScheduleExpander берёт EndTime, когда он задан.
        if (task.Type is TaskType.Recurring or TaskType.Things
            && TimeVariants.Any(v => v.EndValue is null))
        {
            if (!int.TryParse(DurationText.Trim(), out var minutes) || minutes <= 0)
                return Strings.Get("ValDuration");
        }

        if (task.Type != TaskType.OneTime)
        {
            if (TimeVariants.Count == 0)
                return Strings.Get("ValAddTime");

            foreach (var v in TimeVariants)
            {
                if (v.StartValue is null)
                    return Strings.Get("ValStartMissing");
                if (task.Type == TaskType.FixedTime)
                {
                    if (v.EndValue is null)
                        return Strings.Get("ValEndMissing");
                    if (v.EndValue <= v.StartValue)
                        return Strings.Get("ValEndAfterStart");
                }
                if (!v.HasAnyDay)
                    return Strings.Get("ValDayMissing");
            }
        }

        return null;
    }

    // Проверка конфликтов по 7 дням недели: точна для Weekly, для остальных
    // режимов повторения — приближение (не учитывает конкретные даты).
    private string? FindConflict(TaskTemplate candidate, TaskTemplate? exclude)
    {
        foreach (var t in TaskTemplates.Concat(OneTimeTasks))
        {
            if (ReferenceEquals(t, exclude))
                continue;
            // У задач OneTime времени нет — конфликт по времени не проверить.
            if (candidate.Type == TaskType.OneTime || t.Type == TaskType.OneTime)
                continue;

            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                var (cs, ce) = ScheduleExpander.IntervalForDay(candidate, day);
                var (ts, te) = ScheduleExpander.IntervalForDay(t, day);
                if (cs == null || ce == null || ts == null || te == null)
                    continue;

                if (cs.Value < te.Value && ts.Value < ce.Value)
                {
                    var baseText = Strings.Format("OverlapOn", t.Name, ShortDayName(day));
                    // Сегодняшний день идёт со сдвигом: в скобках время по плану,
                    // рядом - как таска стоит на экране сейчас.
                    if (day == DateTime.Today.DayOfWeek && _dayOffset != null)
                    {
                        var shift = TimeSpan.FromMinutes(_dayOffset.CumulativeShift((int)ts.Value.TotalMinutes));
                        return $"{baseText} ({ScheduleExpander.FormatTime(ts.Value)}-{ScheduleExpander.FormatTime(te.Value)}) " +
                            $"{ScheduleExpander.FormatTime(ts.Value + shift)}-{ScheduleExpander.FormatTime(te.Value + shift)}";
                    }
                    return $"{baseText} {ScheduleExpander.FormatTime(ts.Value)}-{ScheduleExpander.FormatTime(te.Value)}";
                }
            }
        }
        return null;
    }

    [RelayCommand]
    private void CancelNewTask()
    {
        IsNewTaskOpen = false;
    }
}
