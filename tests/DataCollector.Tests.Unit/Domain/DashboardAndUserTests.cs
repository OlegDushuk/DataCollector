using DataCollector.Server.Domain.Dashboards;
using DataCollector.Server.Domain.Exceptions;
using DataCollector.Server.Domain.Users;

namespace DataCollector.Tests.Unit.Domain;

public class DashboardTests
{
  private static DashboardWidget Widget(int x = 0, int w = 6, int h = 3, string? settings = null)
    => new(null, null, DashboardWidgetType.Chart, null, x, 0, w, h, settings);

  [Theory]
  [InlineData(0, 13, 3)]
  [InlineData(8, 6, 3)]
  [InlineData(0, 6, 0)]
  [InlineData(0, 6, 13)]
  public void Widget_OutsideGrid_Rejected(int x, int w, int h)
  {
    Assert.Throws<DomainValidationException>(() => Widget(x, w, h));
  }

  [Fact]
  public void Widget_SettingsMustBeJsonObject()
  {
    Assert.Throws<DomainValidationException>(() => Widget(settings: "[1, 2]"));
    Assert.Equal("{\"a\":1}", Widget(settings: "{\"a\":1}").Settings.Replace(" ", ""));
  }

  [Fact]
  public void Dashboard_TooManyWidgets_Rejected()
  {
    var widgets = Enumerable.Range(0, Dashboard.MaxWidgets + 1).Select(_ => Widget());

    Assert.Throws<DomainValidationException>(() => new Dashboard("Огляд", widgets: widgets));
  }

  [Fact]
  public void Dashboard_Copy_GetsNewIds()
  {
    var original = new Dashboard("Огляд", widgets: [Widget(), Widget(x: 6)]);

    var copy = original.Copy();

    Assert.NotEqual(original.Id, copy.Id);
    Assert.Equal("Огляд (копія)", copy.Name);
    Assert.Empty(copy.Widgets.Select(w => w.Id).Intersect(original.Widgets.Select(w => w.Id)));
  }

  [Fact]
  public void Dashboard_UnsupportedRefreshInterval_Rejected()
  {
    Assert.Throws<DomainValidationException>(() => new Dashboard("Огляд", refreshSeconds: 7));
  }
}

public class UserTests
{
  [Fact]
  public void FiveFailedLogins_LockForFiveMinutes()
  {
    var user = User.Create("operator", null, UserRole.Editor, "hash");
    var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    for (var i = 1; i < User.MaxFailedLogins; i++)
      Assert.False(user.RegisterFailedLogin(now));

    Assert.True(user.RegisterFailedLogin(now));
    Assert.False(user.CanSignIn(now.AddMinutes(4)));
    Assert.True(user.CanSignIn(now.AddMinutes(5).AddSeconds(1)));
  }

  [Fact]
  public void PasswordOrRoleChange_RotatesSecurityStamp()
  {
    var user = User.Create("operator", null, UserRole.Editor, "hash");
    var stamp = user.SecurityStamp;

    user.Change("Оператор", UserRole.Editor, isActive: true);
    Assert.Equal(stamp, user.SecurityStamp);   // лише ім'я - токени лишаються дійсними

    user.Change("Оператор", UserRole.Viewer, isActive: true);
    Assert.NotEqual(stamp, user.SecurityStamp);

    var afterRole = user.SecurityStamp;
    user.SetPassword("new-hash");
    Assert.NotEqual(afterRole, user.SecurityStamp);
  }

  [Theory]
  [InlineData("short1")]
  [InlineData("onlyletters")]
  [InlineData("1234567890")]
  public void PasswordPolicy_RejectsWeakPasswords(string password)
  {
    Assert.Throws<DomainValidationException>(() => PasswordPolicy.Validate(password));
  }

  [Theory]
  [InlineData("ab")]
  [InlineData("user name")]
  [InlineData("користувач")]
  public void Login_InvalidFormat_Rejected(string login)
  {
    Assert.Throws<DomainValidationException>(() => User.NormalizeLogin(login));
  }

  [Fact]
  public void ApiKey_Generated_OnlyHashStored()
  {
    var (key, plain) = ApiKey.Generate("Датчики теплиці", UserRole.Editor, null);

    Assert.StartsWith(ApiKey.KeyPrefix, plain);
    Assert.True(ApiKey.LooksValid(plain));
    Assert.Equal(ApiKey.Hash(plain), key.KeyHash);
    // Зберігається лише хеш і короткий початок ключа (щоб адміністратор упізнав ключ у списку).
    Assert.StartsWith(key.Prefix, plain);
    Assert.True(key.Prefix.Length < plain.Length / 2);
  }

  [Fact]
  public void ApiKey_AdminRole_NotAllowed()
  {
    Assert.Throws<DomainValidationException>(() => ApiKey.Generate("Інтеграція", UserRole.Admin, null));
  }
}
