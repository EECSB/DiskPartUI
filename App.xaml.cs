using Microsoft.Extensions.DependencyInjection;

namespace DiskPartUI;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        //Single-page app: use MainPage directly as the window content (no Shell),
        //so there is no Shell navigation/title bar above our own header.
        var page = _services.GetRequiredService<MainPage>();
        return new Window(page)
        {
            Title = "DiskPart UI",
            Width = 1280,
            Height = 1000,
        };
    }
}
