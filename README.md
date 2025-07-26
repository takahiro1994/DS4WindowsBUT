# DS4Windows

[![.NET Release](https://github.com/madziaar/DS4WindowsBUT/actions/workflows/release.yml/badge.svg)](https://github.com/madziaar/DS4WindowsBUT/actions/workflows/release.yml)

A powerful controller mapping tool that transforms your DualShock 4, DualSense, Switch Pro, and JoyCon controllers into Xbox 360 controllers for maximum game compatibility on Windows.

## ✨ Features

- **Universal Controller Support**: DualShock 4, DualSense, Switch Pro, and JoyCon controllers (**first party hardware only**)
- **Xbox 360 Emulation**: Broad game compatibility through Xbox 360 controller emulation
- **Advanced Input Processing**: Switch debouncing, stick drift compensation, and motion controls
- **Customizable Profiles**: Per-game settings with automatic profile switching
- **Lightbar & Haptics**: Full support for controller lighting and rumble effects
- **Low Latency**: Optimized for competitive gaming with minimal input delay

## 🔧 What's New in This Fork

This fork addresses several issues and adds enhancements over the original DS4Windows:

### Key Improvements
- **Button Debouncing**: Fixes controller button bounce issues that cause unwanted double inputs
- **Stick Drift Compensation**: Advanced filtering to reduce the impact of analog stick drift
- **DS3 Motion Support**: Pitch and roll simulation based on accelerometer data
- **Updated Version Detection**: Uses this repository for version checks (no more outdated version popups)
- **Enhanced Stability**: Various bug fixes and performance improvements

### Technical Enhancements
- Modern .NET 8.0 WPF framework
- Improved memory management
- Better error handling and logging
- Streamlined build process

## 📦 Installation

### For Users

#### Option 1: Direct Download
1. Download the latest release from [Releases](https://github.com/madziaar/DS4WindowsBUT/releases)
2. Extract to your preferred location
3. Run `DS4Windows.exe`

#### Option 2: Scoop Package Manager
```powershell
scoop install ds4windows
```

#### Option 3: Quick Install Script
```powershell
# Download and run the install script
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/madziaar/DS4WindowsBUT/master/ds4w.bat" -OutFile "ds4w.bat"
.\ds4w.bat
```

### For Developers

#### Prerequisites
- Windows 10 or newer
- Visual Studio 2022 or later (with .NET desktop development workload)
- .NET 8.0 SDK
- Git

#### Building from Source

1. **Clone the repository**
   ```bash
   git clone https://github.com/madziaar/DS4WindowsBUT.git
   cd DS4WindowsBUT
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the solution**
   ```bash
   # Debug build
   dotnet build --configuration Debug
   
   # Release build
   dotnet build --configuration Release
   ```

4. **Build for specific platform**
   ```bash
   # x64 build
   dotnet build --configuration Release --arch x64
   
   # x86 build  
   dotnet build --configuration Release --arch x86
   ```

5. **Run tests**
   ```bash
   dotnet test
   ```

6. **Create distribution package**
   ```bash
   # x64 package
   dotnet publish .\DS4Windows\DS4WinWPF.csproj -c Release -r win-x64 --self-contained false
   
   # x86 package
   dotnet publish .\DS4Windows\DS4WinWPF.csproj -c Release -r win-x86 --self-contained false
   ```

#### Development Setup

The project includes development automation scripts:

- **`.mentat/setup.sh`**: Installs dependencies and prepares the development environment
- **`.mentat/format.sh`**: Formats code using .NET standard formatters

## 📋 System Requirements

### Runtime Requirements
- **OS**: Windows 10 or newer (64-bit or 32-bit)
- **Runtime**: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) ([x64](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x64-installer) | [x86](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x86-installer))
- **Redistributable**: [Visual C++ 2015-2022](https://docs.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist) ([x64](https://aka.ms/vs/17/release/vc_redist.x64.exe) | [x86](https://aka.ms/vs/17/release/vc_redist.x86.exe))
- **Driver**: [ViGEmBus](https://vigem.org/) (auto-installed by DS4Windows)

### Supported Controllers
- **Sony DualShock 4** (all revisions)
- **Sony DualSense** (PS5 controller)
- **Nintendo Switch Pro Controller**
- **Nintendo Joy-Con** (L/R pair)

### Connection Methods
- **USB**: Direct connection via USB cable
- **Bluetooth 4.0+**: Built-in or USB adapter (Microsoft BT stack only)
- **Sony Wireless Adapter**: Official DualShock 4 USB adapter

⚠️ **Important**: 
- CSR Bluetooth stack is not supported
- Toshiba Bluetooth adapters may not work reliably
- Disable Steam's controller configuration support for best compatibility

## 🎮 Quick Start

1. **Install DS4Windows** using one of the methods above
2. **Connect your controller** via USB or Bluetooth
3. **Launch DS4Windows** - it will auto-detect and configure your controller
4. **Install ViGEmBus** when prompted (required for Xbox 360 emulation)
5. **Configure profiles** for your games using the built-in editor

### Steam Users
Disable these Steam settings for optimal compatibility:
- Steam → Settings → Controller → PlayStation Configuration Support: **OFF**
- Steam → Settings → Controller → Xbox Configuration Support: **OFF**

## 🛠️ Advanced Configuration

### Profile Management
- Create custom profiles for different games
- Set up automatic profile switching
- Configure per-game input mappings and sensitivity

### Input Customization
- Remap any button or stick
- Create macros and special actions
- Adjust dead zones and response curves
- Enable motion controls and gyro input

### Troubleshooting
- Enable diagnostic logging for debugging
- Use the built-in controller test interface
- Check Windows Device Manager for driver issues
- Restart DS4Windows service if controllers stop responding

## 🤝 Contributing

We welcome contributions! Here's how you can help:

### Bug Reports
- Use the [issue tracker](https://github.com/madziaar/DS4WindowsBUT/issues)
- Include system info, DS4Windows version, and steps to reproduce
- Attach logs from the application when possible

### Feature Requests
- Check existing issues first to avoid duplicates
- Describe the use case and expected behavior
- Consider implementation complexity and user impact

### Code Contributions
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes with proper testing
4. Follow the existing code style (use the provided format scripts)
5. Commit your changes (`git commit -m 'Add amazing feature'`)
6. Push to your branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

### Development Guidelines
- Test changes thoroughly before submitting
- Maintain backward compatibility when possible
- Document any new features or API changes
- Follow .NET coding conventions and best practices

## 📄 License

DS4Windows is licensed under the [GNU General Public License v3.0](LICENSE.txt).

```
Copyright © Scarlet.Crush Productions 2012-2013
Copyright © InhexSTER, HecticSeptic, electrobrains 2013-2014  
Copyright © Jays2Kings 2013-2016
Copyright © Ryochan7 2017-2023
Copyright © schmaldeo 2024
Copyright © madziaar 2024
```

## 🔗 Links

- **Releases**: [GitHub Releases](https://github.com/madziaar/DS4WindowsBUT/releases)
- **Documentation**: [User Guide](USERGUIDE.md)
- **Issues**: [Bug Reports & Feature Requests](https://github.com/madziaar/DS4WindowsBUT/issues)
- **Original Project**: [DS4Windows by Ryochan7](https://github.com/Ryochan7/DS4Windows)

## 🙏 Acknowledgments

- **Jays2Kings** & **Ryochan7** - Original DS4Windows developers
- **schmaldeo** - Continued development and maintenance
- **sunnyqeen** - DS3 accelerometer implementation
- **ViGEm Project** - Virtual gamepad framework
- All contributors who have helped improve DS4Windows over the years

---

**Note**: This project is not affiliated with Sony Interactive Entertainment or Nintendo. DualShock, DualSense, and PlayStation are trademarks of Sony Interactive Entertainment. Nintendo Switch and Joy-Con are trademarks of Nintendo.
