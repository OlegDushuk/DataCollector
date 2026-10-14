using System.Data;
using Dapper;
using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.DataAccess.Common;

internal static class TableValuedParameters
{
  public static SqlMapper.ICustomQueryParameter ToPropertyList(Guid entityConfigId, IEnumerable<EntityPropertyConfig> properties)
  {
    var table = new DataTable();

    table.Columns.Add("Id", typeof(Guid));
    table.Columns.Add("EntityConfigId", typeof(Guid));
    table.Columns.Add("Key", typeof(string));
    table.Columns.Add("Name", typeof(string));
    table.Columns.Add("DataType", typeof(int));
    table.Columns.Add("CreatedAt", typeof(DateTime));
    table.Columns.Add("IsRequired", typeof(bool));
    table.Columns.Add("DefaultValue", typeof(string));
    table.Columns.Add("Settings", typeof(string));

    foreach (var prop in properties)
    {
      var rules = prop.GetRules();

      table.Rows.Add(
        prop.Id,
        entityConfigId,
        prop.Key,
        prop.Name,
        (int)prop.Type,
        prop.CreatedAt,
        rules.IsRequired,
        rules.DefaultValue ?? (object)DBNull.Value,
        PropertySettingsJson.Serialize(rules) ?? (object)DBNull.Value);
    }

    return table.AsTableValuedParameter("dbo.EntityPropertyList");
  }

  /// <summary>
  /// Значення всіх полів запису: кожне значення - у колонці свого типу, порожнє - рядок без значень.
  /// Порядок колонок має збігатися з типом dbo.PropertyValueList.
  /// </summary>
  public static SqlMapper.ICustomQueryParameter ToValueList(EntityInstance instance)
  {
    var table = new DataTable();

    table.Columns.Add("EntityPropertyConfigId", typeof(Guid));
    table.Columns.Add("ValueText", typeof(string));
    table.Columns.Add("ValueNumber", typeof(decimal));
    table.Columns.Add("ValueBool", typeof(bool));
    table.Columns.Add("ValueDate", typeof(DateTime));

    foreach (var prop in instance.Properties)
    {
      table.Rows.Add(
        prop.Config.Id,
        prop.Value as string ?? (object)DBNull.Value,
        prop.Value is decimal number ? number : DBNull.Value,
        prop.Value is bool flag ? flag : DBNull.Value,
        ToDateColumn(prop.Value));
    }

    return table.AsTableValuedParameter("dbo.PropertyValueList");
  }

  /// <summary>Записи для пакетних процедур (dbo.InstanceList): Id і дати.</summary>
  public static SqlMapper.ICustomQueryParameter ToInstanceList(IEnumerable<EntityInstance> instances)
  {
    var table = new DataTable();

    table.Columns.Add("Id", typeof(Guid));
    table.Columns.Add("CreatedAt", typeof(DateTime));
    table.Columns.Add("UpdatedAt", typeof(DateTime));

    foreach (var instance in instances)
      table.Rows.Add(instance.Id, instance.CreatedAt, instance.UpdatedAt ?? (object)DBNull.Value);

    return table.AsTableValuedParameter("dbo.InstanceList");
  }

  /// <summary>
  /// Значення кількох записів (dbo.InstanceValueList). <paramref name="propertyIds"/> - лише ці поля
  /// (оновлення частини колонок), null - усі поля запису.
  /// </summary>
  public static SqlMapper.ICustomQueryParameter ToInstanceValueList(
    IEnumerable<EntityInstance> instances,
    IReadOnlyCollection<Guid>? propertyIds = null)
  {
    var table = new DataTable();

    table.Columns.Add("InstanceId", typeof(Guid));
    table.Columns.Add("EntityPropertyConfigId", typeof(Guid));
    table.Columns.Add("ValueText", typeof(string));
    table.Columns.Add("ValueNumber", typeof(decimal));
    table.Columns.Add("ValueBool", typeof(bool));
    table.Columns.Add("ValueDate", typeof(DateTime));

    var filter = propertyIds is null ? null : new HashSet<Guid>(propertyIds);

    foreach (var instance in instances)
    {
      foreach (var prop in instance.Properties)
      {
        if (filter != null && !filter.Contains(prop.Config.Id))
          continue;

        // Для нового запису порожні значення не потрібні; для оновлення порожнє значення очищає поле.
        if (filter is null && prop.Value is null)
          continue;

        table.Rows.Add(
          instance.Id,
          prop.Config.Id,
          prop.Value as string ?? (object)DBNull.Value,
          prop.Value is decimal number ? number : DBNull.Value,
          prop.Value is bool flag ? flag : DBNull.Value,
          ToDateColumn(prop.Value));
      }
    }

    return table.AsTableValuedParameter("dbo.InstanceValueList");
  }

  /// <summary>Набір ідентифікаторів (dbo.IdList).</summary>
  public static SqlMapper.ICustomQueryParameter ToIdList(IEnumerable<Guid> ids)
  {
    var table = new DataTable();
    table.Columns.Add("Id", typeof(Guid));

    foreach (var id in ids.Distinct())
      table.Rows.Add(id);

    return table.AsTableValuedParameter("dbo.IdList");
  }

  /// <summary>Date і DateTime зберігаються в одній колонці DATETIME2.</summary>
  private static object ToDateColumn(object? value) => value switch
  {
    DateOnly date => date.ToDateTime(TimeOnly.MinValue),
    DateTime dateTime => dateTime,
    _ => DBNull.Value
  };
}
