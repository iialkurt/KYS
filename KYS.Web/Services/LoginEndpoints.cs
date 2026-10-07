using System.Net;
using System.Security.Claims;
using KYS.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace KYS.Web.Services;

public static class LoginEndpoints
{
    public static void MapLoginEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async ([FromForm] LoginRequest request,
            HttpContext context, ApiClient api, ILogger<ApiClient> logger) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password)
                || request.UserName.Length > 256 || request.Password.Length > 1024)
                return LoginError("required", request.ReturnUrl);

            try
            {
                var token = await api.LoginAsync(request, context.RequestAborted);
                if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresIn <= 0)
                    return LoginError("invalid", request.ReturnUrl);

                var user = await api.GetCurrentUserAsync(token.AccessToken, context.RequestAborted);
                if (user is null) return LoginError("invalid", request.ReturnUrl);

                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim("unvan", user.Unvan ?? string.Empty)
                }, CookieAuthenticationDefaults.AuthenticationScheme);

                var properties = new AuthenticationProperties
                {
                    IsPersistent = request.RememberMe,
                    AllowRefresh = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Min(token.ExpiresIn, 8 * 60 * 60))
                };
                properties.StoreTokens(new[]
                {
                    new AuthenticationToken { Name = "access_token", Value = token.AccessToken }
                });
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity), properties);

                return Results.LocalRedirect(SafeReturnUrl(request.ReturnUrl));
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning("Giriş API'sine erişilemedi. HTTP durum kodu: {StatusCode}", exception.StatusCode);
                return LoginError(exception.StatusCode == HttpStatusCode.TooManyRequests ? "rate" : "connection",
                    request.ReturnUrl);
            }
            catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
            {
                return LoginError("connection", request.ReturnUrl);
            }
        }).AllowAnonymous().RequireRateLimiting("login");

        app.MapPost("/auth/logout", async ([FromForm] string? returnUrl, HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/login");
        }).RequireAuthorization();
    }

    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//") && !returnUrl.Contains('\\')
        && !returnUrl.Any(char.IsControl) ? returnUrl : "/";

    private static IResult LoginError(string error, string? returnUrl) =>
        Results.LocalRedirect($"/login?error={error}&ReturnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}");
}
