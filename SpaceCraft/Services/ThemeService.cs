using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace SpaceCraft.Services
{
    public class ThemeService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private bool _isDarkTheme = false;

        public event Action<bool>? OnThemeChanged;

        public ThemeService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        public bool IsDarkTheme => _isDarkTheme;

        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("ThemeService: Initializing...");

                var loaded = await LoadThemeFromServer();
                if (!loaded)
                {
                    Console.WriteLine("ThemeService: Skipping fallback, waiting for authenticated theme load");
                }

                Console.WriteLine($"ThemeService: Final theme: {( _isDarkTheme ? "dark" : "light" )}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeService: Error during initialization: {ex.Message}");
            }
        }

        public Task<bool> RefreshFromServerAsync()
        {
            return LoadThemeFromServer();
        }

        private async Task<bool> LoadThemeFromServer()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/theme");
                if (response.IsSuccessStatusCode)
                {
                    var theme = await response.Content.ReadAsStringAsync();
                    theme = theme.Trim('"');
                    Console.WriteLine($"ThemeService: Loaded from server: {theme}");
                    await SetThemeAsync(theme);
                    return true;
                }

                Console.WriteLine($"ThemeService: Server returned status {response.StatusCode} while loading theme");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeService: Error loading from server: {ex.Message}");
            }
            return false;
        }

        public async Task ToggleThemeAsync()
        {
            var newTheme = _isDarkTheme ? "light" : "dark";
            Console.WriteLine($"ThemeService: Toggling theme to: {newTheme}");
            await SetThemeAsync(newTheme);
        }

        public async Task SetThemeAsync(string theme)
        {
            _isDarkTheme = theme == "dark";
            Console.WriteLine($"ThemeService: Setting theme to: {theme}");

            await ApplyThemeToDOM(theme);
            await SaveThemeToServer(theme);

            OnThemeChanged?.Invoke(_isDarkTheme);
        }

        private async Task SaveThemeToServer(string theme)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("api/theme", theme);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"ThemeService: Save to server failed with status {response.StatusCode}");
                }
                else
                {
                    Console.WriteLine($"ThemeService: Saved to server: {theme}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeService: Error saving to server: {ex.Message}");
            }
        }

        private async Task ApplyThemeToDOM(string theme)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", theme);
                Console.WriteLine($"ThemeService: Applied theme to DOM: {theme}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeService: Error applying theme to DOM: {ex.Message}");
            }
        }
    }
}  