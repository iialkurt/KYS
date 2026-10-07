using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using KYS.Context;
using KYS.Models;
using KYS.Modules;
using KYS.Services;
using KYS.Web.Models;
using KYS.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
Environment.SetEnvironmentVariable("TEST_CONTENTROOT_KYS", Path.Combine(root, "KYS/KYS"));
Environment.SetEnvironmentVariable("TEST_CONTENTROOT_KYS_WEB", Path.Combine(root, "KYS.Web"));

await using var apiFactory = new ApiFactory();
using var api = apiFactory.CreateClient(new WebApplicationFactoryClientOptions
{
    BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
});

using (var scope = apiFactory.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService>();
    var hashed = new User01 { UserName = "hashed", IsActive = true, Unvan = "Kalite Sorumlusu" };
    hashed.Password = passwords.Hash(hashed, "Test-password-123!");
    db.User01.AddRange(hashed,
        new User01 { UserName = "legacy", Password = "Legacy-password-123!", IsActive = true },
        new User01 { UserName = "inactive", Password = "Test-password-123!", IsActive = false },
        new User01 { UserName = "deleted", Password = "Test-password-123!", IsActive = true, IsDeleted = true });
    await db.SaveChangesAsync();
}

Check((await api.GetAsync("/auth/me")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous API request rejected");
Check((await ApiLogin("hashed", "wrong")).StatusCode == HttpStatusCode.Unauthorized, "Wrong password rejected");
Check((await ApiLogin("inactive", "Test-password-123!")).StatusCode == HttpStatusCode.Unauthorized, "Inactive user rejected");
Check((await ApiLogin("deleted", "Test-password-123!")).StatusCode == HttpStatusCode.Unauthorized, "Deleted user rejected");
Check((await ApiLogin("", "")).StatusCode == HttpStatusCode.BadRequest, "Empty credentials rejected");

using var hashedLogin = await ApiLogin("hashed", "Test-password-123!");
Check(hashedLogin.IsSuccessStatusCode, "Hashed password accepted");
var token = await hashedLogin.Content.ReadFromJsonAsync<TokenResponse>();
using var current = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
current.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
using var profile = await api.SendAsync(current);
var user = await profile.Content.ReadFromJsonAsync<CurrentUser>();
Check(profile.IsSuccessStatusCode && user?.UserName == "hashed" && user.Unvan == "Kalite Sorumlusu", "Bearer token retrieves current user");

using var legacyLogin = await ApiLogin("legacy", "Legacy-password-123!");
Check(legacyLogin.IsSuccessStatusCode, "Existing plaintext password accepted");
using (var scope = apiFactory.Services.CreateScope())
{
    var stored = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().User01.SingleAsync(x => x.UserName == "legacy");
    Check(stored.Password!.StartsWith("KYS$"), "Existing password upgraded to a hash");
}

var usersJson = await api.GetStringAsync("/users");
Check(!usersJson.Contains("password", StringComparison.OrdinalIgnoreCase), "User list does not expose passwords");

await using var webFactory = new WebApplicationFactory<LoginRequest>().WithWebHostBuilder(builder =>
{
    builder.UseEnvironment("Development");
    builder.ConfigureLogging(logging => logging.ClearProviders());
    builder.ConfigureServices(services => services.AddHttpClient<ApiClient>()
        .ConfigurePrimaryHttpMessageHandler(() => apiFactory.Server.CreateHandler()));
});
using var web = webFactory.CreateClient(new WebApplicationFactoryClientOptions
{
    BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
});

using var anonymousHome = await web.GetAsync("/");
Check(anonymousHome.StatusCode == HttpStatusCode.Redirect && anonymousHome.Headers.Location!.OriginalString.Contains("/login"), "Anonymous home redirects to login");

using var forged = await web.PostAsync("/auth/login", Form("hashed", "Test-password-123!"));
Check(forged.StatusCode == HttpStatusCode.BadRequest, "Login rejects missing antiforgery token");

var antiforgery = await ReadAntiforgery(web, "/login");
using var invalid = await web.PostAsync("/auth/login", Form("hashed", "wrong", antiforgery));
Check(invalid.StatusCode == HttpStatusCode.Redirect && invalid.Headers.Location!.OriginalString.Contains("error=invalid"), "Wrong login returns user-facing error");

antiforgery = await ReadAntiforgery(web, "/login");
using var signIn = await web.PostAsync("/auth/login", Form("hashed", "Test-password-123!", antiforgery,
    remember: true, returnUrl: "//external.example"));
Check(signIn.StatusCode == HttpStatusCode.Redirect && signIn.Headers.Location!.OriginalString == "/", "Successful login redirects locally");
var cookie = string.Join(";", signIn.Headers.GetValues("Set-Cookie"));
Check(cookie.Contains("KYS.Web.Session") && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase)
    && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase), "Remember me issues a persistent HttpOnly cookie");
var dashboard = await web.GetStringAsync("/");
Check(WebUtility.HtmlDecode(dashboard).Contains("Hoş geldiniz, hashed"), "Authenticated home displays signed-in user");

using var forgedLogout = await web.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["returnUrl"] = "/login" }));
Check(forgedLogout.StatusCode == HttpStatusCode.BadRequest, "Logout rejects missing antiforgery token");
antiforgery = await ReadAntiforgery(web, "/");
using var signOut = await web.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["returnUrl"] = "/login", ["__RequestVerificationToken"] = antiforgery
}));
Check(signOut.StatusCode == HttpStatusCode.Redirect && signOut.Headers.Location!.OriginalString == "/login", "Logout redirects to login");
Check((await web.GetAsync("/")).StatusCode == HttpStatusCode.Redirect, "Logout clears authenticated session");

Console.WriteLine("All authentication checks passed. Application databases were not accessed.");

Task<HttpResponseMessage> ApiLogin(string name, string password) => api.PostAsJsonAsync("/auth/login", new { UserName = name, Password = password });

static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
}

static FormUrlEncodedContent Form(string name, string password, string? antiforgery = null,
    bool remember = false, string returnUrl = "/")
{
    var fields = new Dictionary<string, string>
    {
        ["UserName"] = name, ["Password"] = password, ["RememberMe"] = remember ? "true" : "false", ["ReturnUrl"] = returnUrl
    };
    if (antiforgery is not null) fields["__RequestVerificationToken"] = antiforgery;
    return new FormUrlEncodedContent(fields);
}

static async Task<string> ReadAntiforgery(HttpClient client, string path)
{
    var html = await client.GetStringAsync(path);
    var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
    if (!match.Success) throw new InvalidOperationException("Antiforgery token not found.");
    return WebUtility.HtmlDecode(match.Groups[1].Value);
}

sealed class ApiFactory : WebApplicationFactory<AuthModule>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("KYS.AuthChecks"));
        });
    }
}
