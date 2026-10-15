using DataCollector.Server.Application.Common.Exceptions;
using DataCollector.Server.Application.ExternalContracts.Models;
using DataCollector.Server.Application.Interfaces.Repositories;
using DataCollector.Server.Domain.Dashboards;
using DataCollector.Server.Domain.Entities;
using DataCollector.Server.Domain.Monitoring;
using DataCollector.Server.Domain.Querying;
using DataCollector.Server.Domain.Users;

namespace DataCollector.Tests.Shared;

/// <summary>
/// Сховище в пам'яті замість SQL Server для тестів сервісів і API. Поводиться як збережені процедури там,
/// де від цього залежить логіка застосунку: записи й дашборди мають версію, що змінюється при кожному збереженні,
/// і зміна за застарілою версією відхиляється (як UPDATE … WHERE RowVersion = @ExpectedVersion).
/// Пошук з фільтрами й агрегація виконуються в БД динамічним SQL - його перевіряють окремі тести тексту SQL.
/// </summary>
public sealed class InMemoryStore
{
  private long _version = 1000;
  internal readonly object Sync = new();

  public Dictionary<Guid, EntityConfig> Configs { get; } = [];
  public Dictionary<Guid, StoredInstance> Instances { get; } = [];
  public Dictionary<Guid, StoredDashboard> Dashboards { get; } = [];
  public Dictionary<Guid, User> Users { get; } = [];
  public Dictionary<Guid, ApiKey> ApiKeys { get; } = [];
  public Dictionary<Guid, (MonitoringRule Rule, string ConfigName)> Rules { get; } = [];

  /// <summary>Імітація недоступної бази для перевірки стану.</summary>
  public bool DatabaseDown { get; set; }

  internal long NextVersion() => Interlocked.Increment(ref _version);

  /// <summary>Змінити запис «в обхід» сервісу - ніби його зберіг інший користувач.</summary>
  public void TouchInstance(Guid id)
  {
    lock (Sync)
      Instances[id] = Instances[id] with { Version = NextVersion(), UpdatedAt = DateTime.UtcNow };
  }

  public sealed record StoredInstance(
    Guid Id, Guid ConfigId, DateTime CreatedAt, DateTime? UpdatedAt, long Version, Dictionary<Guid, object?> Values);

  public sealed record StoredDashboard(Dashboard Dashboard, long Version);
}

public sealed class InMemoryEntityConfigRepository(InMemoryStore store) : IEntityConfigRepository
{
  public Task<List<EntityConfigListItemDto>> GetAll()
  {
    lock (store.Sync)
      return Task.FromResult(store.Configs.Values.Select(c => new EntityConfigListItemDto
      {
        Id = c.Id,
        Key = c.Key,
        Name = c.Name,
        CreatedAt = c.CreatedAt,
        PropertyCount = c.Properties.Count,
        RecordCount = store.Instances.Values.Count(i => i.ConfigId == c.Id)
      }).ToList());
  }

  public Task<EntityConfig?> GetById(Guid id)
  {
    lock (store.Sync)
      return Task.FromResult(store.Configs.GetValueOrDefault(id));
  }

  public Task<EntityConfig?> GetByKey(string key)
  {
    lock (store.Sync)
      return Task.FromResult(store.Configs.Values.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)));
  }

  public Task Create(EntityConfig model)
  {
    lock (store.Sync)
    {
      if (store.Configs.Values.Any(c => string.Equals(c.Key, model.Key, StringComparison.OrdinalIgnoreCase)))
        throw new ConflictException("Запис з такими даними вже існує");

      store.Configs[model.Id] = model;
    }

    return Task.CompletedTask;
  }

  public Task Update(EntityConfig model)
  {
    lock (store.Sync)
      store.Configs[model.Id] = model;
    return Task.CompletedTask;
  }

  public Task Delete(Guid id)
  {
    lock (store.Sync)
    {
      if (!store.Configs.Remove(id))
        throw new NotFoundException("Модель не знайдена");

      foreach (var instance in store.Instances.Values.Where(i => i.ConfigId == id).ToList())
        store.Instances.Remove(instance.Id);
    }

    return Task.CompletedTask;
  }
}

