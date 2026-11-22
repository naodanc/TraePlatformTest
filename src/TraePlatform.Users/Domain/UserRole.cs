namespace TraePlatform.Users.Domain;

/// <summary>用户与角色的关联。</summary>
public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
