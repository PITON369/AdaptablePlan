using AdaptablePlan.UI.Localization;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Локализация: свойство языка (Settings), реакция на смену языка.
// Статусные тексты запоминаются ключами, а не готовыми строками, чтобы
// пересчитываться на текущем языке.
public partial class MainWindowViewModel
{
    private string _statusKey = "StatusLoading";
    private string _backupStamp = string.Empty;

    private void SetStatus(string key)
    {
        _statusKey = key;
        AppStatus = Strings.Get(key);
    }

    private void OnStringsChanged()
    {
        // Пустое имя свойства — обновить все привязки этого объекта
        // (вычисляемые надписи: TaskActionText, ChecklistTitle и т.п.).
        OnPropertyChanged(string.Empty);

        // Имена дней живут в под-вьюмоделях записей Custom-страниц.
        var pages = CustomPages.ToList();
        if (YearPage != null)
            pages.Add(YearPage);
        if (MonthPage != null)
            pages.Add(MonthPage);
        foreach (var page in pages)
        {
            page.RefreshBindings();
            foreach (var entry in page.Entries)
                entry.RefreshBindings();
        }

        // План месяца/года пересобираем из базы: заголовки недель и дней
        // зашиты в под-вьюмодели.
        if (_db != null)
            LoadPlanPages(_db);
        else
            ApplyPlanPages(DefaultPlanPages());

        // Заголовки дней недельной сетки строятся в RegenerateSchedule.
        RegenerateSchedule();
    }

    private void RecomputeStatusTexts()
    {
        AppStatus = Strings.Get(_statusKey);
        if (IsServerDown)
            DbBannerText = Strings.Format("BannerBackup", _backupStamp);
        else if (IsDatabaseError)
            DbBannerText = Strings.Get("BannerNoDb");
    }
}
