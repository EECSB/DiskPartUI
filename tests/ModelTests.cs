using DiskPartUI.Models;

namespace DiskPartUI.Tests;

public class ModelTests
{
    [Fact]
    public void DiskInfo_Summary_lists_status_size_free_and_style()
    {
        var disk = new DiskInfo
        {
            Number = 0,
            Status = "Online",
            Size = "476 GB",
            Free = "1024 KB",
            IsGpt = true,
        };

        Assert.Equal("Online  •  476 GB  •  1024 KB free  •  GPT", disk.Summary);
    }

    [Fact]
    public void DiskInfo_Summary_omits_zero_free_space()
    {
        var disk = new DiskInfo
        {
            Number = 1,
            Status = "Online",
            Size = "931 GB",
            Free = "0 B",
            IsGpt = false,
        };

        Assert.Equal("Online  •  931 GB  •  MBR", disk.Summary);
    }

    [Fact]
    public void DiskInfo_PartitionStyle_reflects_gpt_flag()
    {
        Assert.Equal("GPT", new DiskInfo { IsGpt = true }.PartitionStyle);
        Assert.Equal("MBR", new DiskInfo { IsGpt = false }.PartitionStyle);
    }

    [Fact]
    public void DiskInfo_Summary_appends_dynamic_when_flagged()
    {
        var disk = new DiskInfo
        {
            Number = 2,
            Status = "Online",
            Size = "931 GB",
            Free = "0 B",
            IsDynamic = true,
        };

        Assert.Equal("Online  •  931 GB  •  MBR  •  Dynamic", disk.Summary);
    }

    [Fact]
    public void VolumeInfo_Summary_is_empty_when_no_fields_are_set()
    {
        Assert.Equal(string.Empty, new VolumeInfo { Number = 3 }.Summary);
    }

    [Fact]
    public void PartitionInfo_Summary_omits_a_missing_offset()
    {
        var partition = new PartitionInfo
        {
            Number = 1,
            Type = "Primary",
            Size = "475 GB",
        };

        Assert.Equal("Primary  •  475 GB", partition.Summary);
    }

    [Fact]
    public void VolumeInfo_Caption_includes_the_drive_letter_when_present()
    {
        var volume = new VolumeInfo { Number = 0, Letter = "C" };
        Assert.Equal("Volume 0  (C:)", volume.Caption);
    }

    [Fact]
    public void VolumeInfo_Caption_drops_the_letter_when_absent()
    {
        var volume = new VolumeInfo { Number = 1, Letter = "" };
        Assert.Equal("Volume 1", volume.Caption);
    }

    [Fact]
    public void VolumeInfo_Summary_joins_only_the_non_empty_fields()
    {
        var volume = new VolumeInfo
        {
            Label = "Windows",
            FileSystem = "NTFS",
            Type = "Partition",
            Size = "475 GB",
            Status = "Healthy",
            Info = "Boot",
        };

        Assert.Equal("Windows  •  NTFS  •  Partition  •  475 GB  •  Healthy  •  Boot", volume.Summary);
    }

    [Fact]
    public void PartitionInfo_Caption_and_Summary_are_formatted()
    {
        var partition = new PartitionInfo
        {
            Number = 2,
            Type = "System",
            Size = "100 MB",
            Offset = "530 MB",
        };

        Assert.Equal("Partition 2", partition.Caption);
        Assert.Equal("System  •  100 MB  •  offset 530 MB", partition.Summary);
    }
}
