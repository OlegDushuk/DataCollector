using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Tests.Unit.Domain;

/// <summary>Модель даних користувача: ключі, поля, правила полів і записи.</summary>
public class EntityModelTests
{
  [Theory]
  [InlineData("orders")]
  [InlineData("Order_Items2")]
  [InlineData("_tmp")]
  public void ModelKey_Valid(string key)
  {
    Assert.Equal(key, new EntityConfig(key, "Модель").Key);
  }

  [Theory]
  [InlineData("")]
  [InlineData("2orders")]
  [InlineData("замовлення")]
  [InlineData("order-items")]
  [InlineData("a b")]
  [InlineData("abcdefghijklmnopqrstuvwxyz1234567")]
  public void ModelKey_Invalid(string key)
  {
    Assert.Throws<DomainValidationException>(() => new EntityConfig(key, "Модель"));
  }

  [Theory]
  [InlineData("id")]
  [InlineData("createdAt")]
  [InlineData("UPDATEDAT")]
  public void PropertyKey_ReservedForSystemFields(string key)
  {
    var config = new EntityConfig("orders", "Замовлення");

    Assert.Throws<DomainValidationException>(() => config.AddProperty(key, "Поле", PropertyDataType.Text));
  }

  [Fact]
  public void PropertyKey_MustBeUnique_CaseInsensitive()
  {
    var config = new EntityConfig("orders", "Замовлення");
    config.AddProperty("price", "Сума", PropertyDataType.Number);

    Assert.Throws<DomainValidationException>(() => config.AddProperty("Price", "Ціна", PropertyDataType.Number));
  }

  [Fact]
  public void Rules_MinGreaterThanMax_Rejected_AndPreviousRulesKept()
  {
    var property = new EntityPropertyConfig("price", "Сума", PropertyDataType.Number,
      rules: new PropertyRules { Min = "0", Max = "10" });

    Assert.Throws<DomainValidationException>(() => property.ApplyRules(new PropertyRules { Min = "100", Max = "10" }));

    // Правила застосовуються атомарно: після помилки діють старі.
    Assert.Equal(0m, property.Min);
    Assert.Equal(10m, property.Max);
  }

  [Theory]
  [InlineData(PropertyDataType.Text, "Min")]
  [InlineData(PropertyDataType.Boolean, "Min")]
  [InlineData(PropertyDataType.Number, "MaxLength")]
  [InlineData(PropertyDataType.Text, "Options")]
  public void Rules_NotApplicableToType_Rejected(PropertyDataType type, string rule)
  {
    var rules = rule switch
    {
      "Min" => new PropertyRules { Min = "1" },
      "MaxLength" => new PropertyRules { MaxLength = 10 },
      _ => new PropertyRules { Options = ["a"] }
    };

    Assert.Throws<DomainValidationException>(() => new EntityPropertyConfig("field", "Поле", type, rules: rules));
  }

  [Fact]
  public void Rules_DefaultValueMustSatisfyRules()
  {
    Assert.Throws<DomainValidationException>(() => new EntityPropertyConfig("status", "Статус", PropertyDataType.Enum,
      rules: new PropertyRules { Options = ["new", "done"], DefaultValue = "unknown" }));
  }

  [Theory]
  [InlineData("price", "-1")]
  [InlineData("price", "100001")]
  [InlineData("number", "ORD-0000000000000000001")]
  [InlineData("status", "lost")]
  public void Value_ViolatingRules_Rejected(string key, string value)
  {
    var instance = TestModels.Orders().CreateInstance();

    Assert.Throws<DomainValidationException>(() => instance.SetPropertyValue(key, value));
  }

  [Fact]
  public void NewRecord_GetsDefaults()
  {
    var instance = TestModels.Orders().CreateInstance();

    Assert.Equal(false, instance.GetProperty("paid").Value);
    Assert.Equal("new", instance.GetProperty("status").Value);
    Assert.Null(instance.GetProperty("price").Value);
  }

  [Fact]
  public void NewRecord_RequiredFieldMissing_Rejected()
  {
    var instance = TestModels.Orders().CreateInstance();
    instance.SetPropertyValue("price", "100");

    var error = Assert.Throws<DomainValidationException>(() => instance.EnsureRequiredFilled());
    Assert.Contains("Номер", error.Message);
  }

  [Fact]
  public void RequiredField_CannotBeCleared()
  {
    var instance = TestModels.Orders().CreateInstance();
    instance.SetPropertyValue("number", "ORD-1");

    Assert.Throws<DomainValidationException>(() => instance.SetPropertyValue("number", null));
  }

  [Fact]
  public void UnknownField_Rejected()
  {
    var instance = TestModels.Orders().CreateInstance();

    Assert.Throws<DomainValidationException>(() => instance.SetPropertyValue("discount", "5"));
  }

  [Fact]
  public void FieldKeys_AreCaseInsensitive()
  {
    var instance = TestModels.Orders().CreateInstance();
    instance.SetPropertyValue("PRICE", "12,5");

    Assert.Equal(12.5m, instance.GetProperty("price").Value);
  }
}
