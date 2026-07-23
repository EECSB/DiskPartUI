namespace DiskPartUI.Models;

///<summary>
///One row from <c>diskpart</c>'s <c>list disk</c> output.
///</summary>
public sealed class DiskInfo
{
    public int Number { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Free { get; init; } = string.Empty;
    public bool IsDynamic { get; init; }
    public bool IsGpt { get; init; }

    public string Caption => $"Disk {Number}";

    public string PartitionStyle
    {
        get
        {
            if (IsGpt)
                return "GPT";
            else
                return "MBR";
        }
    }

    public string Summary
    {
        get
        {
            var parts = new List<string> { Status, Size };

            if (!string.IsNullOrWhiteSpace(Free) && Free != "0 B")
                parts.Add($"{Free} free");

            parts.Add(PartitionStyle);

            if (IsDynamic)
                parts.Add("Dynamic");

            return string.Join("  •  ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }
}
