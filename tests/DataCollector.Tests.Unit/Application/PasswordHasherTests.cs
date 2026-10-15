using DataCollector.Server.Application.Security;

namespace DataCollector.Tests.Unit.Application;

public class PasswordHasherTests
{
  private readonly Pbkdf2PasswordHasher _hasher = new();

  [Fact]
  public void Hash_Format_And_Verify()
  {
    var hash = _hasher.Hash("Admin12345");

    var parts = hash.Split('$');
    Assert.Equal(["PBKDF2", "SHA256", "600000"], parts[..3]);
    Assert.Equal(16, Convert.FromBase64String(parts[3]).Length);
    Assert.Equal(32, Convert.FromBase64String(parts[4]).Length);

    Assert.True(_hasher.Verify("Admin12345", hash));
    Assert.False(_hasher.Verify("admin12345", hash));
  }

  [Fact]
  public void SamePassword_DifferentSalt_DifferentHashes()
  {
    Assert.NotEqual(_hasher.Hash("Admin12345"), _hasher.Hash("Admin12345"));
  }

  [Theory]
  [InlineData("")]
  [InlineData("plain-text")]
  [InlineData("PBKDF2$SHA256$abc$c2FsdA==$aGFzaA==")]
  [InlineData("PBKDF2$SHA256$1000$not-base64$aGFzaA==")]
  [InlineData("BCRYPT$SHA256$1000$c2FsdA==$aGFzaA==")]
  public void Verify_MalformedHash_ReturnsFalse(string hash)
  {
    Assert.False(_hasher.Verify("Admin12345", hash));
  }

  [Fact]
  public void Verify_UsesIterationsFromHash_SoTheyCanBeRaisedLater()
  {
    // Хеш, порахований з 1000 ітерацій (наприклад, старий формат), перевіряється за своєю кількістю ітерацій.
    var salt = new byte[16];
    var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2("Admin12345", salt, 1000,
      System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
    var stored = $"PBKDF2$SHA256$1000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";

    Assert.True(_hasher.Verify("Admin12345", stored));
  }
}
