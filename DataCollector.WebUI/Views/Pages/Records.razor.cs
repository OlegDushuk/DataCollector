using DataCollector.WebUI.Entities;
using DataCollector.WebUI.ServerApiServices;
using DataCollector.WebUI.Views.Base;
using DataCollector.WebUI.Views.Components.DataTable;
using DataCollector.WebUI.Views.Components.DataTable.Models;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Pages;

public partial class Records : PageBase, IDisposable
{
  [Parameter] public string? ConfigKey { get; set; }

  [Inject] private EntityConfigApiService ConfigApi { get; set; } = null!;
  [Inject] private EntityRecordApiService RecordApi { get; set; } = null!;

  private DataTable<EntityInstance>? _table;

  private List<EntityConfigListItem>? _configs;
  private EntityConfig? _config;

  private List<EntityInstance> _records = [];
  private int _totalCount;
  private bool _isLoading = true;

  // Запит поточної сторінки таблиці. Новий запит скасовує попередній, щоб застаріла відповідь
  // не перезаписала свіжу і сервер не рахував сторінку, яка вже не потрібна.
  private CancellationTokenSource? _reloadCts;

  private bool _isFormOpen;
  private EntityInstance? _editing;
  private Dictionary<string, string?> _form = [];
  private string? _formError;

  private EntityInstance? _toDelete;
  private string? _deleteError;

  protected override async Task OnParametersSetAsync()
  {
    Error = null;

    _configs ??= await LoadConfigs();
    if (_configs is null || _configs.Count == 0)
      return;

    // Без ключа в адресі відкриваємо першу модель.
    if (string.IsNullOrEmpty(ConfigKey))
    {
      Navigation.NavigateTo($"/records/{_configs[0].Key}", replace: true);
      return;
    }

    if (_config != null && string.Equals(_config.Key, ConfigKey, StringComparison.OrdinalIgnoreCase))
      return;

    CancelReload();
    _config = null;
    _records = [];
    _totalCount = 0;
    await Try(async () => _config = await ConfigApi.Get(ConfigKey));
  }

  private async Task<List<EntityConfigListItem>?> LoadConfigs()
  {
    List<EntityConfigListItem>? result = null;
    await Try(async () => result = await ConfigApi.GetAll());
    return result;
  }

  private void OnConfigChange(ChangeEventArgs e)
  {
    Navigation.NavigateTo($"/records/{e.Value}");
  }

  private async Task OnReload(ReloadEventArgs args)
  {
    if (_config is null)
      return;

    CancelReload();
    var cts = _reloadCts = new CancellationTokenSource();
    var configId = _config.Id;

    _isLoading = true;
    try
    {
      await Try(async () =>
      {
        var page = await RecordApi.GetPage(
          configId, args.PageNumber, args.PageSize, args.SortColumnKey, args.SortIsDescending, cts.Token);

        _records = page.Items;
        _totalCount = page.TotalCount;
      });
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
      // Запит замінено новішим (інша сторінка, сортування, модель) - результат більше не потрібен,
      // а індикатор завантаження вимкне новий запит.
      return;
    }
    finally
    {
      if (_reloadCts == cts)
        _reloadCts = null;

      cts.Dispose();
    }

    _isLoading = false;
  }

  private void CancelReload()
  {
    _reloadCts?.Cancel();
    _reloadCts = null;
  }

  public void Dispose()
  {
    // Користувач пішов зі сторінки - незавершений запит більше нікому не потрібен.
    CancelReload();
  }

  private void OpenCreate()
  {
    _editing = null;
    _form = _config!.Properties.ToDictionary(p => p.Key, _ => (string?)null);
    _formError = null;
    _isFormOpen = true;
  }

  private void OpenEdit(EntityInstance record)
  {
    _editing = record;
    _form = _config!.Properties.ToDictionary(p => p.Key, p => record.GetRawValue(p.Key));
    _formError = null;
    _isFormOpen = true;
  }

  private async Task Save()
  {
    if (_config is null)
      return;

    // Значення надсилаємо рядками: сервер сам перевіряє і нормалізує їх відповідно до типу поля
    // (числа з комою або крапкою, true/false), тож ця логіка не дублюється в інтерфейсі.
    var values = _config.Properties.ToDictionary(
      p => p.Key,
      p => string.IsNullOrWhiteSpace(_form[p.Key]) ? null : _form[p.Key]);

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

    if (_table != null)
      await _table.Reload(toFirstPage: isNew);
  }

  private async Task Delete()
  {
    if (_config is null || _toDelete is null)
      return;

    var ok = await Try(() => RecordApi.Delete(_config.Id, _toDelete.Id), e => _deleteError = e);
    if (!ok)
      return;

    _toDelete = null;
    if (_table != null)
      await _table.Reload();
  }
}
