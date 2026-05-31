# SafeDrive Backup

A lightweight Windows desktop application that automatically backs up your personal files — Desktop, Documents, Downloads, Pictures, and more — to a local or network destination of your choice.

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

- **Real-time protection** — Continuous mode watches for file changes and backs up within seconds
- **Scheduled backups** — Choose every 30 minutes, hourly, daily, or only when a backup drive is connected
- **Version history** — Keeps multiple versions of each file so you can recover older copies
- **Deleted file recovery** — Files removed from your computer are kept in the backup for a configurable number of days
- **Restore anywhere** — Browse and restore any file to its original location or a custom folder
- **Ransomware protection** — Automatically pauses backup if an abnormally large number of files change in a short time
- **Runs silently in the background** — Lives in the system tray, never gets in your way
- **No cloud, no subscription** — Your data stays on your own drive

---

## Screenshots

> _Coming soon_

---

## Installation

### Option 1 — Installer (Recommended)

1. Download `SafeDriveBackup-Setup-v1.0.0.exe` from the [Releases](../../releases) page
2. Run the installer and follow the setup wizard
3. Accept the MIT license agreement
4. Choose your install folder and click **Install**
5. SafeDrive Backup will launch automatically after installation

### Option 2 — Portable EXE

Download `SafeDriveBackup.exe` from the [Releases](../../releases) page and run it directly — no installation required.

---

## Requirements

- Windows 10 or later (64-bit)
- No additional software required — .NET 8 runtime is bundled

---

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- Windows (WPF requires Windows)

### Build

```bash
git clone https://github.com/Songnamz/SAFEDRIVE-BACKUP.git
cd "SAFEDRIVE-BACKUP/SafeDriveBackup.App"
dotnet build
```

### Run

```bash
dotnet run
```

### Publish (single self-contained EXE)

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### Build Installer

1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php)
2. Publish the app (step above)
3. Open `installer.iss` in Inno Setup and press **F9**

---

## How It Works

```
Your Files                 SafeDrive Backup              Backup Destination
──────────                 ────────────────              ──────────────────
Desktop   ──┐                                            Current/
Documents ──┼──► File Watcher ──► Backup Engine ──────► Versions/
Downloads ──┤    or Schedule                             Deleted/
Pictures  ──┘                                            Logs/
```

- **Current/** — Latest copy of every backed-up file
- **Versions/** — Previous versions organised by file name and timestamp
- **Deleted/** — Files deleted from source, kept for a configurable number of days
- **Logs/** — Daily backup logs

---

## Configuration

On first launch, the setup wizard guides you through:

1. Selecting which folders to back up
2. Choosing a backup destination (local drive, external drive, or network path)
3. Selecting a backup mode

All settings are stored in `%APPDATA%\SafeDriveBackup\safedrive-config.json`.

---

## License

MIT License — Copyright © 2026 Songnam Saraphai

See [LICENSE.txt](LICENSE.txt) for full text.