public sealed class InMemoryEntityPropertyConfigRepository : IEntityPropertyConfigRepository
{
  // Поля - частина EntityConfig у пам'яті: модель уже змінена сервісом.
  public Task CreateMany(Guid entityConfigId, IEnumerable<EntityPropertyConfig> properties) => Task.CompletedTask;
  public Task Update(EntityPropertyConfig property) => Task.CompletedTask;
  public Task Delete(Guid propertyId) => Task.CompletedTask;
}

public sealed class InMemoryEntityInstanceRepository(InMemoryStore store) : IEntityInstanceRepository
{
  /// <summary>Сторінка записів моделі: пошук у текстових полях, сортування за часом створення. Умови фільтра не застосовуються.</summary>
  public Task<(List<EntityInstance> Items, int TotalCount)> Query(EntityConfig config, RecordQuery query, CancellationToken cancellationToken = default)
  {
    lock (store.Sync)
    {
      var all = store.Instances.Values
        .Where(i => i.ConfigId == config.Id)
        .Select(i => Build(config, i))
        .Where(i => query.Search is null || i.Properties.Any(p =>
          p.Value is string text && text.Contains(query.Search, StringComparison.OrdinalIgnoreCase)));

      all = query.SortDescending
        ? all.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
        : all.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id);

      var list = all.ToList();
      var page = list.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToList();
      return Task.FromResult((page, list.Count));
    }
  }

  public Task<AggregateResult> Aggregate(EntityConfig config, AggregateQuery query, CancellationToken cancellationToken = default)
    => throw new NotSupportedException("Агрегація виконується в SQL Server; у тестах у пам'яті - не підтримується");

  public Task<EntityInstance?> GetById(EntityConfig config, Guid id)
  {
    lock (store.Sync)
      return Task.FromResult(store.Instances.TryGetValue(id, out var stored) && stored.ConfigId == config.Id
        ? Build(config, stored)
        : null);
  }

  public Task Create(EntityInstance instance)
  {
    lock (store.Sync)
      store.Instances[instance.Id] = Snapshot(instance, store.NextVersion());
    return Task.CompletedTask;
  }

  public Task<long> Update(EntityInstance instance)
  {
    lock (store.Sync)
    {
      if (!store.Instances.TryGetValue(instance.Id, out var stored))
        throw new NotFoundException("Запис не знайдений");

      if (stored.Version != instance.Version)
        throw new VersionConflictException(VersionConflictException.Record);

      var version = store.NextVersion();
      store.Instances[instance.Id] = Snapshot(instance, version) with { CreatedAt = stored.CreatedAt };
      return Task.FromResult(version);
    }
  }

  public Task Delete(Guid id, long? expectedVersion = null)
  {
    lock (store.Sync)
    {
      if (!store.Instances.TryGetValue(id, out var stored))
        throw new NotFoundException("Запис не знайдений");

      if (expectedVersion is { } expected && expected != stored.Version)
        throw new VersionConflictException(VersionConflictException.Record);

      store.Instances.Remove(id);
    }

    return Task.CompletedTask;
  }

  public Task<Dictionary<Guid, Guid>> GetOwners(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
  {
    lock (store.Sync)
      return Task.FromResult(ids.Where(store.Instances.ContainsKey).Distinct().ToDictionary(id => id, id => store.Instances[id].ConfigId));
  }

  public Task Import(
    EntityConfig config,
    IReadOnlyList<EntityInstance> created,
    IReadOnlyList<EntityInstance> updated,
    IReadOnlyCollection<Guid> updatedPropertyIds,
    CancellationToken cancellationToken = default)
  {
    lock (store.Sync)
    {
      foreach (var instance in created)
        store.Instances[instance.Id] = Snapshot(instance, store.NextVersion());

      foreach (var instance in updated)
      {
        var stored = store.Instances[instance.Id];
        var values = new Dictionary<Guid, object?>(stored.Values);
        foreach (var property in instance.Properties.Where(p => updatedPropertyIds.Contains(p.Config.Id)))
          values[property.Config.Id] = property.Value;

        store.Instances[instance.Id] = stored with { Values = values, UpdatedAt = instance.UpdatedAt ?? DateTime.UtcNow, Version = store.NextVersion() };
      }
    }

    return Task.CompletedTask;
  }

  private static InMemoryStore.StoredInstance Snapshot(EntityInstance instance, long version)
    => new(instance.Id, instance.Config.Id, instance.CreatedAt, instance.UpdatedAt, version,
      instance.Properties.ToDictionary(p => p.Config.Id, p => p.Value));

  private static EntityInstance Build(EntityConfig config, InMemoryStore.StoredInstance stored)
  {
    var instance = new EntityInstance(config, stored.Id, stored.CreatedAt, stored.UpdatedAt, stored.Version);
    foreach (var (propertyId, value) in stored.Values)
      instance.RestorePropertyValue(propertyId, value);
    return instance;
  }
}

