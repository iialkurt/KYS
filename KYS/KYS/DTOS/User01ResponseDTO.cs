namespace KYS.DTOS;

public sealed record User01ResponseDTO(
    Guid Id,
    string? UserName,
    string? Unvan,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    DateTimeOffset DeletedAt);
