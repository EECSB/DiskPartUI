namespace DiskPartUI.Services;

///<summary>
///Small abstraction over page dialogs so the view-model can ask for confirmation
///and input without holding a reference to a <see cref="Page"/>.
///</summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel");

    Task<string?> PromptAsync(string title, string message, string initialValue = "",
        string accept = "OK", string cancel = "Cancel", Keyboard? keyboard = null, int maxLength = -1);

    Task AlertAsync(string title, string message, string cancel = "OK");
}

///<inheritdoc />
public sealed class DialogService : IDialogService
{
    private static Page? CurrentPage => Application.Current?.Windows.FirstOrDefault()?.Page;

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel")
    {
        if (CurrentPage is { } page)
            return page.DisplayAlertAsync(title, message, accept, cancel);
        else
            return Task.FromResult(false);
    }

    public Task<string?> PromptAsync(string title, string message, string initialValue = "",
        string accept = "OK", string cancel = "Cancel", Keyboard? keyboard = null, int maxLength = -1)
    {
        if (CurrentPage is not { } page)
            return Task.FromResult<string?>(null);

        return page.DisplayPromptAsync(
            title,
            message,
            accept,
            cancel,
            placeholder: null,
            maxLength: maxLength,
            keyboard: keyboard ?? Keyboard.Default,
            initialValue: initialValue);
    }

    public Task AlertAsync(string title, string message, string cancel = "OK")
    {
        if (CurrentPage is { } page)
            return page.DisplayAlertAsync(title, message, cancel);
        else
            return Task.CompletedTask;
    }
}
