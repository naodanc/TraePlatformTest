using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraePlatform.Users.Authentication;
using TraePlatform.Users.Authorization;
using TraePlatform.Users.Configuration;
using TraePlatform.Users.Domain;

namespace TraePlatform.Users.Persistence;

/// <summary>用户模块数据库初始化：建库建表、开启 WAL、写入默认权限/角色/管理员。</summary>
public sealed class UserDatabaseInitializer
{
    private readonly IDbContextFactory<UserDbContext> _contextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly UserModuleOptions _options;
    private readonly ILogger<UserDatabaseInitializer> _logger;

    public UserDatabaseInitializer(
        IDbContextFactory<UserDbContext> contextFactory,
        IPasswordHasher passwordHasher,
        IOptions<UserModuleOptions> options,
        ILogger<UserDatabaseInitializer> logger)
    {
        _contextFactory = contextFactory;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await EnableWalAsync(db, cancellationToken).ConfigureAwait(false);

        if (!_options.SeedDefaultData)
        {
            return;
        }

        await SeedAsync(db, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("用户模块数据库初始化完成：{Path}", _options.DatabasePath);
    }

    private static async Task EnableWalAsync(UserDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedAsync(UserDbContext db, CancellationToken cancellationToken)
    {
        var permissions = await EnsurePermissionsAsync(db, cancellationToken).ConfigureAwait(false);
        var roles = await EnsureRolesAsync(db, cancellationToken).ConfigureAwait(false);

        await AssignPermissionsAsync(db, roles["Administrator"].Id, permissions, cancellationToken).ConfigureAwait(false);
        await AssignPermissionsAsync(db, roles["Operator"].Id,
            permissions.Where(p => p.Action is PermissionAction.View or PermissionAction.Operate).ToList(),
            cancellationToken).ConfigureAwait(false);
        await AssignPermissionsAsync(db, roles["Viewer"].Id,
            permissions.Where(p => p.Action == PermissionAction.View).ToList(),
            cancellationToken).ConfigureAwait(false);

        await EnsureAdminUserAsync(db, roles["Administrator"].Id, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<Permission>> EnsurePermissionsAsync(UserDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Permissions.ToListAsync(cancellationToken).ConfigureAwait(false);
        var keys = existing.Select(p => (p.Resource, p.Action)).ToHashSet();

        foreach (var definition in PermissionCatalog.Default)
        {
            foreach (var action in definition.Actions)
            {
                if (keys.Contains((definition.Resource, action))) continue;
                var permission = new Permission
                {
                    Resource = definition.Resource,
                    Action = action,
                    Name = definition.Name,
                    Group = definition.Group
                };
                db.Permissions.Add(permission);
                existing.Add(permission);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing;
    }

    private static async Task<Dictionary<string, Role>> EnsureRolesAsync(UserDbContext db, CancellationToken cancellationToken)
    {
        var definitions = new (string Name, string Description)[]
        {
            ("Administrator", "系统管理员，拥有全部权限"),
            ("Operator", "操作员，可查看与操作"),
            ("Viewer", "访客，仅可查看")
        };

        var existing = await db.Roles.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (name, description) in definitions)
        {
            if (existing.Any(r => r.Name == name)) continue;
            var role = new Role { Name = name, Description = description, IsBuiltIn = true };
            db.Roles.Add(role);
            existing.Add(role);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing.ToDictionary(r => r.Name!);
    }

    private static async Task AssignPermissionsAsync(
        UserDbContext db, int roleId, IReadOnlyCollection<Permission> permissions, CancellationToken cancellationToken)
    {
        var assigned = (await db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToHashSet();

        foreach (var permission in permissions)
        {
            if (assigned.Contains(permission.Id)) continue;
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureAdminUserAsync(UserDbContext db, int adminRoleId, CancellationToken cancellationToken)
    {
        var admin = await db.Users.FirstOrDefaultAsync(u => u.UserName == _options.DefaultAdminUserName, cancellationToken)
            .ConfigureAwait(false);
        if (admin is not null) return;

        admin = new User
        {
            UserName = _options.DefaultAdminUserName,
            DisplayName = _options.DefaultAdminDisplayName,
            PasswordHash = _passwordHasher.Hash(_options.DefaultAdminPassword),
            Status = UserStatus.Active
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRoleId });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
