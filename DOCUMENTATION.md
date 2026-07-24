# DiskPart UI — Developer Documentation

This document describes how the app is put together, subsystem by subsystem. For the
user-facing overview see [README.md](README.md).

## Contents

- [Overview](#overview)
- [Runtime & bootstrap](#runtime--bootstrap)
- [diskpart execution layer](#diskpart-execution-layer)
- [Output parsing](#output-parsing)
- [Domain models](#domain-models)
- [The view-model](#the-view-model)
- [Dialogs & file pickers](#dialogs--file-pickers)
- [The view (Blazor UI)](#the-view-blazor-ui)
- [Elevation & the manifest](#elevation--the-manifest)
- [Testing](#testing)
- [Build & run](#build--run)
- [Packaging the release](#packaging-the-release)

## Overview

DiskPart UI is a single-window **.NET MAUI** desktop app that targets **Windows only**
(`net10.0-windows10.0.19041.0`) and runs **unpackaged** (`WindowsPackageType=None`). It is a
thin, transparent front-end over the `diskpart` command-line tool.

**Architecture** is textbook MVVM:

```
   BlazorWebView (WebView2)
     Main.razor  ──@inject──►  MainViewModel  ──►  DiskPartService ──► diskpart.exe
     (Razor UI)                (state + commands)    │  (CliWrap)
        ▲  ▲                                         ├─► DiskPartParser  (text → models)
        │  └─ PropertyChanged / CollectionChanged    ├─► IDialogService  (native MAUI dialogs)
        │     → StateHasChanged                       └─► IFileDialogService (WinRT open/save)
        └─ js/splitter.js  (drag-to-resize + localStorage)
```

The UI is **.NET MAUI Blazor Hybrid**: a native MAUI `ContentPage` (`MainPage`) hosts a
`BlazorWebView`, which renders the `Main` Razor component in an embedded WebView2. The app is
still a native Windows process, so diskpart execution, elevation, and the WinRT file pickers
all work exactly as in a XAML MAUI app.

Design principles:

- **Nothing runs behind your back.** Every mutating operation is turned into visible
  `diskpart` commands that the user reviews and confirms before execution.
- **The UI is a thin view over the view-model.** `Main.razor` holds no logic — it renders
  `MainViewModel` state and forwards clicks to its commands, re-rendering whenever the
  view-model raises `PropertyChanged` or a collection changes.
- **Services are injected.** Everything arrives through the MAUI DI container, which keeps
  `MainViewModel` unit-testable and free of view concerns.

Dependencies: **CliWrap** (process execution), **CommunityToolkit.Mvvm** (`ObservableObject`,
`[ObservableProperty]`, `[RelayCommand]`), and **Microsoft.AspNetCore.Components.WebView.Maui**
(the BlazorWebView). No other third-party packages.

## Runtime & bootstrap

- **`MauiProgram.CreateMauiApp`** calls `AddMauiBlazorWebView()` (plus
  `AddBlazorWebViewDeveloperTools()` in Debug) and registers the services and view-model in the
  DI container: `DiskPartService`, `DiskPartParser`, `IDialogService`, `IFileDialogService`, and
  `MainViewModel` as singletons; `MainPage` as transient.
- **`App`** receives the `IServiceProvider` and, in `CreateWindow`, resolves `MainPage` and
  returns it as the window's content (there is no Shell). The window title and initial size are
  set here.
- **`MainPage.xaml`** is a `ContentPage` whose only child is a `BlazorWebView` with
  `HostPage="wwwroot/index.html"` and a root component of `Main`.
- **`Main.razor`** `@inject`s the singleton `MainViewModel`, subscribes to its `PropertyChanged`
  and the collections' `CollectionChanged` (calling `StateHasChanged`), initializes the JS
  splitters, and fires `RefreshAllCommand` on first render to populate the lists.

## diskpart execution layer

`Services/DiskPartService.cs` is the only code that launches a process.

- **Script model.** `diskpart` is a batch tool: `RunScriptAsync(string script)` normalizes the
  script (trims each line, drops blanks, appends a trailing newline) and writes it to a
  temporary file, then runs `diskpart /s <file>` through CliWrap.
- **Encoding.** The temp file is written as **UTF-8 without a BOM** — a leading BOM can make
  `diskpart` reject the first command.
- **Output.** `StandardOutput` and `StandardError` are captured (`ExecuteBufferedAsync`) and
  combined; validation is disabled (`CommandResultValidation.None`) because `diskpart` signals
  problems through both exit codes *and* text, so everything is surfaced to the user rather
  than thrown.
- **Serialization.** A `SemaphoreSlim(1, 1)` guards execution so two `diskpart` instances never
  run concurrently.
- **Result.** Returns a `DiskPartResult(bool Success, string Output)`. The temp file is deleted
  best-effort in a `finally`.
- `RunCommandsAsync(params string[])` is a convenience overload that joins commands into a
  script.

## Output parsing

`Services/DiskPartParser.cs` converts `diskpart`'s fixed-width text tables into typed models.
`diskpart` prints tables like:

```
  Disk ###  Status         Size     Free     Dyn  Gpt
  --------  -------------  -------  -------  ---  ---
  Disk 0    Online          931 GB      0 B        *
```

The algorithm (`ParseTable`):

1. Find the **separator row** — the first line made only of dashes and spaces (and containing
   at least `--`).
2. Use the **runs of dashes** to record each column's start position.
3. Slice every following data row at those positions until the next blank line; each field is
   trimmed. The last column extends to end-of-line.

`ParseDisks` / `ParseVolumes` / `ParsePartitions` map the sliced fields to `DiskInfo`,
`VolumeInfo`, `PartitionInfo` by column index and skip rows without a numeric id. This
position-based slicing is resilient to the differing column widths seen across Windows
versions, and tolerates empty cells (a volume with no drive letter, `No Media`, `0 B`, etc.).

## Domain models

Plain immutable objects in `Models/` (`init`-only properties):

- **`DiskInfo`** — number, status, size, free, dynamic/GPT flags, plus computed `Caption`
  (`"Disk 0"`), `PartitionStyle` (`GPT`/`MBR`), and `Summary` (the `•`-joined one-liner shown
  in the list).
- **`VolumeInfo`** — number, letter, label, file system, type, size, status, info + `Caption`
  / `Summary`.
- **`PartitionInfo`** — number, type, size, offset + `Caption` / `Summary`.
- **`DiskPartResult`** — the record returned by `DiskPartService` (`Success` + `Output`).
- **`MenuAction`** — one entry in a per-item popup: `Label`, `Category` (`Info` / `Normal` /
  `Danger`, which drives the button color), and a `Func<Task> Run`.

## The view-model

`ViewModels/MainViewModel.cs` (a `partial ObservableObject`) holds all state and commands.

- **State.** Observable collections (`Disks`, `Volumes`, `Partitions`, `MenuActions`) and
  observable properties (`SelectedDisk/Volume/Partition`, `CommandScript`, `OutputLog`,
  `StatusText`, `IsBusy`, `IsElevated`, `IsActionMenuOpen`, `MenuTitle`, …).
- **Two kinds of command.** *Read-only* commands (`RefreshAll`, `DetailDisk`,
  `ListPartitions`, `DetailVolume`) run `diskpart` immediately and show the output.
  *Builder* commands (`AppendClean`, `AppendFormat`, `AppendDeleteVolume`, …) only append the
  matching commands — with the correct `select …` prefix — to `CommandScript` via
  `AppendToScript`. Nothing mutates a disk until **`RunScript`**.
- **The run gate.** `RunScriptAsync` confirms (showing the exact script and a destructive-data
  warning) before calling `DiskPartService`, then refreshes the lists to reflect any change.
- **Selection.** Setting `SelectedDisk` (`OnSelectedDiskChanged`) clears and reloads that
  disk's partitions automatically.
- **Guards & busy tracking.** `WithSelectedDisk/Volume/Partition` short-circuit with an alert
  when nothing is selected. `RunGuarded` wraps every `diskpart` interaction in try/catch and a
  re-entrant busy counter (`EnterBusy`/`ExitBusy`) that drives `IsBusy`.
- **Per-item menus.** `ShowDiskActions` / `ShowVolumeActions` / `ShowPartitionActions` set the
  selection, fill `MenuActions` with color-categorized entries, and open the popup;
  `InvokeMenuAction` closes it and runs the chosen `Func<Task>`.

## Dialogs & file pickers

- **`Services/DialogService.cs`** implements `IDialogService` (confirm / prompt / alert) by
  resolving the current window's `Page` at call time (`Application.Current.Windows[0].Page`),
  so the view-model never holds a `Page` reference.
- **`Services/FileDialogService.cs`** implements open/save with the **native WinRT pickers**
  (`FileOpenPicker` / `FileSavePicker`). Because the app is unpackaged, each picker is
  associated with the window's **HWND** via `WinRT.Interop.InitializeWithWindow` before it is
  shown.

## The view (Blazor UI)

`Components/Main.razor` is the whole UI — a two-column flex layout with a modal overlay for
the popup. It renders the injected `MainViewModel` and forwards clicks to its commands:

- **Header** — title + live status on the left; elevation badge, busy spinner, and the square
  **⟳** refresh button on the right.
- **Left column** — Disks / Partitions / Volumes cards, each an `@foreach` over the matching
  observable collection, with a per-row **Actions** button and a `selected` class on the
  current item.
- **Right column** — the Actions panel (grouped, color-coded buttons with divider rules), the
  **Command preview** (a `<textarea>` two-way bound to `CommandScript` + Open/Save/Run/Clear),
  and the **Output** console (`<pre>`).
- **Per-item popup** — rendered when `IsActionMenuOpen` is true: a dimmed overlay with a card
  whose buttons come from `MenuActions`; a CSS class derived from `Category` colors them
  (blue/gray/red) to match the Actions panel.

Commands are invoked via the generated `IAsyncRelayCommand` / `IRelayCommand` properties
(`Vm.AppendCleanCommand.ExecuteAsync(null)`, `Vm.ShowDiskActionsCommand.Execute(disk)`, …).
Because the component subscribes to the view-model's change events, any state the commands
mutate re-renders automatically. Confirm/prompt dialogs still surface as **native MAUI page
dialogs** (via `IDialogService`), shown over the WebView.

**Resizing & persistence** are handled in the browser, not C#:

- `wwwroot/app.css` lays out the columns and cards with flexbox; thin **splitter strips**
  (`.h-split` / `.v-split`) sit between the resizable panels.
- `wwwroot/js/splitter.js` attaches pointer handlers to each splitter, adjusts the previous
  element's `flex-basis` (clamped to a minimum), and on release saves the size to the WebView's
  **`localStorage`** keyed by a `data-splitkey`. `splitter.init` restores saved sizes on
  startup; the two columns default to a clean 50/50 until dragged.

## Elevation & the manifest

`diskpart` requires elevation, so `Platforms/Windows/app.manifest` requests
`requireAdministrator`. Running unpackaged is what lets that Win32 manifest take effect, so the
app self-elevates (one UAC prompt) at launch. `ElevationHelper.IsElevated()` reports the
current state for the header badge and the "not elevated" warnings.

## Testing

Unit tests live in [`tests/`](tests) as a separate **xUnit** project (`DiskPartUI.Tests`).
Rather than reference the Windows MAUI app (which would drag in the WinUI/RID/packaging
model), the test project targets plain **`net10.0`** and **compiles the files under test
directly** via linked `<Compile Include="..\…" />` items — they are pure logic with no MAUI
dependencies, so the suite is fast and portable. The app project excludes `tests/**` from its
own compile so the two never collide.

Covered:

- **`DiskPartParser`** — parsing real `list disk` / `list volume` / `list partition` output,
  including the tricky cases: empty drive-letter cells, a multi-word `No Media` status, `0 B`
  values, GPT-vs-MBR detection, CRLF as well as LF line endings, rows whose id is not numeric,
  and empty / no-table input.
- **Model formatting** — the `Caption` / `Summary` / `PartitionStyle` computed strings,
  including the omit-when-empty branches (zero free space, missing offset, no fields set).

```bash
dotnet test
```

The side-effecting layers — process launch, dialogs, file pickers, elevation, and the UI — are
integration concerns and are verified by running the app.

## Build & run

```bash
dotnet build -c Debug
```

```bash
dotnet run -c Debug -f net10.0-windows10.0.19041.0
```

The project multi-targets nothing else — it is Windows-only because `diskpart` is. See
[README.md](README.md#running-locally) for the Visual Studio path and the admin-debugging note.

## Packaging the release

Release builds are published **self-contained** so the download needs no .NET or Windows App SDK
runtime installed:

```bash
dotnet publish DiskPartUI.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true
```

The installer is an [Inno Setup](https://jrsoftware.org/isinfo.php) script,
[`installer/DiskPartUI.iss`](installer/DiskPartUI.iss), which packs that publish folder into
`bin\DiskPartUI-v<version>-setup.exe`:

```bash
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\DiskPartUI.iss
```

MSIX is deliberately not used: a packaged app cannot request `requireAdministrator`, which
`diskpart` needs. The installer therefore sets `PrivilegesRequired=admin` and installs
per-machine into Program Files.

[`installer/Test-Installer.ps1`](installer/Test-Installer.ps1) smoke-tests the result end to end
— it silently installs, asserts the app files, the self-contained runtime, the Start Menu
shortcut, the uninstaller and the Programs-and-Features entry all exist, then silently
uninstalls and confirms the cleanup. It needs an **elevated** shell:

```bash
powershell -ExecutionPolicy Bypass -File installer\Test-Installer.ps1
```
