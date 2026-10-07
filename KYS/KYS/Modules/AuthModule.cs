using System.Data.Common;
using System.Security.Claims;
using Carter;
using KYS.Context;
using KYS.DTOS;
using KYS.Services;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.EntityFrameworkCore;

namespace KYS.Modules;

public sealed class AuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth").WithTags("Authentication");

        auth.MapPost("/login", async (
            LoginDTO request,
            ApplicationDbContext db,
            UserPasswordService passwords,
            ILogger<AuthModule> logger,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password)
                || request.UserName.Length > 256 || request.Password.Length > 1024)
                return Results.BadRequest(new { message = "Kullanıcı adı ve şifre giriniz." });

            try
            {
                var user = await db.User01.FirstOrDefaultAsync(
                    x => x.UserName == request.UserName.Trim() && x.IsActive && !x.IsDeleted,
                    cancellationToken);

                if (user is null || !passwords.Verify(user, request.Password, out var needsUpgrade))
                    return Results.Unauthorized();

                if (needsUpgrade)
                {
                    user.Password = passwords.Hash(user, request.Password);
                    await db.SaveChangesAsync(cancellationToken);
                }

                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName!),
                    new Claim("unvan", user.Unvan ?? string.Empty)
                }, BearerTokenDefaults.AuthenticationScheme);

                return Results.SignIn(new ClaimsPrincipal(identity),
                    authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
            }
            catch (DbException exception)
            {
                logger.LogError(exception, "Giriş sırasında kullanıcı veritabanına erişilemedi.");
                return Results.Problem("Kullanıcı veritabanına şu anda erişilemiyor.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowAnonymous().RequireRateLimiting("login")
            .Produces<AccessTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        auth.MapGet("/me", async (ClaimsPrincipal principal, ApplicationDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                return Results.Unauthorized();

            var user = await db.User01.AsNoTracking().FirstOrDefaultAsync(
                x => x.Id == id && x.IsActive && !x.IsDeleted, cancellationToken);

            return user is null
                ? Results.Unauthorized()
                : Results.Ok(new { user.Id, user.UserName, user.Unvan });
        }).RequireAuthorization();
    }
}
