using DataCollector.WebUI.Entities;
using DataCollector.WebUI.ServerApiServices;
using DataCollector.WebUI.Views.Base;
using Microsoft.AspNetCore.Components;

namespace DataCollector.WebUI.Views.Pages;

public partial class ModelDetails : PageBase
{
  [Parameter] public Guid Id { get; set; }

  [Inject] private EntityConfigApiService ConfigApi { get; set; } = null!;

  private EntityConfig? _config;
  private bool _isLoading = true;
  private string? _success;

  private string _name = string.Empty;
  private string _key = string.Empty;

  private bool _isAddOpen;
  private List<EntityConfigApiService.NewProperty> _newProperties = [];
  private string? _addError;

  private EntityPropertyConfig? _propertyToEdit;
  private string _editName = string.Empty;
  private string _editKey = string.Empty;
  private string? _editError;

  private EntityPropertyConfig? _propertyToDelete;
  private string? _deletePropertyError;

  private bool _isDeleteOpen;
  private string? _deleteError;

  protected override async Task OnParametersSetAsync()
  {
    await Load();
  }

  private async Task Load()
  {
    _isLoading = true;
    await Try(async () =>
    {
      _config = await ConfigApi.Get(Id.ToString());
      _name = _config.Name;
      _key = _config.Key;
    });
    _isLoading = false;
  }

  private async Task Save()
  {
    Error = null;
    _success = null;

    var ok = await Try(() => ConfigApi.Edit(Id, _name, _key));
    if (!ok)
      return;

    _success = "Зміни збережено";
    await Load();
  }

  private void OpenAdd()
  {
    _newProperties = [new EntityConfigApiService.NewProperty()];
    _addError = null;
    _isAddOpen = true;
  }

  private async Task AddProperties()
  {
    var ok = await Try(() => ConfigApi.AddProperties(Id, _newProperties), e => _addError = e);
    if (!ok)
      return;

    _isAddOpen = false;
    await Load();
  }

  private void OpenEdit(EntityPropertyConfig prop)
  {
    _propertyToEdit = prop;
    _editName = prop.Name;
    _editKey = prop.Key;
    _editError = null;
  }

  private async Task SaveProperty()
  {
    if (_propertyToEdit is null)
      return;

    var ok = await Try(() => ConfigApi.EditProperty(Id, _propertyToEdit.Id, _editName, _editKey), e => _editError = e);
    if (!ok)
      return;

    _propertyToEdit = null;
    await Load();
  }

  private async Task DeleteProperty()
  {
    if (_propertyToDelete is null)
      return;

    var ok = await Try(() => ConfigApi.DeleteProperty(Id, _propertyToDelete.Id), e => _deletePropertyError = e);
    if (!ok)
      return;

    _propertyToDelete = null;
    await Load();
  }

  private async Task DeleteModel()
  {
    var ok = await Try(() => ConfigApi.Delete(Id), e => _deleteError = e);
    if (ok)
      Navigation.NavigateTo("/management");
  }
}
