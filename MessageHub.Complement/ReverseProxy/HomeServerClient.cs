using System.Net.Http.Headers;
using MessageHub.ClientServer.Protocol;
using Microsoft.AspNetCore.WebUtilities;

namespace MessageHub.Complement.ReverseProxy;

public class HomeServerClient
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Config config;

    public HomeServerClient(IHttpClientFactory httpClientFactory, Config config)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(config);

        this.httpClientFactory = httpClientFactory;
        this.config = config;
    }

    public async Task<LogInResponse> LogInAsync(
        string serverAddress,
        string? deviceId = null,
        string? deviceName = null)
    {
        using var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri($"http://{serverAddress}");
        string url = "_matrix/client/v3/login/sso/redirect";
        url = QueryHelpers.AddQueryString(
            url,
            "redirectUrl",
            new Uri(new Uri(config.SelfUrl), "_matrix/complement/loginToken").ToString());
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        string loginToken = await response.Content.ReadAsStringAsync();
        response = await client.PostAsJsonAsync("_matrix/client/v3/login", new LogInRequest
        {
            LogInType = LogInTypes.Token,
            Token = loginToken,
            DeviceId = deviceId,
            InitialDeviceDisplayName = deviceName
        });
        response.EnsureSuccessStatusCode();
        var loginResponse = await response.Content.ReadFromJsonAsync<LogInResponse>();
        return loginResponse!;
    }

    public async Task LogOutAsync(string serverAddress, string accessToken)
    {
        using var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri($"http://{serverAddress}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.PostAsync("_matrix/client/v3/logout", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<WhoAmIResponse?> WhoAmIAsync(string serverAddress, string accessToken)
    {
        using var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri($"http://{serverAddress}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync("_matrix/client/v3/account/whoami");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<WhoAmIResponse>();
        }
        return null;
    }
}
