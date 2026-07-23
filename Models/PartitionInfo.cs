namespace DiskPartUI.Models;

///<summary>
///One row from <c>diskpart</c>'s <c>list partition</c> output (for the selected disk).
///</summary>
public sealed class PartitionInfo
{
    public int Number { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Offset { get; init; } = string.Empty;

    public string Caption => $"Partition {Number}";

    public string Summary
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Type))
                parts.Add(Type);

            if (!string.IsNullOrWhiteSpace(Size))
                parts.Add(Size);

            if (!string.IsNullOrWhiteSpace(Offset))
                parts.Add($"offset {Offset}");

            return string.Join("  •  ", parts);
        }
    }
}
