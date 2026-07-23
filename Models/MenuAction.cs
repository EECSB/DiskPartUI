namespace DiskPartUI.Models;

///<summary>
///One entry in a per-item action popup. <see cref="Category"/> ("Info", "Danger",
///or "Normal") drives the button color so the popup matches the Actions card.
///</summary>
public sealed class MenuAction
{
    public MenuAction(string label, string category, Func<Task> run)
    {
        Label = label;
        Category = category;
        Run = run;
    }

    public string Label { get; }
    public string Category { get; }
    public Func<Task> Run { get; }
}
