using Microsoft.UI.Xaml;

namespace DiskPartUI.WinUI;

///<summary>
///Provides application-specific behavior to supplement the default Application class.
///</summary>
public partial class App : MauiWinUIApplication
{
    ///<summary>
    ///Initializes the singleton application object. This is the first line of authored code
    ///executed, and as such is the logical equivalent of main() or WinMain().
    ///</summary>
    public App()
    {
        RedirectWebViewDataFolder();
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    ///<summary>
    ///WebView2 defaults its user-data folder to the directory that holds the executable. Once the
    ///app is installed under Program Files that folder is read-only, so the WebView fails to start
    ///with "We couldn't create the data directory". Point it at LocalAppData instead — this must
    ///run before any WebView is created, hence the constructor.
    ///</summary>
    private static void RedirectWebViewDataFolder()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
                return;

            var folder = Path.Combine(localAppData, "DiskPartUI", "WebView2");
            Directory.CreateDirectory(folder);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", folder);
        }
        catch { }
    }
}