public sealed class InMemoryDashboardRepository(InMemoryStore store) : IDashboardRepository
{
  public Task<List<DashboardListItemDto>> GetAll()
  {
    lock (store.Sync)
      return Task.FromResult(store.Dashboards.Values.Select(d => new DashboardListItemDto
      {
        Id = d.Dashboard.Id,
        Name = d.Dashboard.Name,
        Description = d.Dashboard.Description,
        RefreshSeconds = d.Dashboard.RefreshSeconds,
        CreatedAt = d.Dashboard.CreatedAt,
        UpdatedAt = d.Dashboard.UpdatedAt,
        WidgetCount = d.Dashboard.Widgets.Count
      }).ToList());
  }

  public Task<Dashboard?> GetById(Guid id)
  {
    lock (store.Sync)
    {
      if (!store.Dashboards.TryGetValue(id, out var stored))
        return Task.FromResult<Dashboard?>(null);

      // Окремий об'єкт на кожне читання - як рядок з БД.
      var d = stored.Dashboard;
      return Task.FromResult<Dashboard?>(new Dashboard(
        d.Name, d.Description, d.RefreshSeconds, d.Widgets, d.Id, d.CreatedAt, d.UpdatedAt, stored.Version));
    }
  }

  public Task Create(Dashboard dashboard)
  {
    lock (store.Sync)
      store.Dashboards[dashboard.Id] = new InMemoryStore.StoredDashboard(dashboard, store.NextVersion());
    return Task.CompletedTask;
  }

  public Task<long> Update(Dashboard dashboard)
  {
    lock (store.Sync)
    {
      if (!store.Dashboards.TryGetValue(dashboard.Id, out var stored))
        throw new NotFoundException("Дашборд не знайдений");

      if (stored.Version != dashboard.Version)
        throw new VersionConflictException(VersionConflictException.Dashboard);

      var version = store.NextVersion();
      store.Dashboards[dashboard.Id] = new InMemoryStore.StoredDashboard(dashboard, version);
      return Task.FromResult(version);
    }
  }

  public Task Delete(Guid id)
  {
    lock (store.Sync)
      if (!store.Dashboards.Remove(id))
        throw new NotFoundException("Дашборд не знайдений");
    return Task.CompletedTask;
  }
}

public sealed class InMemoryUserRepository(InMemoryStore store) : IUserRepository
{
  public Task<List<User>> GetAll()
  {
    lock (store.Sync)
      return Task.FromResult(store.Users.Values.ToList());
  }

  public Task<User?> GetById(Guid id)
  {
    lock (store.Sync)
      return Task.FromResult(store.Users.GetValueOrDefault(id));
  }

  public Task<User?> GetByLogin(string login)
  {
    lock (store.Sync)
      return Task.FromResult(store.Users.Values.FirstOrDefault(u => string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase)));
  }

  public Task Create(User user)
  {
    lock (store.Sync)
    {
      if (store.Users.Values.Any(u => string.Equals(u.Login, user.Login, StringComparison.OrdinalIgnoreCase)))
        throw new ConflictException("Запис з такими даними вже існує");
      store.Users[user.Id] = user;
    }

    return Task.CompletedTask;
  }

  public Task Update(User user)
  {
    lock (store.Sync)
      store.Users[user.Id] = user;
    return Task.CompletedTask;
  }
}

public sealed class InMemoryApiKeyRepository(InMemoryStore store) : IApiKeyRepository
{
  public Task<List<ApiKeyRecord>> GetAll()
  {
    lock (store.Sync)
      return Task.FromResult(store.ApiKeys.Values.Select(k => new ApiKeyRecord(k, null)).ToList());
  }

