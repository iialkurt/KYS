namespace KYS.Web.Models;

public sealed record TokenResponse(string AccessToken, long ExpiresIn);
