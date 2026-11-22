namespace TraePlatform.Users.Domain;

/// <summary>
/// 权限动作类型。与页面/控件的管控语义一一对应：
/// View → 可见，Operate → 可操作（可用/可点击），Edit → 可编辑，Manage → 可管理（增删/授权）。
/// </summary>
public enum PermissionAction
{
    /// <summary>可见。</summary>
    View = 0,

    /// <summary>可操作。</summary>
    Operate = 1,

    /// <summary>可编辑。</summary>
    Edit = 2,

    /// <summary>可管理。</summary>
    Manage = 3
}
