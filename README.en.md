[中文文档](./README.md)

# LiteMonitor

![AI-assisted development](https://img.shields.io/badge/AI--assisted-development-blueviolet)

> [!IMPORTANT]
> Everything this fork changes relative to the upstream original was written by an AI coding assistant under human direction (with human review and real-device acceptance testing).

> **This repository is a fork of the original project [Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor).**
> For the original feature overview, screenshots, installation guide and configuration docs (themes, `settings.json`, languages), see the **[upstream README](https://github.com/Diorser/LiteMonitor#readme)**.

## 🔧 What this fork changes

- Fixed GPU usage / VRAM showing 0 after startup on Intel integrated graphics (ported community fix + startup warm-up fallback).
- Reproducible builds: a fresh clone builds out of the box (the original fails to build because a notice file it references is not in the repository).
- Runtime base upgraded to .NET 10 (LTS, supported until Nov 2028).
- About dialog: copyright year now follows the current year; the original author, repository and website are clearly labeled, plus this fork's attribution.
- Feedback links and the update check point to this fork (they no longer send users back upstream).
- Monitor item display order supports drag-and-drop (drag the grip at the left edge of a row or a group header; the context menu offers move to top / up / down / bottom and "restore default order").
- Fixed crashes when updating or switching a GPU driver, or disabling a display adapter (the underlying hardware library was upgraded to remove stale driver-handle access violations).
- GPU readings no longer freeze on the last value after a display adapter is disabled (they show `—`) and recover automatically once the device is back — no manual settings change needed; when a GPU is unavailable, the app falls back to one that is still present.

## 🛠️ Build Instructions

### Requirements
- Windows 10 / 11
- **.NET 10 SDK** (required to build; running the app needs .NET Desktop Runtime 10)
- IDE (optional), any of the following — or just the command line:
  - Visual Studio 2026 (18.0+)
  - JetBrains Rider
  - VS Code / plain command line (`dotnet build`)

> ⚠️ **Visual Studio 2022 cannot target `net10.0`**: in the official support matrix, `net10.0` is supported in Visual Studio 18.0+ only.

### Build
```bash
git clone https://github.com/azhuge233/LiteMonitor.git
cd LiteMonitor
dotnet build -c Release
```

Output:
```
/bin/Release/net10.0-windows/LiteMonitor.exe
```

---

## 📄 License

Released under the **MIT License** (same as the original project), free for personal & commercial use.

---

## 💬 Contact

Contact information of the **original author** (this fork provides no additional contact):

**Original author:** Diorser  
**Original project:** [https://github.com/Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor)
