# nordic-launcher

Windows launchers for the Nordic apps that run in WSL:

| App | Script in Ubuntu | Port |
| --- | --- | --- |
| Nordic Prep | `~/projects/nordic-prep/viewer/open-dashboard.sh` | 28765 |
| Nordic Overview | `~/projects/nordic-overview/viewer/open-viewer.sh` | 28800 |

Each one is an `.exe` with the app's icon embedded. That gives it its own Start
Menu entry, search result and taskbar pin.

## Install

From PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```

`install.ps1`:

- compiles `Launcher.cs` once per app, embedding `prep.ico` or `overview.ico`
- creates or updates the Start Menu shortcut, plus any existing taskbar pins,
  so they point at that exe

To pin an app, pin it from Start, then re-run `install.ps1`. Re-run it after
changing an icon or `Launcher.cs` as well.

## How it works

`<exe> <script> <app id> <profile>`

1. Runs the script in Ubuntu through `wslg.exe`. The script:
   - starts the app's server if its port is free
   - opens Chrome in app mode with `--user-data-dir=profiles\<profile>`
   - stops the server again when the window closes
2. Finds the new Chrome window by that profile and gives it the shortcut's
   app ID, so the window groups under the pin rather than opening a second
   taskbar button.

The profile names in `install.ps1` must match `PROFILE` in each script.

## Not tracked

- `profiles/`: Chrome's data for each app, created on first launch.
- `*.exe`: built by `install.ps1`.
