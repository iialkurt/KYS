using KYS.Abstractions;

namespace KYS.Models;

public sealed class User01:Entity
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? Unvan { get; set; }
    public bool IsActive { get; set; }

}
