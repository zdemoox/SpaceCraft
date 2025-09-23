using System.Net.Http.Json;
using SpaceCraft.Models;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.JSInterop;

namespace SpaceCraft.Services;

public class AuthService
{
    private const string TokenKey = "auth_token";

    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly ThemeService _themeService;

    public AuthService(HttpClient httpClient, IJSRuntime jsRuntime, ThemeService themeService)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _themeService = themeService;
    }

    public async Task EnsureAuthHeaderAsync()
    {
        try
        {
            if (_httpClient.DefaultRequestHeaders.Authorization != null)
                return;

            var token = await SecureStorage.GetAsync(TokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EnsureAuthHeaderAsync error: {ex.Message}");
        }
    }

    private static async Task SaveTokenAsync(string token)
    {
        try
        {
            await SecureStorage.SetAsync(TokenKey, token);
        }
        catch { }
    }

    public async Task<bool> TestConnection()
    {
        try
        {
            Debug.WriteLine("Testing API connection...");
            Debug.WriteLine($"Base URL: {_httpClient.BaseAddress}");
            Debug.WriteLine($"Full URL: {_httpClient.BaseAddress}api/auth/ping");
            
            var response = await _httpClient.GetAsync("api/auth/ping");
            Debug.WriteLine($"Ping response status: {response.StatusCode}");
            Debug.WriteLine($"Ping response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<dynamic>();
                Debug.WriteLine($"Ping response: {result}");
                return true;
            }
            var errorContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Ping error content: {errorContent}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ping error: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public async Task<(User? User, string? Token)> Register(string username, string password)
    {
        try
        {
            Debug.WriteLine($"Sending register request for user: {username}");
            Debug.WriteLine($"Full URL: {_httpClient.BaseAddress}api/auth/register");
            
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", new { Username = username, Password = password });
            Debug.WriteLine($"Register response status: {response.StatusCode}");
            Debug.WriteLine($"Register response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (result != null)
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.Token);
                    await SaveTokenAsync(result.Token);

                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "username", result.User.Username);

                    await _themeService.RefreshFromServerAsync();
                    return (result.User, result.Token);
                }
            }
            var errorContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Register error: {errorContent}");
            return (null, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Register error: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return (null, null);
        }
    }

    public async Task<(User? User, string? Token)> Login(string username, string password)
    {
        try
        {
            Debug.WriteLine($"Sending login request for user: {username}");
            Debug.WriteLine($"Full URL: {_httpClient.BaseAddress}api/auth/login");
            
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { Username = username, Password = password });
            Debug.WriteLine($"Login response status: {response.StatusCode}");
            Debug.WriteLine($"Login response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (result != null)
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.Token);
                    await SaveTokenAsync(result.Token);

                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "username", result.User.Username);

                    await _themeService.RefreshFromServerAsync();
                    return (result.User, result.Token);
                }
            }
            var errorContent = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Login error: {errorContent}");
            return (null, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Login error: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return (null, null);
        }
    }

    public async Task<bool> ResetPassword(string username, string newPassword)
    {
        try
        {
            Debug.WriteLine($"Sending reset password request for user: {username}");
            Debug.WriteLine($"Full URL: {_httpClient.BaseAddress}api/auth/reset-password");
            
            var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password", new { Username = username, NewPassword = newPassword });
            Debug.WriteLine($"Reset password response status: {response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Reset password error: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}

public record AuthResponse(string Token, User User); 