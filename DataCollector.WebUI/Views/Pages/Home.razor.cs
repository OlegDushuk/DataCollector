using DataCollector.WebUI.Entities;
using DataCollector.WebUI.Enums;
using DataCollector.WebUI.Views.Base;

namespace DataCollector.WebUI.Views.Pages;

public partial class Home : PageBase
{
  private EntityConfig _entityConfig = new();
  
  private readonly List<EntityInstance> _items =[];

  protected override void OnInitialized()
  {
    _entityConfig.AddProperty("Number", "number", PropertyDataType.Number);
    _entityConfig.AddProperty("Quantity", "quantity", PropertyDataType.Number);
    _entityConfig.AddProperty("Price", "price", PropertyDataType.Number);
    _entityConfig.AddProperty("Discount", "discount", PropertyDataType.Number);

    for (var i = 0; i < 10; i++)
    {
      var item = _entityConfig.CreateInstance();
      item.SetProperty("number", $"00000{i+1}");
      item.SetProperty("quantity", $"{Random.Shared.Next(1, 10)}");
      item.SetProperty("price", $"{Random.Shared.Next(100, 9999)}");
      item.SetProperty("discount", $"{Random.Shared.Next(5, 70)}");
      _items.Add(item);
    }
  }
}