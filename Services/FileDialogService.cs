using Windows.Storage;
using Windows.Storage.Pickers;

namespace DiskPartUI.Services;

///<summary>Open/Save dialogs for the diskpart script, using the native Windows file pickers.</summary>
public interface IFileDialogService
{
    Task<(string? Path, string? Content)> OpenTextAsync();

    ///<summary>Shows a Save dialog and writes <paramref name="content"/>. Returns the saved path, or null if canceled.</summary>
    Task<string?> SaveTextAsync(string suggestedFileName, string content);
}

///<inheritdoc />
public sealed class FileDialogService : IFileDialogService
{
    public async Task<(string? Path, string? Content)> OpenTextAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
        };
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".dp");
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return (null, null);

        var content = await FileIO.ReadTextAsync(file);
        return (file.Path, content);
    }

    public async Task<string?> SaveTextAsync(string suggestedFileName, string content)
    {
        var name = suggestedFileName;
        if (string.IsNullOrWhiteSpace(name))
            name = "diskpart-script";

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = name,
            DefaultFileExtension = ".txt",
        };
        picker.FileTypeChoices.Add("DiskPart script", new List<string> { ".txt", ".dp" });
        InitializeWithWindow(picker);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return null;

        await FileIO.WriteTextAsync(file, content ?? string.Empty);
        return file.Path;
    }

    //Unpackaged WinUI apps must associate a picker with the window's HWND before showing it.
    private static void InitializeWithWindow(object picker)
    {
        var mauiWindow = Application.Current?.Windows.FirstOrDefault()
            ?? throw new InvalidOperationException("No application window is available for the file dialog.");
        var nativeWindow = (Microsoft.UI.Xaml.Window?)mauiWindow.Handler?.PlatformView
            ?? throw new InvalidOperationException("The native window handle could not be resolved.");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}
