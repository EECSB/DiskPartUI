namespace DiskPartUI.Models;

///<summary>
///One row from <c>diskpart</c>'s <c>list volume</c> output.
///</summary>
public sealed class VolumeInfo
{
    public int Number { get; init; }
    public string Letter { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string FileSystem { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Info { get; init; } = string.Empty;

    public string Caption
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Letter))
                return $"Volume {Number}";
            else
                return $"Volume {Number}  ({Letter}:)";
        }
    }

    public string Summary
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Label))
                parts.Add(Label);

            if (!string.IsNullOrWhiteSpace(FileSystem))
                parts.Add(FileSystem);

            if (!string.IsNullOrWhiteSpace(Type))
                parts.Add(Type);

            if (!string.IsNullOrWhiteSpace(Size))
                parts.Add(Size);

            if (!string.IsNullOrWhiteSpace(Status))
                parts.Add(Status);

            if (!string.IsNullOrWhiteSpace(Info))
                parts.Add(Info);

            return string.Join("  •  ", parts);
        }
    }
}
