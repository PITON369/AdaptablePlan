using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
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
            ? int.Parse(DurationText.Trim())
            : 0;
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
        if (SelectedTask == null)
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
        if (SelectedTask == null || _db == null)
            return;

        if (SelectedTask.Type == TaskType.OneTime)
            OneTimeTasks.Remove(SelectedTask);
        else
            TaskTemplates.Remove(SelectedTask);

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
            return "Enter a task name.";

        if (task.Type is TaskType.Recurring or TaskType.Things)
        {
            if (!int.TryParse(DurationText.Trim(), out var minutes) || minutes <= 0)
                return "Duration must be a positive whole number of minutes (e.g. 30).";
        }

        if (task.Type != TaskType.OneTime)
        {
            if (TimeVariants.Count == 0)
                return "Add at least one time (days + start).";

            foreach (var v in TimeVariants)
            {
                if (v.StartValue is null)
                    return "Select a start time for every time slot.";
                if (task.Type == TaskType.FixedTime)
                {
                    if (v.EndValue is null)
                        return "Select an end time for every time slot.";
                    if (v.EndValue <= v.StartValue)
                        return "End time must be later than start time.";
                }
                if (!v.HasAnyDay)
                    return "Select at least one day for every time slot.";
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
                    return $"Time overlaps with '{t.Name}' on {ShortDayName(day)} {ScheduleExpander.FormatTime(ts.Value)}";
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
