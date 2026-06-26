<p align="center">
  <img src="https://img.shields.io/badge/version-1.1.0-blue?style=flat-square" alt="Version">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?style=flat-square" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square" alt=".NET">
  <img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="License">
  <img src="https://img.shields.io/github/downloads/GoodZheng/fastdog/total?style=flat-square" alt="Downloads">
</p>

<p align="center">
  <img src="https://github.com/GoodZheng/fastdog/raw/main/assets/fastdog-logo.png" alt="FastDog" width="120">
</p>

<h1 align="center">🐕 FastDog</h1>

<p align="center">
  <strong>A blazing-fast, minimalist text search tool for Windows</strong><br>
  <em>Powered by ripgrep · Built with WPF · Zero configuration</em>
</p>

<p align="center">
  <a href="#english">English</a> • <a href="#chinese">中文</a>
</p>

---

## English

FastDog is a lightweight desktop application that brings the power of [ripgrep](https://github.com/BurntSushi/ripgrep) to Windows users with a clean, modern interface.

### ✨ Why FastDog?

- **🚀 Blazing Fast** — Powered by ripgrep, the world's fastest text search tool
- **🎯 Precise Control** — Regex support, case sensitivity, whole word matching
- **📁 Smart Filtering** — Include/exclude files by pattern, skip binary files
- **💾 Layout Persistence** — Window position and search history auto-saved
- **🎨 Modern UI** — VS Code-inspired design with dark mode support
- **📦 Single Executable** — No installation required, just download and run
- **🔍 Instant Preview** — See matched lines in context immediately

### 📸 Screenshots

<p align="center">
  <img src="assets/screenshot-main.png" alt="FastDog Main UI" width="80%">
</p>

### 🚀 Quick Start

#### Download

Grab the latest release from [GitHub Releases](https://github.com/GoodZheng/fastdog/releases):

- **FastDog-Setup-{version}.exe** — Installer (recommended)
- **FastDog-portable-{version}.zip** — Portable version

#### Build from Source

```bash
git clone https://github.com/GoodZheng/fastdog.git
cd fastdog
dotnet run --project src/FastDog
```

### 🎯 Usage

1. **Set Search Path** — Enter directory path or click "Browse"
2. **Enter Search Term** — Plain text or regex pattern
3. **Configure Options** (optional):
   - ☑ Case sensitive
   - ☑ Whole word match
   - ☑ Regex mode
   - 📅 Date range filter
   - 📄 File type filter (e.g., `*.cs;*.txt`)
4. **Click Search** — Results appear instantly
5. **Click Result** — View matched line with context preview

### ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Enter` | Start search |
| `Esc` | Cancel search |
| `Double-click` | Open file in default editor |
| `Ctrl+C` | Copy selected result |

### 🔧 Configuration

FastDog automatically saves:

- Window position and size
- Grid splitter positions
- Recent search history (last 50)
- Search options and filters

Data stored in `%APPDATA%\FastDog\`

### 🏗️ Tech Stack

- **.NET 8** — Modern .NET runtime
- **WPF** — Windows Presentation Foundation
- **AvalonEdit** — Syntax-highlighted code preview
- **ripgrep 14.1.1** — Core search engine (bundled)
- **CommunityToolkit.Mvvm** — MVVM framework

### 📦 Project Structure

```
FastDog/
├── src/
│   └── FastDog/
│       ├── MainWindow.xaml          # Main UI
│       ├── Models/                  # Data models
│       ├── Services/                # Business logic
│       ├── ViewModels/              # MVVM view models
│       └── Assets/                  # Icons and resources
├── tools/
│   └── rg.exe                       # Bundled ripgrep
└── tests/
    └── FastDog.Tests/               # Unit tests
```

### 🤝 Contributing

Contributions are welcome! Feel free to:

- 🐛 Report bugs
- 💡 Suggest features
- 🔧 Submit pull requests
- 📝 Improve documentation

### 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### 🙏 Acknowledgments

- [ripgrep](https://github.com/BurntSushi/ripgrep) — The fastest search tool in the universe
- [grepWin](https://github.com/stefankueng/grepWin) — Inspiration for search functionality
- [.NET Community Toolkit](https://github.com/CommunityToolkit/dotnet) — MVVM framework

---

## Chinese

FastDog 是一个轻量级桌面应用，将 [ripgrep](https://github.com/BurntSushi/ripgrep) 的强大功能通过现代、简洁的界面带给 Windows 用户。

### ✨ 为什么选择 FastDog？

- **🚀 极速搜索** — 基于 ripgrep，全球最快的文本搜索工具
- **🎯 精准控制** — 支持正则表达式、大小写敏感、全词匹配
- **📁 智能过滤** — 按模式包含/排除文件，自动跳过二进制文件
- **💾 布局持久化** — 窗口位置和搜索历史自动保存
- **🎨 现代界面** — 受 VS Code 启发的设计，支持深色模式
- **📦 单文件可执行** — 无需安装，下载即用
- **🔍 即时预览** — 立即查看匹配行的上下文

### 📸 截图

<p align="center">
  <img src="assets/screenshot-main.png" alt="FastDog 主界面" width="80%">
</p>

### 🚀 快速开始

#### 下载

从 [GitHub Releases](https://github.com/GoodZheng/fastdog/releases) 获取最新版本：

- **FastDog-Setup-{version}.exe** — 安装版（推荐）
- **FastDog-portable-{version}.zip** — 便携版

#### 从源码构建

```bash
git clone https://github.com/GoodZheng/fastdog.git
cd fastdog
dotnet run --project src/FastDog
```

### 🎯 使用方法

1. **设置搜索路径** — 输入目录路径或点击"浏览"
2. **输入搜索词** — 纯文本或正则表达式
3. **配置选项**（可选）：
   - ☑ 区分大小写
   - ☑ 全词匹配
   - ☑ 正则模式
   - 📅 日期范围过滤
   - 📄 文件类型过滤（如 `*.cs;*.txt`）
4. **点击搜索** — 结果立即显示
5. **点击结果** — 查看带上下文的匹配行预览

### ⌨️ 快捷键

| 快捷键 | 操作 |
|--------|------|
| `Enter` | 开始搜索 |
| `Esc` | 取消搜索 |
| `双击` | 用默认编辑器打开文件 |
| `Ctrl+C` | 复制选中的结果 |

### 🔧 配置

FastDog 自动保存：

- 窗口位置和大小
- 网格分割器位置
- 最近搜索历史（最后 50 条）
- 搜索选项和过滤器

数据存储在 `%APPDATA%\FastDog\`

### 🏗️ 技术栈

- **.NET 8** — 现代 .NET 运行时
- **WPF** — Windows 演示基础
- **AvalonEdit** — 语法高亮代码预览
- **ripgrep 14.1.1** — 核心搜索引擎（内置）
- **CommunityToolkit.Mvvm** — MVVM 框架

### 📦 项目结构

```
FastDog/
├── src/
│   └── FastDog/
│       ├── MainWindow.xaml          # 主界面
│       ├── Models/                  # 数据模型
│       ├── Services/                # 业务逻辑
│       ├── ViewModels/              # MVVM 视图模型
│       └── Assets/                  # 图标和资源
├── tools/
│   └── rg.exe                       # 内置 ripgrep
└── tests/
    └── FastDog.Tests/               # 单元测试
```

### 🤝 贡献

欢迎贡献！你可以：

- 🐛 报告 bug
- 💡 建议新功能
- 🔧 提交 pull request
- 📝 改进文档

### 📄 许可证

本项目基于 MIT 许可证授权 - 详见 [LICENSE](LICENSE) 文件

### 🙏 致谢

- [ripgrep](https://github.com/BurntSushi/ripgrep) — 宇宙中最快的搜索工具
- [grepWin](https://github.com/stefankueng/grepWin) — 搜索功能灵感来源
- [.NET Community Toolkit](https://github.com/CommunityToolkit/dotnet) — MVVM 框架

---

<p align="center">
  <sub>Built with ❤️ by <a href="https://github.com/GoodZheng">GoodZheng</a></sub>
</p>
