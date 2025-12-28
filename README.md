# CursorLock

CursorLock is a small Windows utility to keep the mouse cursor confined (Cursor Lock/Cage) for single-monitor apps, games, or kiosk scenarios.

## Overview

CursorLock is a lightweight .NET application that restricts the mouse cursor to a chosen area or the current window. This repository contains the source code (targeting Windows) and published builds in the `bin/Release` folder.

## Quick Start — Use the prebuilt executable

- For those just looking for the executable you can directly download the executable from the root level of the project (CursorCage.exe)

- Download or copy the published files from the `publish` folder produced by a Release build. By default the publish output is located at:

```
CursorLock\bin\Release\net48\win-x64\publish
```

- Inside that folder you'll find the executable . To run it:

PowerShell (run from the publish folder):

```powershell
# Run from the folder where the exe lives
.\CursorCage.exe
```

- You can also double-click the `.exe` in Explorer. If the app needs elevated privileges to control input or interact with system features, right-click and choose **Run as administrator**.

## Build from source

- Prerequisites:
  - Install the .NET SDK appropriate for the project's target frameworks (this repo contains builds for .NET Framework and .NET 8 in places). Installing the latest .NET SDK from https://dotnet.microsoft.com is a good start.

- From the repository root, use PowerShell to publish a self-contained Windows x64 build (example):

```powershell
cd .\CursorLock
dotnet publish -c Release -r win-x64 --self-contained true
```

- The published files will be placed under `CursorLock\bin\Release\net48\win-x64\publish\`. Copy the contents of that `publish` folder to the machine where you want to run the exe.

## Configuration

- The project includes `appsettings.json` and `appsettings.Development.json` for runtime settings. Edit these files if you need to change defaults.

- Common configuration items:
  - Behavior toggles (how the cursor is confined)
  - Logging verbosity

Refer to the source files (`Program.cs` and `CursorLocker.cs`) to see which configuration keys are used and how the app behaves.

## Troubleshooting

- If the exe doesn't start, try running it from an elevated PowerShell prompt.
- If your antivirus flags the executable, verify the file came from a trusted build or build from source locally.
- If the cursor isn't being confined as expected, check for other utilities (virtual desktops, multi-monitor tools) that may interfere.

## Contributing

- Bug reports and small fixes are welcome. If you plan to make changes:
  - Fork the repo
  - Create a feature branch
  - Open a pull request with a clear description

## License

- This project uses the MIT licesne

