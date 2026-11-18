using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AdaptablePlan.UI.Localization;
using AdaptablePlan.UI.Models;
using AdaptablePlan.UI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AdaptablePlan.UI.Views;

// Неделя - таблица дней и строк времени. Собирается в code-behind: задачи
// занимают одну ячейку на все свои строки (Grid.RowSpan), а строки таблицы и
// ширины колонок пересчитываются по данным.
public partial class WeekView : UserControl
{
    private MainWindowViewModel? _vm;
    private bool _rebuildQueued;
    private readonly List<Action> _unsubscribers = new();

    public WeekView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => ClearSubscriptions();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.WeekRows.CollectionChanged -= OnWeekRowsChanged;
        _vm = DataContext as MainWindowViewModel;
        if (_vm != null)
        {
            _vm.WeekRows.CollectionChanged += OnWeekRowsChanged;
            QueueRebuild();
        }
    }

    private void OnWeekRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => QueueRebuild();

    // Сборка откладывается до конца dispatcher-цикла: BuildWeekGrid пересчитывает
    // ширины колонок после добавления строк, нужны готовые значения.
    private void QueueRebuild()
    {
        if (_rebuildQueued)
            return;
        _rebuildQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildQueued = false;
            Rebuild();
        }, DispatcherPriority.Background);
    }

    private void ClearSubscriptions()
    {
        foreach (var u in _unsubscribers)
            u();
        _unsubscribers.Clear();
    }

    private void Rebuild()
    {
        ClearSubscriptions();
        var vm = _vm;
        if (vm == null)
            return;
        var table = Table;
        table.Children.Clear();
        table.RowDefinitions.Clear();
        table.ColumnDefinitions.Clear();

        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        // Строка 0 - заголовки дней, далее по строке на каждый слот WeekRows.
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var timeHeader = new Border
        {
            Classes = { "colhdr" },
            BorderThickness = new Thickness(1, 1, 1, 1),
            Child = new TextBlock
            {
                Text = Strings.Get("Time"),
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Right,
            },
        };
        Grid.SetRow(timeHeader, 0);
        Grid.SetColumn(timeHeader, 0);
        table.Children.Add(timeHeader);

        var headers = vm.WeekDayHeaders.ToList();
        for (int c = 0; c < headers.Count; c++)
        {
            var header = headers[c];
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(header.ColumnWidth) });
            var border = new Border
            {
                Classes = { "colhdr" },
                BorderThickness = new Thickness(0, 1, 1, 1),
                Child = new TextBlock
                {
                    Text = header.Name,
                    FontWeight = FontWeight.SemiBold,
                    TextAlignment = TextAlignment.Center,
                },
            };
            Grid.SetRow(border, 0);
            Grid.SetColumn(border, c + 1);
            table.Children.Add(border);
            var b = border;
            PropertyChangedEventHandler onHeaderChanged = (_, _) => b.Classes.Set("selected", header.IsSelected);
            header.PropertyChanged += onHeaderChanged;
            _unsubscribers.Add(() => header.PropertyChanged -= onHeaderChanged);
            border.Classes.Set("selected", header.IsSelected);
        }

        for (int r = 0; r < vm.WeekRows.Count; r++)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = vm.WeekRows[r];
            var timeBorder = new Border
            {
                Classes = { "timecol" },
                MinHeight = 22,
                BorderThickness = new Thickness(1, 1, 0, 1),
                Child = new TextBlock
                {
                    FontSize = 11,
                    TextAlignment = TextAlignment.Right,
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            timeBorder.Child!.Bind(TextBlock.TextProperty,
                new Binding(nameof(WeekRow.TimeLabel)) { Source = row });
            Grid.SetRow(timeBorder, r + 1);
            table.Children.Add(timeBorder);

            for (int c = 0; c < headers.Count; c++)
            {
                var cellBorder = new Border
                {
                    Classes = { "gridcell" },
                    MinHeight = 22,
                    BorderThickness = new Thickness(0, 1, 1, 1),
                };
                Grid.SetRow(cellBorder, r + 1);
                Grid.SetColumn(cellBorder, c + 1);
                table.Children.Add(cellBorder);
            }
        }

        for (int c = 0; c < headers.Count && c < vm.WeekDayPlacements.Length; c++)
        {
            foreach (var p in vm.WeekDayPlacements[c])
            {
                var btn = new Button
                {
                    Classes = { "task" },
                    FontSize = 11,
                    Padding = new Thickness(3, 1),
                    MinHeight = 18,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Tag = p.Task,
                    Content = new TextBlock
                    {
                        Text = p.Task.Activity,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                Grid.SetRow(btn, p.Row + 1);
                Grid.SetRowSpan(btn, p.Span);
                Grid.SetColumn(btn, c + 1);
                btn.Click += OnCellButtonClick;
                table.Children.Add(btn);
                var b = btn;
                PropertyChangedEventHandler onItemChanged = (_, _) =>
                {
                    b.Classes.Set("selected", p.Task.IsSelected);
                    b.Classes.Set("running", p.Task.IsRunning);
                };
                p.Task.PropertyChanged += onItemChanged;
                _unsubscribers.Add(() => p.Task.PropertyChanged -= onItemChanged);
                btn.Classes.Set("selected", p.Task.IsSelected);
                btn.Classes.Set("running", p.Task.IsRunning);
            }
        }
    }

    private void OnCellButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScheduleItem item } && DataContext is MainWindowViewModel vm)
            vm.SelectedItem = item;
    }
}
