[English](./README.en.md)

# LiteMonitor

![AI-assisted development](https://img.shields.io/badge/AI--assisted-development-blueviolet)

> [!IMPORTANT]
> 本 fork 相对上游原版的所有改动，均由 AI 编程助手在人工指导下完成（含人工审阅与实机验收）。

> **本仓库是原项目 [Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor) 的 fork。**
> 原版的功能介绍、界面截图、安装使用方法与配置说明（主题、`settings.json`、多语言等），请看 **[上游项目 README](https://github.com/Diorser/LiteMonitor#readme)**。

## 🔧 本 fork 的改动

- 修复 Intel 核显平台启动后 GPU 占用/显存显示为 0 的问题（移植社区修法 + 启动预热兜底）。
- 修复构建可复现性：全新克隆可直接编译（原版会因缺少一个未入库的说明文件而构建失败）。
- 运行时基座升级到 .NET 10（LTS，支持至 2028 年 11 月）。
- 关于界面：版权年份改为随现实时间自动更新，明确标注原作者、原仓库与原官网，并新增本 fork 的署名。
- 反馈入口与「检查更新」指向本 fork（不再把用户带回上游项目）。
- 监控项「显示顺序」支持拖拽排序（抓住行 / 分组标题左缘拖动；右键菜单含置顶 / 上移 / 下移 / 置底与「恢复默认顺序」）。
- 修复更新/切换显卡驱动或禁用显卡时可能崩溃退出的问题（升级底层硬件库，消除显卡驱动的陈旧句柄访问违例）。
- 显卡被禁用或移除后，读数不再停在旧值（显示 `—`）；设备恢复后自动恢复读数、无需手动改设置；显卡不可用时自动回退到仍可用的显卡。

## 🛠️ 编译说明

### 环境要求
- Windows 10 / 11
- **.NET 10 SDK**（构建必需；运行程序需 .NET Desktop Runtime 10）
- IDE 可选，以下任一即可，也可只用命令行：
  - Visual Studio 2026（18.0+）
  - JetBrains Rider
  - VS Code / 纯命令行（`dotnet build`）

> ⚠️ **Visual Studio 2022 不支持 `net10.0` 目标**：官方支持矩阵中，`net10.0` 仅在 Visual Studio 18.0+ 正式支持。

### 编译命令
```bash
git clone https://github.com/azhuge233/LiteMonitor.git
cd LiteMonitor
dotnet build -c Release
```

输出文件：
```
/bin/Release/net10.0-windows/LiteMonitor.exe
```

---

## 📄 开源协议

本项目基于 **MIT License** 开源（与原版一致），可自由使用、修改与分发。

---

## 📬 联系方式

以下为**原作者**的联系方式（本 fork 不再提供额外联系方式）：

**原作者**：Diorser  
**原项目主页**：[https://github.com/Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor)
