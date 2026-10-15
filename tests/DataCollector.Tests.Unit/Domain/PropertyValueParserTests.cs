using DataCollector.Server.Domain.Common;
using DataCollector.Server.Domain.Enums;
using DataCollector.Server.Domain.Exceptions;

namespace DataCollector.Tests.Unit.Domain;

/// <summary>Розбір значень полів з тексту (форма, JSON, імпорт) за типом поля.</summary>
public class PropertyValueParserTests
{
  [Theory]
  [InlineData("120.5", "120.5")]
  [InlineData("120,5", "120.5")]
  [InlineData("1 882,31", "1882.31")]
  [InlineData("1 000", "1000")]
  [InlineData("-0.25", "-0.25")]
  [InlineData("120.50", "120.5")]
  public void Number_AcceptsCommaDotAndThousandSeparators(string input, string expected)
  {
    var value = PropertyValueParser.Parse(PropertyDataType.Number, "Сума", input);

    Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    Assert.Equal(expected, PropertyValueParser.ToInvariantString(value));
  }

  [Theory]
  [InlineData("abc")]
  [InlineData("1.2.3")]
  [InlineData("10000000000000000000000000000")]
  public void Number_RejectsInvalidOrTooLarge(string input)
  {
    Assert.Throws<DomainValidationException>(() => PropertyValueParser.Parse(PropertyDataType.Number, "Сума", input));
  }

  [Fact]
  public void Number_IsRoundedToStorageScale()
  {
    var value = PropertyValueParser.Parse(PropertyDataType.Number, "Сума", "0.123456789012345");

    Assert.Equal(0.1234567890m, value);
  }

  [Theory]
  [InlineData("true", true)]
  [InlineData("False", false)]
  [InlineData("1", true)]
  [InlineData("0", false)]
  [InlineData("так", true)]
  [InlineData("Ні", false)]
  public void Boolean_AcceptsCommonForms(string input, bool expected)
  {
    Assert.Equal(expected, PropertyValueParser.Parse(PropertyDataType.Boolean, "Оплачено", input));
  }

  [Theory]
  [InlineData("2026-10-11")]
  [InlineData("11.10.2026")]
  [InlineData("2026-10-11T08:30:00")]
  public void Date_AcceptsIsoAndUkrainianFormats_TimeIsDropped(string input)
  {
    Assert.Equal(new DateOnly(2026, 10, 11), PropertyValueParser.Parse(PropertyDataType.Date, "Дата", input));
  }

  [Fact]
  public void DateTime_WithOffset_IsConvertedToUtc()
  {
    var value = (DateTime)PropertyValueParser.Parse(PropertyDataType.DateTime, "Час", "2026-10-11T10:30:00+03:00")!;

    Assert.Equal(new DateTime(2026, 10, 11, 7, 30, 0), value);
    Assert.Equal(DateTimeKind.Utc, value.Kind);
  }

  [Fact]
  public void DateTime_WithoutOffset_IsTreatedAsUtc()
  {
    var value = (DateTime)PropertyValueParser.Parse(PropertyDataType.DateTime, "Час", "2026-10-11 08:30")!;

    Assert.Equal(new DateTime(2026, 10, 11, 8, 30, 0, DateTimeKind.Utc), value);
    Assert.Equal(DateTimeKind.Utc, value.Kind);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void EmptyValue_IsNull(string? input)
  {
    Assert.Null(PropertyValueParser.Parse(PropertyDataType.Number, "Сума", input));
  }

  [Fact]
  public void Text_LongerThanLimit_IsRejected()
  {
    var text = new string('а', PropertyValueParser.MaxTextLength + 1);

    var error = Assert.Throws<DomainValidationException>(() => PropertyValueParser.Parse(PropertyDataType.Text, "Опис", text));
    Assert.Contains("Опис", error.Message);
  }
}
