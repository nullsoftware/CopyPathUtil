# CopyPathUtil

A tiny Windows tray utility that copies the full path of the file(s) currently
selected in **File Explorer** to the clipboard with a single hotkey:

**`Ctrl` + `Shift` + `C`**

When multiple files are selected, all of their paths are copied, one per line.

## Features

- **Global hotkey** — works system-wide while Explorer is the foreground window.
- **Multi-selection support** — copies every selected item's full path, separated by newlines.
- **Lives in the system tray** — no window, no taskbar entry; right-click the tray icon to exit.
- **Single instance** — a named mutex prevents a second copy from running (safe for auto-start).
- **Optional copy notifications** — a balloon tip can confirm what was copied.
- **No external dependencies** — talks to Explorer through late-bound COM (`Shell.Application`) via reflection, so no interop assemblies are required.

## Requirements

- Windows
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download) (the project targets `net10.0-windows` and uses Windows Forms)

## Usage

1. Run `CopyPathUtil.exe`. The app starts silently and adds an icon to the system tray.
2. In File Explorer, select one or more files or folders.
3. Press **`Ctrl` + `Shift` + `C`**. The full path(s) are now on your clipboard.
4. To quit, right-click the tray icon and choose **Exit**.

If nothing is selected in Explorer (or Explorer isn't the active window), the app
shows a brief "No file selected" notice instead.

> **Note:** If `Ctrl+Shift+C` is already claimed by another application, the
> hotkey cannot be registered and the app will warn you on startup.

## Building

The project is a standard .NET Windows Forms app. From the repository root:

```sh
dotnet build CopyPathUtil/CopyPathUtil.csproj -c Release
```

Or open `CopyPathUtil.slnx` in Visual Studio and build.

To produce a self-contained executable for distribution, publish with your
preferred runtime, e.g.:

```sh
dotnet publish CopyPathUtil/CopyPathUtil.csproj -c Release
```

## How it works

The app creates a hidden window solely to receive `WM_HOTKEY` messages from the
Win32 `RegisterHotKey` API. When the hotkey fires, it gets the foreground window
handle, enumerates open shell windows via `Shell.Application`, matches the one
whose `HWND` equals the foreground window, and reads its selected items' paths.
The combined text is then placed on the clipboard (with a short retry loop in
case the clipboard is briefly locked by another process).

## License

Licensed under the Apache License 2.0 — see [LICENSE.txt](LICENSE.txt).

---

© Null Software
