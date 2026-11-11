using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Редактор задач: валидация, конфликты, CRUD шаблонов.
public partial class MainWindowViewModel
{
    [RelayCommand]
    private void AddTimeVariant() => TimeVariants.Add(new TaskTimeVariantViewModel());

    [RelayCommand]
    private void RemoveTimeVariant(TaskTimeVariantViewModel variant) => TimeVariants.Remove(variant);

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
            ValidationMessage = conflict;
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
