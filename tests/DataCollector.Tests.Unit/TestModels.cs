using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;

namespace DataCollector.Tests.Unit;

/// <summary>Моделі даних для тестів: інтернет-магазин (замовлення) з полями всіх типів.</summary>
internal static class TestModels
{
  public static EntityConfig Orders()
  {
    var config = new EntityConfig("orders", "Замовлення");
    config.AddProperty("number", "Номер", PropertyDataType.Text, new PropertyRules { IsRequired = true, MaxLength = 20 });
    config.AddProperty("price", "Сума", PropertyDataType.Number, new PropertyRules { Min = "0", Max = "100000" });
    config.AddProperty("paid", "Оплачено", PropertyDataType.Boolean, new PropertyRules { DefaultValue = "false" });
    config.AddProperty("deliveryDate", "Дата доставки", PropertyDataType.Date);
    config.AddProperty("paidAt", "Час оплати", PropertyDataType.DateTime);
    config.AddProperty("status", "Статус", PropertyDataType.Enum,
      new PropertyRules { Options = ["new", "sent", "done"], DefaultValue = "new" });
    return config;
  }
}
