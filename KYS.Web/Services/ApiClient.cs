using System.Net;
using System.Net.Http.Headers;
using KYS.Web.Models;

namespace KYS.Web.Services;

public sealed class ApiClient(HttpClient http)
{
    public async Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("auth/login",
            new { UserName = request.UserName.Trim(), request.Password }, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUser>(cancellationToken);
    }
}
