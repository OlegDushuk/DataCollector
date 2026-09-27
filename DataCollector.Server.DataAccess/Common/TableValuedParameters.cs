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

    foreach (var prop in properties)
      table.Rows.Add(prop.Id, entityConfigId, prop.Key, prop.Name, (int)prop.Type, prop.CreatedAt);

    return table.AsTableValuedParameter("dbo.EntityPropertyList");
  }

  public static SqlMapper.ICustomQueryParameter ToValueList(EntityInstance instance)
  {
    var table = new DataTable();

    table.Columns.Add("Id", typeof(Guid));
    table.Columns.Add("EntityPropertyConfigId", typeof(Guid));
    table.Columns.Add("Value", typeof(string));
    table.Columns.Add("CreatedAt", typeof(DateTime));

    foreach (var prop in instance.Properties)
      table.Rows.Add(prop.Id, prop.Config.Id, (object?)prop.Value ?? DBNull.Value, prop.CreatedAt);

    return table.AsTableValuedParameter("dbo.PropertyValueList");
  }
}
