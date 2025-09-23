using Microsoft.AspNetCore.Components;
using SpaceCraft.Models;
using System.Net.Http.Json;

namespace SpaceCraft.Services
{
    public class NavigationHistoryService
    {
        private readonly HttpClient _http;
        private readonly NavigationManager _navigationManager;

        public event Action? OnStackChanged;

        public NavigationHistoryService(HttpClient http, NavigationManager navigationManager)
        {
            _http = http;
            _navigationManager = navigationManager;
        }

        public async Task PushAsync(string url)
        {
            try
            {
                var stack = await GetStackAsync();
                if (stack.Any() && stack.Last().Url == url)
                {
                    Console.WriteLine($"NavigationHistoryService: URL {url} already on top of stack, skipping");
                    return;
                }
                
                await _http.PostAsJsonAsync("api/navigation-stack", url);
                OnStackChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pushing to navigation stack: {ex.Message}");
            }
        }

        public async Task<string> PopAsync()
        {
            try
            {
                var response = await _http.DeleteAsync("api/navigation-stack");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    OnStackChanged?.Invoke();
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error popping from navigation stack: {ex.Message}");
            }
            return null;
        }

        public async Task<List<NavigationStackItem>> GetStackAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<List<NavigationStackItem>>("api/navigation-stack");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting navigation stack: {ex.Message}");
                return new List<NavigationStackItem>();
            }
        }

        public async Task<bool> HasItemsAsync()
        {
            try
            {
                var stack = await GetStackAsync();
                return stack != null && stack.Any();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking stack items: {ex.Message}");
                return false;
            }
        }

        public async Task ClearStackAsync()
        {
            try
            {
                await _http.DeleteAsync("api/navigation-stack/clear");
                OnStackChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing navigation stack: {ex.Message}");
            }
        }
    }
}