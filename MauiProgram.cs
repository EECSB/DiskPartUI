using DiskPartUI.Services;
using DiskPartUI.ViewModels;
using Microsoft.Extensions.Logging;

namespace DiskPartUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        //The UI is entirely Blazor (see wwwroot/app.css), so no MAUI fonts or styles are needed.
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();

        //Services
        builder.Services.AddSingleton<DiskPartService>();
        builder.Services.AddSingleton<DiskPartParser>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IFileDialogService, FileDialogService>();

        //View-model and host page
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
