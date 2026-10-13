using DataCollector.WebUI.Entities;
using DataCollector.WebUI.Helpers;

namespace DataCollector.WebUI.Views.Pages;

/// <summary>Сторінка записів: створення, редагування й видалення записів.</summary>
public partial class Records
{
  private bool _isFormOpen;
  private EntityInstance? _editing;
  private Dictionary<string, string?> _form = [];
  private string? _formError;

  private EntityInstance? _toDelete;
  private string? _deleteError;

  private void OpenCreate()
  {
    _editing = null;

    // Новий запис одразу показує значення за замовчуванням - користувач бачить, що буде збережено.
    _form = _config!.Properties.ToDictionary(
      p => p.Key,
      p => FieldValue.ToForm(p.DataType, FieldValue.Raw(p.Rules.DefaultValue)));
    _formError = null;
    _isFormOpen = true;
  }

  /// <summary>
  /// Форма редагування завжди бере повний запис з сервера: у таблиці можуть бути лише вибрані поля,
  /// і збереження форми з неповними даними стерло б значення схованих полів.
  /// </summary>
  private async Task OpenEdit(EntityInstance row)
  {
    if (_config is null)
      return;

    var config = _config;
    EntityInstance? record = null;

    if (!await Try(async () => record = await RecordApi.Get(config.Id, row.Id)) || record is null || _config != config)
      return;

    _editing = record;
    _form = config.Properties.ToDictionary(p => p.Key, p => record.GetFormValue(p));
    _formError = null;
    _isFormOpen = true;
  }

  private async Task Save()
  {
    if (_config is null)
      return;

    // Значення надсилаємо рядками: сервер сам перевіряє їх за типом і правилами поля
    // (числа з комою або крапкою, межі, варіанти переліку), тож ця логіка не дублюється в інтерфейсі.
    // Інтерфейс лише переводить дату й час з місцевого часу в UTC.
    var values = _config.Properties.ToDictionary(
      p => p.Key,
      p => FieldValue.ToApi(p.DataType, _form[p.Key]));

    var ok = await Try(async () =>
    {
      if (_editing is null)
        await RecordApi.Create(_config.Id, values);
      else
        await RecordApi.Update(_config.Id, _editing.Id, values);
    }, e => _formError = e);

    if (!ok)
      return;

    var isNew = _editing is null;
    _isFormOpen = false;

    await RefreshData(toFirstPage: isNew);
  }

  private async Task Delete()
  {
    if (_config is null || _toDelete is null)
      return;

    var ok = await Try(() => RecordApi.Delete(_config.Id, _toDelete.Id), e => _deleteError = e);
    if (!ok)
      return;

    _toDelete = null;
    await RefreshData(toFirstPage: false);
  }

  /// <summary>Дані змінились: перечитати таблицю або перерахувати зведення - залежно від того, що відкрито.</summary>
  private Task RefreshData(bool toFirstPage)
  {
    if (IsAggregateView || _table is null)
    {
      _filtersVersion++;
      return Task.CompletedTask;
    }

    return _table.Reload(toFirstPage);
  }
}
