namespace KYS.DTOS;

public record User01UpdateDTO(
    string UserName,
    string Password,
    string Unvan,
    bool IsActive
    );

