using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Domain.Entities;

namespace DataCollector.Server.Application.Services.Internal;

/// <summary>
/// Завантажує модель за Id або ключем - так API зручно викликати і з UI (Id), і із зовнішніх систем (ключ).
/// </summary>
internal static class EntityConfigLoader
{
  public static async Task<EntityConfig> Load(this IEntityConfigRepository repository, string config)
  {
    if (string.IsNullOrWhiteSpace(config))
      throw new NotFoundException("Модель не знайдена");

    var entity = Guid.TryParse(config, out var id)
      ? await repository.GetById(id)
      : await repository.GetByKey(config.Trim());

    return entity ?? throw new NotFoundException($"Модель '{config}' не знайдена");
  }
}
