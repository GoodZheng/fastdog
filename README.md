<p align="center">
  <img src="https://img.shields.io/badge/version-1.1.0-blue?style=flat-square" alt="Version">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?style=flat-square" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square" alt=".NET">
  <img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="License">
  <img src="https://img.shields.io/github/downloads/GoodZheng/fastdog/total?style=flat-square" alt="Downloads">
</p>

<h1 align="center">
  <img src="src/FastDog/Assets/newLogo1.2.png" alt="FastDog" width="36" valign="middle"> FastDog
</h1>

<p align="center">
  <strong>A blazing-fast, minimalist text search tool for Windows</strong><br>
  <em>Powered by ripgrep · Built with WPF · Zero configuration</em>
</p>

<p align="center">
  <strong>English</strong> | <a href="README.zh-CN.md">中文</a>
</p>

---

### ✨ Why FastDog?

- **🚀 Blazing Fast** — Powered by [ripgrep](https://github.com/BurntSushi/ripgrep), the world's fastest text search tool
- **🎯 Precise Control** — Regex support, case sensitivity, whole word matching
- **📁 Smart Filtering** — Include/exclude files by pattern, skip binary files
- **💾 Layout Persistence** — Window position and search history auto-saved
- **🎨 Modern UI** — VS Code-inspired design, clean and professional
- **📦 Single Executable** — No installation required, just download and run
- **🔍 Instant Preview** — See matched lines in context immediately

### 📸 Screenshots

**Quick Search Example**

<p align="center">
  <img src="src/FastDog/Assets/P1.png" alt="FastDog Main UI" width="80%">
</p>

### 🚀 Quick Start

#### Download

Grab the latest release from [GitHub Releases](https://github.com/GoodZheng/fastdog/releases):

| File | Description |
|------|-------------|
| `FastDog-Setup-{version}.exe` | Installer (recommended) |
| `FastDog-portable-{version}.zip` | Portable version, extract and run |

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

Configuration stored in `%APPDATA%\FastDog\`

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

<p align="center">
  <sub>Built with ❤️ by <a href="https://github.com/GoodZheng">GoodZheng</a></sub>
</p>
