namespace TraePlatform.Users.Authentication;

/// <summary>口令哈希器。</summary>
public interface IPasswordHasher
{
    /// <summary>生成口令哈希（内含盐与参数）。</summary>
    string Hash(string password);

    /// <summary>校验口令。</summary>
    bool Verify(string password, string encodedHash);
}
