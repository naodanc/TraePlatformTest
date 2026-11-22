using TraePlatform.Users.Domain;

namespace TraePlatform.Users.Authorization;

/// <summary>权限校验服务。</summary>
public interface IAuthorizationService
{
    /// <summary>获取用户权限快照（含角色与资源动作集合）。</summary>
    Task<UserCapabilities> GetCapabilitiesAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>判断用户是否拥有某资源的指定动作权限。</summary>
    Task<bool> HasPermissionAsync(
        int userId, string resource, PermissionAction action, CancellationToken cancellationToken = default);
}
