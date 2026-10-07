namespace KYS.DTOS;

public record User01CreateDTO(
    string UserName,
    string Password,
    string Unvan,
    bool IsActive
    );