  public Task<ApiKey?> GetById(Guid id)
  {
    lock (store.Sync)
      return Task.FromResult(store.ApiKeys.GetValueOrDefault(id));
  }

  public Task<ApiKey?> GetByHash(byte[] keyHash)
  {
    lock (store.Sync)
      return Task.FromResult(store.ApiKeys.Values.FirstOrDefault(k => k.KeyHash.AsSpan().SequenceEqual(keyHash)));
  }

  public Task Create(ApiKey key)
  {
    lock (store.Sync)
      store.ApiKeys[key.Id] = key;
    return Task.CompletedTask;
  }

  public Task Revoke(Guid id, DateTime revokedAt)
  {
    lock (store.Sync)
      store.ApiKeys[id].Revoke(revokedAt);
    return Task.CompletedTask;
  }

  public Task MarkUsed(Guid id, DateTime usedAt)
  {
    lock (store.Sync)
      if (store.ApiKeys.TryGetValue(id, out var key))
        key.MarkUsed(usedAt);
    return Task.CompletedTask;
  }
}

public sealed class InMemoryMonitoringRuleRepository(InMemoryStore store) : IMonitoringRuleRepository
{
  public Task<List<RuleRecord>> GetAll()
  {
    lock (store.Sync)
      return Task.FromResult(store.Rules.Values.Select(r => new RuleRecord(r.Rule, r.ConfigName)).ToList());
  }

  public Task<RuleRecord?> GetById(Guid id)
  {
    lock (store.Sync)
      return Task.FromResult(store.Rules.TryGetValue(id, out var r) ? new RuleRecord(r.Rule, r.ConfigName) : null);
  }

  public Task Create(MonitoringRule rule)
  {
    lock (store.Sync)
      store.Rules[rule.Id] = (rule, store.Configs.GetValueOrDefault(rule.ConfigId)?.Name ?? "");
    return Task.CompletedTask;
  }

  public Task Update(MonitoringRule rule, int expectedVersion)
  {
    lock (store.Sync)
    {
      if (!store.Rules.TryGetValue(rule.Id, out var stored))
        throw new NotFoundException("Правило не знайдене");

      // У пам'яті правило - той самий об'єкт, тож «чужу» зміну тест імітує через StoredRuleVersions.
      if (StoredRuleVersions.TryGetValue(rule.Id, out var current) && current != expectedVersion)
        throw new VersionConflictException(VersionConflictException.Rule);

      StoredRuleVersions[rule.Id] = rule.Version;
      store.Rules[rule.Id] = (rule, stored.ConfigName);
    }

    return Task.CompletedTask;
  }

  /// <summary>Версія правила «в базі» (якщо тест її задав).</summary>
  public Dictionary<Guid, int> StoredRuleVersions { get; } = [];

  public Task Delete(Guid id)
  {
    lock (store.Sync)
      store.Rules.Remove(id);
    return Task.CompletedTask;
  }

  public Task<List<RuleRecord>> ClaimDue(DateTime nowUtc, int take) => Task.FromResult(new List<RuleRecord>());

  public Task<bool> SaveCheck(MonitoringRule rule, NotificationDraft? notification, DeliveryStatus delivery) => Task.FromResult(true);
}

public sealed class InMemoryNotificationRepository : INotificationRepository
{
  public Task<List<NotificationDto>> GetRecent(int take, bool unreadOnly, Guid? ruleId, DateTime? before) => Task.FromResult(new List<NotificationDto>());
  public Task<int> CountUnread() => Task.FromResult(0);
  public Task MarkRead(Guid? id, DateTime nowUtc) => Task.CompletedTask;
  public Task<List<NotificationDto>> GetPendingDelivery(int take) => Task.FromResult(new List<NotificationDto>());
  public Task SetDelivery(Guid id, DeliveryStatus status, int attempts, DateTime? deliveredAt, string? error) => Task.CompletedTask;
}

public sealed class InMemoryDatabaseHealth(InMemoryStore store) : IDatabaseHealth
{
  public Task Ping(CancellationToken cancellationToken = default)
    => store.DatabaseDown ? throw new InvalidOperationException("База даних недоступна (тест)") : Task.CompletedTask;
}
