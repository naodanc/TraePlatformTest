using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TraePlatform.Logging;
using TraePlatform.Users.Authentication;
using TraePlatform.Users.Domain;
using TraePlatform.Users.Persistence;

namespace TraePlatform.Users.Management;

/// <summary>用户与权限管理服务实现。</summary>
public sealed class UserManagementService : IUserManagementService
{
    private readonly IDbContextFactory<UserDbContext> _contextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        IDbContextFactory<UserDbContext> contextFactory,
        IPasswordHasher passwordHasher,
        ILogger<UserManagementService> logger)
    {
        _contextFactory = contextFactory;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Roles.AsNoTracking()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Permissions.AsNoTracking()
            .OrderBy(p => p.Group).ThenBy(p => p.Resource).ThenBy(p => p.Action)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CreateUserAsync(
        string userName, string password, string displayName,
        IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        using var scope = LogContextScope.Push(module: "UserManagement");

        if (!await TryValidateNewUserAsync(userName, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"用户名 {userName} 已存在。");
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = new User
        {
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName,
            PasswordHash = _passwordHasher.Hash(password),
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await AssignRolesAsync(db, user.Id, roleNames, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("创建用户 {UserName}，角色 {Roles}", userName, string.Join(",", roleNames));
        return user.Id;
    }

    public async Task SetUserRolesAsync(int userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        using var scope = LogContextScope.Push(module: "UserManagement");

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"用户 {userId} 不存在。");

        await AssignRolesAsync(db, user.Id, roleNames, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("调整用户 {UserName} 角色为 {Roles}", user.UserName, string.Join(",", roleNames));
    }

    public async Task SetUserPasswordAsync(int userId, string password, CancellationToken cancellationToken = default)
    {
        using var scope = LogContextScope.Push(module: "UserManagement");

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"用户 {userId} 不存在。");

        user.PasswordHash = _passwordHasher.Hash(password);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("重设用户 {UserName} 口令", user.UserName);
    }

    public async Task SetUserStatusAsync(int userId, UserStatus status, CancellationToken cancellationToken = default)
    {
        using var scope = LogContextScope.Push(module: "UserManagement");

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"用户 {userId} 不存在。");

        user.Status = status;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("用户 {UserName} 状态调整为 {Status}", user.UserName, status);
    }

    public async Task<int> CreateRoleAsync(
        string name, string? description, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        using var scope = LogContextScope.Push(module: "UserManagement");

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (await db.Roles.AnyAsync(r => r.Name == name, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"角色 {name} 已存在。");
        }

        var role = new Role { Name = name, Description = description };
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await ApplyRolePermissionsAsync(db, role.Id, permissionCodes, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("创建角色 {Role}", name);
        return role.Id;
    }

    public async Task SetRolePermissionsAsync(
        int roleId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        using var scope = LogContextScope.Push(module: "UserManagement");

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await ApplyRolePermissionsAsync(db, roleId, permissionCodes, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("调整角色 {RoleId} 权限", roleId);
    }

    private async Task<bool> TryValidateNewUserAsync(string userName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new InvalidOperationException("用户名不能为空。");
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return !await db.Users.AnyAsync(u => u.UserName == userName, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AssignRolesAsync(
        UserDbContext db, int userId, IEnumerable<string> roleNames, CancellationToken cancellationToken)
    {
        var names = roleNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var roleIds = await db.Roles
            .Where(r => names.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var current = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        db.UserRoles.RemoveRange(current.Where(ur => !roleIds.Contains(ur.RoleId)));
        foreach (var roleId in roleIds.Where(id => current.All(ur => ur.RoleId != id)))
        {
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyRolePermissionsAsync(
        UserDbContext db, int roleId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken)
    {
        var codes = permissionCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = await db.Permissions.ToListAsync(cancellationToken).ConfigureAwait(false);
        var targetIds = permissions.Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToHashSet();

        var current = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        db.RolePermissions.RemoveRange(current.Where(rp => !targetIds.Contains(rp.PermissionId)));
        foreach (var permissionId in targetIds.Where(id => current.All(rp => rp.PermissionId != id)))
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
