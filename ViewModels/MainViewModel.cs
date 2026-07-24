using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskPartUI.Models;
using DiskPartUI.Services;

namespace DiskPartUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DiskPartService _diskpart;
    private readonly DiskPartParser _parser;
    private readonly IDialogService _dialog;
    private readonly IFileDialogService _fileDialog;

    private int _busyDepth;

    public MainViewModel(DiskPartService diskpart, DiskPartParser parser, IDialogService dialog, IFileDialogService fileDialog)
    {
        _diskpart = diskpart;
        _parser = parser;
        _dialog = dialog;
        _fileDialog = fileDialog;

        CommandScript = string.Empty;
        OutputLog = string.Empty;

        IsElevated = ElevationHelper.IsElevated();
        if (IsElevated)
            StatusText = "Ready";
        else
            StatusText = "Not running as Administrator — diskpart will fail. Relaunch the app elevated.";
    }

    public ObservableCollection<DiskInfo> Disks { get; } = new();
    public ObservableCollection<VolumeInfo> Volumes { get; } = new();
    public ObservableCollection<PartitionInfo> Partitions { get; } = new();

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsElevated { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string CommandScript { get; set; }

    [ObservableProperty]
    public partial string OutputLog { get; set; }

    [ObservableProperty]
    public partial string? CurrentScriptPath { get; set; }

    //Per-item action popup
    public ObservableCollection<MenuAction> MenuActions { get; } = new();

    [ObservableProperty]
    public partial bool IsActionMenuOpen { get; set; }

    [ObservableProperty]
    public partial string? MenuTitle { get; set; }

    [ObservableProperty]
    public partial DiskInfo? SelectedDisk { get; set; }

    [ObservableProperty]
    public partial VolumeInfo? SelectedVolume { get; set; }

    [ObservableProperty]
    public partial PartitionInfo? SelectedPartition { get; set; }

    //Selecting a disk automatically lists its partitions.
    partial void OnSelectedDiskChanged(DiskInfo? value)
    {
        Partitions.Clear();
        SelectedPartition = null;

        if (value is not null)
            _ = LoadPartitionsAsync(value);
    }

    //----------------------------------------------------------------- read-only

    [RelayCommand]
    private Task RefreshAllAsync()
    {
        return RunGuarded(async () =>
        {
            await RefreshDisksAsync();
            await RefreshVolumesAsync();

            if (SelectedDisk is not null)
                await LoadPartitionsCoreAsync(SelectedDisk);

            if (IsElevated)
                StatusText = $"Refreshed at {DateTime.Now:HH:mm:ss}";
        });
    }

    [RelayCommand]
    private Task DetailDiskAsync()
    {
        return WithSelectedDisk(async disk =>
        {
            var result = await _diskpart.RunCommandsAsync($"select disk {disk.Number}", "detail disk");
            ShowResult(result);
        });
    }

    [RelayCommand]
    private Task ListPartitionsAsync()
    {
        return WithSelectedDisk(async disk =>
        {
            var result = await _diskpart.RunCommandsAsync($"select disk {disk.Number}", "list partition");
            ReplacePartitions(result.Output);
            ShowResult(result);
        });
    }

    [RelayCommand]
    private Task DetailVolumeAsync()
    {
        return WithSelectedVolume(async volume =>
        {
            var result = await _diskpart.RunCommandsAsync($"select volume {volume.Number}", "detail volume");
            ShowResult(result);
        });
    }

    //------------------------------------------------------------- disk builders

    [RelayCommand]
    private Task AppendClean()
    {
        return WithSelectedDisk(disk =>
        {
            AppendToScript($"select disk {disk.Number}", "clean");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendCleanAll()
    {
        return WithSelectedDisk(disk =>
        {
            AppendToScript($"select disk {disk.Number}", "clean all");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendConvertGpt()
    {
        return WithSelectedDisk(disk =>
        {
            AppendToScript($"select disk {disk.Number}", "convert gpt");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendConvertMbr()
    {
        return WithSelectedDisk(disk =>
        {
            AppendToScript($"select disk {disk.Number}", "convert mbr");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendOnline()
    {
        return WithSelectedDisk(disk =>
        {
            AppendToScript($"select disk {disk.Number}", "online disk");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendOffline()
    {
        return WithSelectedDisk(disk =>
        {
            AppendToScript($"select disk {disk.Number}", "offline disk");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendCreatePrimaryAsync()
    {
        return WithSelectedDisk(async disk =>
        {
            var size = await _dialog.PromptAsync(
                "Create primary partition",
                "Size in MB (leave blank to use all free space):",
                keyboard: Keyboard.Numeric);
            if (size is null)
                return;//canceled

            var sizeArg = string.Empty;
            if (!string.IsNullOrWhiteSpace(size))
                sizeArg = $" size={size.Trim()}";

            AppendToScript($"select disk {disk.Number}", $"create partition primary{sizeArg}");
        });
    }

    //----------------------------------------------------------- volume builders

    [RelayCommand]
    private Task AppendFormatAsync()
    {
        return WithSelectedVolume(async volume =>
        {
            var initialFs = "ntfs";
            if (!string.IsNullOrWhiteSpace(volume.FileSystem))
                initialFs = volume.FileSystem.ToLowerInvariant();

            var fs = await _dialog.PromptAsync(
                "Format volume",
                $"File system for {volume.Caption} (ntfs, fat32, exfat):",
                initialValue: initialFs);
            if (string.IsNullOrWhiteSpace(fs))
                return;

            var label = await _dialog.PromptAsync(
                "Volume label",
                "New volume label (optional):",
                initialValue: volume.Label);

            var labelArg = string.Empty;
            if (!string.IsNullOrWhiteSpace(label))
                labelArg = $" label=\"{label.Trim()}\"";

            AppendToScript($"select volume {volume.Number}", $"format fs={fs.Trim()}{labelArg} quick");
        });
    }

    [RelayCommand]
    private Task AppendAssignLetterAsync()
    {
        return WithSelectedVolume(async volume =>
        {
            var letter = await _dialog.PromptAsync("Assign drive letter", "Drive letter (A–Z):", maxLength: 2);
            if (string.IsNullOrWhiteSpace(letter))
                return;

            var normalized = letter.Trim().TrimEnd(':').ToUpperInvariant();
            AppendToScript($"select volume {volume.Number}", $"assign letter={normalized}");
        });
    }

    [RelayCommand]
    private Task AppendRemoveLetter()
    {
        return WithSelectedVolume(volume =>
        {
            AppendToScript($"select volume {volume.Number}", "remove");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendExtendAsync()
    {
        return WithSelectedVolume(async volume =>
        {
            var size = await _dialog.PromptAsync(
                "Extend volume",
                "Extra size in MB (leave blank to use all contiguous free space):",
                keyboard: Keyboard.Numeric);
            if (size is null)
                return;//canceled

            var sizeArg = string.Empty;
            if (!string.IsNullOrWhiteSpace(size))
                sizeArg = $" size={size.Trim()}";

            AppendToScript($"select volume {volume.Number}", $"extend{sizeArg}");
        });
    }

    [RelayCommand]
    private Task AppendShrinkAsync()
    {
        return WithSelectedVolume(async volume =>
        {
            var size = await _dialog.PromptAsync("Shrink volume", "Amount to shrink by, in MB:", keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(size))
                return;

            AppendToScript($"select volume {volume.Number}", $"shrink desired={size.Trim()}");
        });
    }

    [RelayCommand]
    private Task AppendDeleteVolume()
    {
        return WithSelectedVolume(volume =>
        {
            AppendToScript($"select volume {volume.Number}", "delete volume");
            return Task.CompletedTask;
        });
    }

    //-------------------------------------------------------- partition builders

    [RelayCommand]
    private Task AppendSetActive()
    {
        return WithSelectedPartition((disk, partition) =>
        {
            AppendToScript($"select disk {disk.Number}", $"select partition {partition.Number}", "active");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendSetInactive()
    {
        return WithSelectedPartition((disk, partition) =>
        {
            AppendToScript($"select disk {disk.Number}", $"select partition {partition.Number}", "inactive");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private Task AppendDeletePartition()
    {
        return WithSelectedPartition((disk, partition) =>
        {
            AppendToScript($"select disk {disk.Number}", $"select partition {partition.Number}", "delete partition");
            return Task.CompletedTask;
        });
    }

    //------------------------------------------------------------------- wizards

    [RelayCommand]
    private Task MakeBootableUsbAsync()
    {
        return WithSelectedDisk(async disk =>
        {
            var proceed = await _dialog.ConfirmAsync(
                "Create bootable USB",
                $@"This builds a script that ERASES ALL DATA on {disk.Caption} ({disk.Size}) and sets it up as a bootable drive.

Continue building the script?",
                "Build script",
                "Cancel");
            if (!proceed)
                return;

            var fs = await _dialog.PromptAsync(
                "File system",
                "File system (fat32 boots on UEFI; ntfs allows files > 4 GB):",
                initialValue: "fat32");
            if (string.IsNullOrWhiteSpace(fs))
                return;

            var label = await _dialog.PromptAsync("Volume label", "Volume label (optional):", initialValue: "BOOT");

            var formatLine = $"format fs={fs.Trim()} quick";
            if (!string.IsNullOrWhiteSpace(label))
                formatLine = $"format fs={fs.Trim()} label=\"{label.Trim()}\" quick";

            AppendToScript(
                $"select disk {disk.Number}",
                "clean",
                "convert mbr",
                "create partition primary",
                "select partition 1",
                "active",
                formatLine,
                "assign");

            await _dialog.AlertAsync(
                "Script ready",
                "The bootable-USB script was added to the command box. Review it and press Run to execute.");
        });
    }

    //--------------------------------------------------------------- run / clear

    [RelayCommand]
    private Task RunScriptAsync()
    {
        return RunGuarded(async () =>
        {
            var script = (CommandScript ?? string.Empty).Trim();
            if (script.Length == 0)
            {
                await _dialog.AlertAsync(
                    "Nothing to run",
                    "The command box is empty. Add actions with the buttons, or type diskpart commands directly.");
                return;
            }

            if (!IsElevated)
            {
                var runAnyway = await _dialog.ConfirmAsync(
                    "Not elevated",
                    "This app is not running as Administrator, so diskpart will fail. Run anyway?",
                    "Run anyway",
                    "Cancel");
                if (!runAnyway)
                    return;
            }

            var confirmed = await _dialog.ConfirmAsync(
                "Run diskpart script?",
                $@"These commands will be sent to diskpart:

{script}

⚠ clean, delete, and format PERMANENTLY erase data. Double-check the selected disk.",
                "Run",
                "Cancel");
            if (!confirmed)
                return;

            AppendOutput($"===== {DateTime.Now:HH:mm:ss}  running script =====");
            AppendOutput(script);

            var result = await _diskpart.RunScriptAsync(script);
            ShowResult(result);

            if (result.Success)
                AppendOutput("===== completed =====\n");
            else
                AppendOutput("===== completed with errors =====\n");

            //Reflect any changes the script made.
            await RefreshDisksAsync();
            await RefreshVolumesAsync();

            if (SelectedDisk is not null)
                await LoadPartitionsCoreAsync(SelectedDisk);
        });
    }

    [RelayCommand]
    private void ClearScript()
    {
        CommandScript = string.Empty;
        StatusText = "Command box cleared";
    }

    [RelayCommand]
    private void ClearOutput()
    {
        OutputLog = string.Empty;
    }

    [RelayCommand]
    private async Task OpenScriptAsync()
    {
        try
        {
            var (path, content) = await _fileDialog.OpenTextAsync();
            if (content is null)
                return;//canceled

            CommandScript = content;
            CurrentScriptPath = path;

            if (path is null)
                StatusText = "Opened script";
            else
                StatusText = $"Opened {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            await _dialog.AlertAsync("Couldn't open file", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveScriptAsync()
    {
        try
        {
            var suggested = "diskpart-script.txt";
            if (!string.IsNullOrWhiteSpace(CurrentScriptPath))
                suggested = Path.GetFileName(CurrentScriptPath);

            var savedPath = await _fileDialog.SaveTextAsync(suggested, CommandScript ?? string.Empty);
            if (savedPath is null)
                return;//canceled

            CurrentScriptPath = savedPath;
            StatusText = $"Saved {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            await _dialog.AlertAsync("Couldn't save file", ex.Message);
        }
    }

    //------------------------------------------------- per-item action menus

    [RelayCommand]
    private void ShowDiskActions(DiskInfo disk)
    {
        SelectedDisk = disk;
        MenuTitle = $"Actions — {disk.Caption}";
        MenuActions.Clear();
        MenuActions.Add(new MenuAction("Detail disk", "Info", DetailDiskAsync));
        MenuActions.Add(new MenuAction("List partitions", "Info", ListPartitionsAsync));
        MenuActions.Add(new MenuAction("Create primary partition", "Normal", AppendCreatePrimaryAsync));
        MenuActions.Add(new MenuAction("Convert to GPT", "Normal", AppendConvertGpt));
        MenuActions.Add(new MenuAction("Convert to MBR", "Normal", AppendConvertMbr));
        MenuActions.Add(new MenuAction("Bring online", "Normal", AppendOnline));
        MenuActions.Add(new MenuAction("Take offline", "Normal", AppendOffline));
        MenuActions.Add(new MenuAction("Clean", "Danger", AppendClean));
        MenuActions.Add(new MenuAction("Clean all", "Danger", AppendCleanAll));
        MenuActions.Add(new MenuAction("Make bootable USB", "Danger", MakeBootableUsbAsync));
        IsActionMenuOpen = true;
    }

    [RelayCommand]
    private void ShowVolumeActions(VolumeInfo volume)
    {
        SelectedVolume = volume;
        MenuTitle = $"Actions — {volume.Caption}";
        MenuActions.Clear();
        MenuActions.Add(new MenuAction("Detail volume", "Info", DetailVolumeAsync));
        MenuActions.Add(new MenuAction("Assign letter", "Normal", AppendAssignLetterAsync));
        MenuActions.Add(new MenuAction("Remove letter", "Normal", AppendRemoveLetter));
        MenuActions.Add(new MenuAction("Extend", "Normal", AppendExtendAsync));
        MenuActions.Add(new MenuAction("Shrink", "Normal", AppendShrinkAsync));
        MenuActions.Add(new MenuAction("Format", "Danger", AppendFormatAsync));
        MenuActions.Add(new MenuAction("Delete volume", "Danger", AppendDeleteVolume));
        IsActionMenuOpen = true;
    }

    [RelayCommand]
    private void ShowPartitionActions(PartitionInfo partition)
    {
        SelectedPartition = partition;
        MenuTitle = $"Actions — {partition.Caption}";
        MenuActions.Clear();
        MenuActions.Add(new MenuAction("Set active", "Normal", AppendSetActive));
        MenuActions.Add(new MenuAction("Set inactive", "Normal", AppendSetInactive));
        MenuActions.Add(new MenuAction("Delete partition", "Danger", AppendDeletePartition));
        IsActionMenuOpen = true;
    }

    [RelayCommand]
    private async Task InvokeMenuAction(MenuAction? action)
    {
        IsActionMenuOpen = false;

        if (action is not null)
            await action.Run();
    }

    [RelayCommand]
    private void CloseActionMenu()
    {
        IsActionMenuOpen = false;
    }

    //------------------------------------------------------------------- helpers

    private Task LoadPartitionsAsync(DiskInfo disk)
    {
        return RunGuarded(() => LoadPartitionsCoreAsync(disk));
    }

    private async Task LoadPartitionsCoreAsync(DiskInfo disk)
    {
        var result = await _diskpart.RunCommandsAsync($"select disk {disk.Number}", "list partition");
        ReplacePartitions(result.Output);
    }

    private async Task RefreshDisksAsync()
    {
        var previous = SelectedDisk?.Number;
        var result = await _diskpart.RunScriptAsync("list disk");
        var disks = _parser.ParseDisks(result.Output);

        Disks.Clear();
        foreach (var disk in disks)
            Disks.Add(disk);

        if (previous is int number)
            SelectedDisk = Disks.FirstOrDefault(d => d.Number == number);
    }

    private async Task RefreshVolumesAsync()
    {
        var previous = SelectedVolume?.Number;
        var result = await _diskpart.RunScriptAsync("list volume");
        var volumes = _parser.ParseVolumes(result.Output);

        Volumes.Clear();
        foreach (var volume in volumes)
            Volumes.Add(volume);

        if (previous is int number)
            SelectedVolume = Volumes.FirstOrDefault(v => v.Number == number);
    }

    private void ReplacePartitions(string output)
    {
        var partitions = _parser.ParsePartitions(output);
        var previous = SelectedPartition?.Number;

        Partitions.Clear();
        foreach (var partition in partitions)
            Partitions.Add(partition);

        if (previous is int number)
            SelectedPartition = Partitions.FirstOrDefault(p => p.Number == number);
    }

    private void AppendToScript(params string[] lines)
    {
        var block = string.Join(Environment.NewLine, lines.Where(l => !string.IsNullOrWhiteSpace(l)));

        if (string.IsNullOrWhiteSpace(CommandScript))
            CommandScript = block;
        else
            CommandScript = CommandScript.TrimEnd() + Environment.NewLine + block;

        StatusText = "Command added — review the script, then press Run";
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(OutputLog))
            OutputLog = text;
        else
            OutputLog = OutputLog + Environment.NewLine + text;
    }

    private void ShowResult(DiskPartResult result)
    {
        AppendOutput(result.Output);

        if (result.Success)
            StatusText = "diskpart finished";
        else
            StatusText = "diskpart reported an error";
    }

    private async Task WithSelectedDisk(Func<DiskInfo, Task> action)
    {
        if (SelectedDisk is null)
        {
            await _dialog.AlertAsync("No disk selected", "Select a disk in the Disks list first.");
            return;
        }

        await RunGuarded(() => action(SelectedDisk));
    }

    private async Task WithSelectedVolume(Func<VolumeInfo, Task> action)
    {
        if (SelectedVolume is null)
        {
            await _dialog.AlertAsync("No volume selected", "Select a volume in the Volumes list first.");
            return;
        }

        await RunGuarded(() => action(SelectedVolume));
    }

    private async Task WithSelectedPartition(Func<DiskInfo, PartitionInfo, Task> action)
    {
        if (SelectedDisk is null)
        {
            await _dialog.AlertAsync("No disk selected", "Select a disk first, then a partition.");
            return;
        }

        if (SelectedPartition is null)
        {
            await _dialog.AlertAsync(
                "No partition selected",
                "Select a partition first (use “List partitions” to populate the list).");
            return;
        }

        await RunGuarded(() => action(SelectedDisk, SelectedPartition));
    }

    private async Task RunGuarded(Func<Task> work)
    {
        EnterBusy();
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            AppendOutput($"[ERROR] {ex.Message}");
            StatusText = "Error — see output";
        }
        finally
        {
            ExitBusy();
        }
    }

    private void EnterBusy()
    {
        _busyDepth++;
        IsBusy = true;

        if (IsElevated)
            StatusText = "Running diskpart…";
    }

    private void ExitBusy()
    {
        _busyDepth--;

        if (_busyDepth <= 0)
        {
            _busyDepth = 0;
            IsBusy = false;
        }
    }
}
