using System.Text.RegularExpressions;
using DiskPartUI.Models;

namespace DiskPartUI.Services;

///<summary>
///Turns the fixed-width text tables that diskpart prints (for <c>list disk</c>,
///<c>list volume</c>, <c>list partition</c>) into strongly-typed objects.
///
///The tables always look like:
///<code>
///  Disk ###  Status         Size     Free     Dyn  Gpt
///  --------  -------------  -------  -------  ---  ---
///  Disk 0    Online          931 GB      0 B        *
///</code>
///We locate the row of dashes, use the dash groups to find each column's start
///position, then slice every following data row at those positions. This is
///resilient to differing column widths across Windows versions.
///</summary>
public sealed class DiskPartParser
{
    public IReadOnlyList<DiskInfo> ParseDisks(string output)
    {
        var result = new List<DiskInfo>();

        foreach (var fields in ParseTable(output))
        {
            var number = ExtractInt(Field(fields, 0));
            if (number < 0)
                continue;

            result.Add(new DiskInfo
            {
                Number = number,
                Status = Field(fields, 1),
                Size = Field(fields, 2),
                Free = Field(fields, 3),
                IsDynamic = Field(fields, 4) == "*",
                IsGpt = Field(fields, 5) == "*",
            });
        }

        return result;
    }

    public IReadOnlyList<VolumeInfo> ParseVolumes(string output)
    {
        var result = new List<VolumeInfo>();

        foreach (var fields in ParseTable(output))
        {
            var number = ExtractInt(Field(fields, 0));
            if (number < 0)
                continue;

            result.Add(new VolumeInfo
            {
                Number = number,
                Letter = Field(fields, 1),
                Label = Field(fields, 2),
                FileSystem = Field(fields, 3),
                Type = Field(fields, 4),
                Size = Field(fields, 5),
                Status = Field(fields, 6),
                Info = Field(fields, 7),
            });
        }

        return result;
    }

    public IReadOnlyList<PartitionInfo> ParsePartitions(string output)
    {
        var result = new List<PartitionInfo>();

        foreach (var fields in ParseTable(output))
        {
            var number = ExtractInt(Field(fields, 0));
            if (number < 0)
                continue;

            result.Add(new PartitionInfo
            {
                Number = number,
                Type = Field(fields, 1),
                Size = Field(fields, 2),
                Offset = Field(fields, 3),
            });
        }

        return result;
    }

    private static string Field(string[] fields, int index)
    {
        if (index >= 0 && index < fields.Length)
            return fields[index];
        else
            return string.Empty;
    }

    private static int ExtractInt(string value)
    {
        var match = Regex.Match(value, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var n))
            return n;
        else
            return -1;
    }

    ///<summary>
    ///Splits the first fixed-width table found in <paramref name="output"/> into
    ///rows of trimmed field values. Returns an empty list when no table is present.
    ///</summary>
    private static List<string[]> ParseTable(string output)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(output))
            return rows;

        var lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        //Find the separator line: only dashes and spaces, and containing at least "--".
        var separatorIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length >= 2
                && trimmed.Contains("--")
                && trimmed.All(c => c is '-' or ' '))
            {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex < 0)
            return rows;

        //Column start positions = the start of each run of dashes.
        var starts = new List<int>();
        var separator = lines[separatorIndex];
        var inDashRun = false;
        for (var c = 0; c < separator.Length; c++)
        {
            if (separator[c] == '-')
            {
                if (!inDashRun)
                {
                    starts.Add(c);
                    inDashRun = true;
                }
            }
            else
            {
                inDashRun = false;
            }
        }

        if (starts.Count == 0)
            return rows;

        //Each data row runs until the next blank line.
        for (var i = separatorIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                break;

            var fields = new string[starts.Count];
            for (var k = 0; k < starts.Count; k++)
            {
                var start = starts[k];

                int end;
                if (k + 1 < starts.Count)
                    end = starts[k + 1];
                else
                    end = line.Length;

                fields[k] = Slice(line, start, end);
            }

            rows.Add(fields);
        }

        return rows;
    }

    private static string Slice(string line, int start, int end)
    {
        if (start >= line.Length)
            return string.Empty;

        if (end > line.Length)
            end = line.Length;

        if (end <= start)
            return string.Empty;

        return line[start..end].Trim();
    }
}
