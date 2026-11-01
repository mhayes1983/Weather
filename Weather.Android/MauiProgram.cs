using Microsoft.Extensions.DependencyInjection;

namespace Weather.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var store = new SettingsStore();
        var host = new AppHost(store);

        // Startup can't await, and this only reads what's saved on the phone.
        Task.Run(host.LoadAsync).GetAwaiter().GetResult();

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(host);
        builder.Services.AddSingleton<AppBridge>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Services.AddHybridWebViewDeveloperTools();
#endif

        return builder.Build();
    }
}
