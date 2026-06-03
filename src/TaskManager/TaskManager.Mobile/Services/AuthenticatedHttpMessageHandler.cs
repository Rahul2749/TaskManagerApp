using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public class AuthenticatedHttpMessageHandler : DelegatingHandler
{
    private readonly ISecureTokenStorage _storage;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthenticatedHttpMessageHandler(ISecureTokenStorage storage, IHttpClientFactory httpClientFactory)
    {
        _storage = storage;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await ApplyTokenAsync(request);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        if (!await TryRefreshTokenAsync())
            return response;

        response.Dispose();
        var retry = await CloneRequestAsync(request);
        await ApplyTokenAsync(retry);
        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task ApplyTokenAsync(HttpRequestMessage request)
    {
        var token = await _storage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        var refreshToken = await _storage.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
        {
            await _storage.ClearAsync();
            return false;
        }

        var client = _httpClientFactory.CreateClient("TaskManagerAuth");
        var response = await client.PostAsJsonAsync("api/auth/refresh",
            new RefreshTokenDto { RefreshToken = refreshToken });

        if (!response.IsSuccessStatusCode)
        {
            await _storage.ClearAsync();
            return false;
        }

        var token = await response.Content.ReadFromJsonAsync<TokenDto>();
        if (token == null)
        {
            await _storage.ClearAsync();
            return false;
        }

        await _storage.SaveSessionAsync(token);
        return true;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content != null)
            clone.Content = await CloneContentAsync(request.Content);

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }

    private static async Task<HttpContent> CloneContentAsync(HttpContent content)
    {
        var bytes = await content.ReadAsByteArrayAsync();
        var clone = new ByteArrayContent(bytes);
        foreach (var header in content.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
