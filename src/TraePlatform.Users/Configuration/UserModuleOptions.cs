namespace TraePlatform.Users.Configuration;

/// <summary>用户模块配置。</summary>
public sealed class UserModuleOptions
{
    /// <summary>配置节名称。</summary>
    public const string SectionName = "UserModule";

    /// <summary>SQLite 数据库文件路径。</summary>
    public string DatabasePath { get; set; } = "users/trae-users.db";

    /// <summary>是否在初始化时写入默认数据（权限目录、内置角色、管理员账号）。</summary>
    public bool SeedDefaultData { get; set; } = true;

    /// <summary>默认管理员登录名。</summary>
    public string DefaultAdminUserName { get; set; } = "admin";

    /// <summary>默认管理员初始口令。</summary>
    public string DefaultAdminPassword { get; set; } = "Admin@123";

    /// <summary>默认管理员显示名。</summary>
    public string DefaultAdminDisplayName { get; set; } = "系统管理员";

    /// <summary>会话有效期。</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);
}
