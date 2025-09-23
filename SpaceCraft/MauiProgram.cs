using Microsoft.Extensions.Logging;
using SpaceCraft.Services;
using Microsoft.JSInterop;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace SpaceCraft
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7005/") });
            builder.Services.AddScoped<NavigationHistoryService>();
            builder.Services.AddScoped<ThemeService>();
            builder.Services.AddScoped<AuthService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Services.AddSingleton<PinnedService>();
            builder.Services.AddScoped<KnowledgeService>(sp => new KnowledgeService(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IJSRuntime>()
            ));
            builder.Services.AddScoped<TaskService>(sp => new TaskService(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IJSRuntime>()
            ));
            builder.Services.AddScoped<TrashService>(sp => new TrashService(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IJSRuntime>()
            ));
            builder.Services.AddScoped<SettingsService>();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
