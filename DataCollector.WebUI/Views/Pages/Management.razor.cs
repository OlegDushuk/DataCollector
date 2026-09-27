using DataCollector.WebUI.Entities;
using DataCollector.WebUI.Helpers;
using DataCollector.WebUI.ServerApiServices;
using DataCollector.WebUI.Views.Base;
using DataCollector.WebUI.Views.Components.DataTable;
using DataCollector.WebUI.Views.Components.DataTable.Models;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Pages;

public partial class Management : PageBase
{
  [Inject] private EntityConfigApiService ConfigApi { get; set; } = null!;

  private DataTable<EntityConfigListItem> _table = null!;

  // Моделей небагато, тому завантажуємо всі і сортуємо/гортаємо на клієнті.
  private List<EntityConfigListItem> _all = [];
  private List<EntityConfigListItem> _page = [];
  private bool _isLoading = true;

  private bool _isCreateOpen;
  private string _newName = string.Empty;
  private string _newKey = string.Empty;
  private bool _newKeyTouched;
  private List<EntityConfigApiService.NewProperty> _newProperties = [];
  private string? _createError;

  private EntityConfigListItem? _toDelete;
  private string? _deleteError;

  private async Task OnReload(ReloadEventArgs args)
  {
    _isLoading = true;
    await Try(async () => _all = await ConfigApi.GetAll());
    _isLoading = false;

    IEnumerable<EntityConfigListItem> sorted = args.SortColumnKey switch
    {
      "name" => Sort(x => x.Name),
      "key" => Sort(x => x.Key),
      "properties" => Sort(x => x.PropertyCount),
      "records" => Sort(x => x.RecordCount),
      _ => Sort(x => x.CreatedAt)
    };

    _page = sorted
      .Skip((args.PageNumber - 1) * args.PageSize)
      .Take(args.PageSize)
      .ToList();

    IEnumerable<EntityConfigListItem> Sort<TKey>(Func<EntityConfigListItem, TKey> key)
      => args.SortIsDescending ? _all.OrderByDescending(key) : _all.OrderBy(key);
  }

  private void OpenCreate()
  {
    _newName = string.Empty;
    _newKey = string.Empty;
    _newKeyTouched = false;
    _newProperties = [];
    _createError = null;
    _isCreateOpen = true;
  }

  private void OnNewNameInput(ChangeEventArgs e)
  {
    _newName = e.Value?.ToString() ?? string.Empty;

    if (!_newKeyTouched)
      _newKey = KeyGenerator.FromName(_newName);
  }

  private async Task Create()
  {
    _createError = null;

    var ok = await Try(async () =>
    {
      var id = await ConfigApi.Create(_newName, _newKey, _newProperties);
      Navigation.NavigateTo($"/management/{id}");
    }, error => _createError = error);

    if (ok)
      _isCreateOpen = false;
  }

  private void AskDelete(EntityConfigListItem item)
  {
    _deleteError = null;
    _toDelete = item;
  }

  private async Task Delete()
  {
    if (_toDelete is null)
      return;

    var ok = await Try(() => ConfigApi.Delete(_toDelete.Id), error => _deleteError = error);
    if (!ok)
      return;

    _toDelete = null;
    await _table.Reload();
  }
}
