using Microsoft.EntityFrameworkCore;
using TraePlatform.Users.Domain;
using TraePlatform.Users.Persistence;

namespace TraePlatform.Users.Authorization;

/// <summary>基于角色的权限校验实现：用户 → 角色 → 权限 → 资源动作集合。</summary>
public sealed class AuthorizationService : IAuthorizationService
{
    private readonly IDbContextFactory<UserDbContext> _contextFactory;

    public AuthorizationService(IDbContextFactory<UserDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<UserCapabilities> GetCapabilitiesAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var user = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return UserCapabilities.Empty;
        }

        var roles = user.UserRoles
            .Select(ur => ur.Role.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resources = new Dictionary<string, HashSet<PermissionAction>>(StringComparer.Ordinal);
        foreach (var rolePermission in user.UserRoles.SelectMany(ur => ur.Role.RolePermissions))
        {
            var permission = rolePermission.Permission;
            if (!resources.TryGetValue(permission.Resource, out var actions))
            {
                actions = new HashSet<PermissionAction>();
                resources[permission.Resource] = actions;
            }
            actions.Add(permission.Action);
        }

        return new UserCapabilities
        {
            UserId = user.Id,
            Roles = roles,
            Resources = resources.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlySet<PermissionAction>)pair.Value)
        };
    }

    public async Task<bool> HasPermissionAsync(
        int userId, string resource, PermissionAction action, CancellationToken cancellationToken = default)
    {
        var capabilities = await GetCapabilitiesAsync(userId, cancellationToken).ConfigureAwait(false);
        return capabilities.Can(resource, action);
    }
}
