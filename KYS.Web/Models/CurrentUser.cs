namespace KYS.Web.Models;

public sealed record CurrentUser(Guid Id, string UserName, string? Unvan);
