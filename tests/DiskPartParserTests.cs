using DiskPartUI.Models;
using DiskPartUI.Services;

namespace DiskPartUI.Tests;

public class DiskPartParserTests
{
    private readonly DiskPartParser _parser = new();

    //Realistic `list disk` output as diskpart prints it under /s (banner + prompt + table).
    private const string ListDiskOutput =
        """

        Microsoft DiskPart version 10.0.26100.1

        Copyright (C) Microsoft Corporation.
        On computer: DESKTOP-ABC123

        DISKPART>

          Disk ###  Status         Size     Free     Dyn  Gpt
          --------  -------------  -------  -------  ---  ---
          Disk 0    Online          476 GB  1024 KB        *
          Disk 1    Online          931 GB      0 B
          Disk 2    No Media           0 B      0 B

        DISKPART>

        """;

    //`list volume` — note the empty Ltr / Label / Fs cells and the trailing Info column.
    private const string ListVolumeOutput =
        """

          Volume ###  Ltr  Label        Fs     Type        Size     Status     Info
          ----------  ---  -----------  -----  ----------  -------  ---------  --------
          Volume 0     C   Windows      NTFS   Partition    475 GB  Healthy    Boot
          Volume 1         Recovery     NTFS   Partition    529 MB  Healthy    Hidden
          Volume 2     D                       Removable       0 B  No Media

        """;

    private const string ListPartitionOutput =
        """

          Partition ###  Type              Size     Offset
          -------------  ----------------  -------  -------
          Partition 1    Recovery           529 MB  1024 KB
          Partition 2    System             100 MB   530 MB
          Partition 3    Reserved            16 MB   630 MB
          Partition 4    Primary            475 GB   646 MB

        """;

    //------------------------------------------------------------------- disks

    [Fact]
    public void ParseDisks_returns_every_row()
    {
        var disks = _parser.ParseDisks(ListDiskOutput);
        Assert.Equal(3, disks.Count);
    }

    [Fact]
    public void ParseDisks_reads_the_fields_of_a_row()
    {
        var disk = _parser.ParseDisks(ListDiskOutput)[0];

        Assert.Equal(0, disk.Number);
        Assert.Equal("Online", disk.Status);
        Assert.Equal("476 GB", disk.Size);
        Assert.Equal("1024 KB", disk.Free);
    }

    [Fact]
    public void ParseDisks_detects_gpt_from_the_star_column()
    {
        var disks = _parser.ParseDisks(ListDiskOutput);

        Assert.True(disks[0].IsGpt);
        Assert.False(disks[1].IsGpt);
    }

    [Fact]
    public void ParseDisks_keeps_a_multi_word_status()
    {
        var disk = _parser.ParseDisks(ListDiskOutput)[2];
        Assert.Equal("No Media", disk.Status);
    }

    //----------------------------------------------------------------- volumes

    [Fact]
    public void ParseVolumes_returns_every_row()
    {
        var volumes = _parser.ParseVolumes(ListVolumeOutput);
        Assert.Equal(3, volumes.Count);
    }

    [Fact]
    public void ParseVolumes_reads_a_lettered_volume()
    {
        var volume = _parser.ParseVolumes(ListVolumeOutput)[0];

        Assert.Equal(0, volume.Number);
        Assert.Equal("C", volume.Letter);
        Assert.Equal("Windows", volume.Label);
        Assert.Equal("NTFS", volume.FileSystem);
        Assert.Equal("Boot", volume.Info);
    }

    [Fact]
    public void ParseVolumes_leaves_a_missing_letter_empty()
    {
        var volume = _parser.ParseVolumes(ListVolumeOutput)[1];

        Assert.Equal("", volume.Letter);
        Assert.Equal("Recovery", volume.Label);
    }

    //-------------------------------------------------------------- partitions

    [Fact]
    public void ParsePartitions_returns_every_row()
    {
        var partitions = _parser.ParsePartitions(ListPartitionOutput);
        Assert.Equal(4, partitions.Count);
    }

    [Fact]
    public void ParsePartitions_reads_the_fields_of_a_row()
    {
        var partitions = _parser.ParsePartitions(ListPartitionOutput);

        Assert.Equal(1, partitions[0].Number);
        Assert.Equal("Recovery", partitions[0].Type);
        Assert.Equal("529 MB", partitions[0].Size);
        Assert.Equal("646 MB", partitions[3].Offset);
    }

    //----------------------------------------------------------------- corners

    [Fact]
    public void ParseDisks_of_empty_output_is_empty()
    {
        Assert.Empty(_parser.ParseDisks(string.Empty));
    }

    [Fact]
    public void ParsePartitions_of_a_no_table_message_is_empty()
    {
        var partitions = _parser.ParsePartitions("There are no partitions on this disk to show.");
        Assert.Empty(partitions);
    }
}
