using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class SettingsService
{
    private readonly HttpClient _httpClient;

    public SettingsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(bool success, string message)> ChangeCredentialsAsync(string newUsername, string newPassword)
    {
        var req = new { NewUsername = newUsername, NewPassword = newPassword };
        var response = await _httpClient.PostAsJsonAsync("/api/settings/change-credentials", req);
        
        if (response.IsSuccessStatusCode)
            return (true, "");
        
        var error = await response.Content.ReadAsStringAsync();
        return (false, error);
    }
}