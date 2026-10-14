using System.Globalization;
using System.Security.Cryptography;

namespace DataCollector.Server.Application.Security;

/// <summary>Хешування паролів.</summary>
public interface IPasswordHasher
{
  string Hash(string password);

  /// <summary>Чи відповідає пароль хешу. Час перевірки не залежить від того, на якому символі розбіжність.</summary>
  bool Verify(string password, string hash);
}

/// <summary>
/// PBKDF2-HMAC-SHA256 з випадковою сіллю 16 байт і 600 000 ітерацій (рекомендація OWASP, 2023).
/// Формат: <c>PBKDF2$SHA256$600000$сіль$хеш</c> (Base64) - кількість ітерацій зберігається в самому хеші,
/// тож її можна збільшити пізніше, а старі паролі продовжать перевірятися.
/// </summary>
/// <remarks>
/// Повільний хеш потрібен, щоб викрадену таблицю паролів не можна було швидко перебрати.
/// Сіль робить однакові паролі різними хешами (немає сенсу в заздалегідь порахованих таблицях).
/// </remarks>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
  private const string Algorithm = "PBKDF2";
  private const string Digest = "SHA256";
  private const int Iterations = 600_000;
  private const int SaltSize = 16;
  private const int HashSize = 32;

  public string Hash(string password)
  {
    var salt = RandomNumberGenerator.GetBytes(SaltSize);
    var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

    return string.Join('$', Algorithm, Digest, Iterations.ToString(CultureInfo.InvariantCulture),
      Convert.ToBase64String(salt), Convert.ToBase64String(hash));
  }

  public bool Verify(string password, string hash)
  {
    var parts = hash.Split('$');
    if (parts.Length != 5 || parts[0] != Algorithm || parts[1] != Digest ||
        !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations < 1)
      return false;

    byte[] salt, expected;
    try
    {
      salt = Convert.FromBase64String(parts[3]);
      expected = Convert.FromBase64String(parts[4]);
    }
    catch (FormatException)
    {
      return false;
    }

    var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
    return CryptographicOperations.FixedTimeEquals(actual, expected);
  }
}
