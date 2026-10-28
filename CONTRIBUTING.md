# Contributing to AVR Desktop Control

Thanks for your interest! This is a personal project, but contributions are welcome.

## How to contribute

1. Fork the repository
2. Create a branch: `git checkout -b feature/my-improvement`
3. Make your changes and commit: `git commit -m "Add my improvement"`
4. Push to your fork: `git push origin feature/my-improvement`
5. Open a Pull Request

## Reporting bugs

Open an [Issue](https://github.com/sreddev/AVR-desktop-control/issues) with:
- Your receiver model
- Windows version
- Steps to reproduce the problem
- A screenshot, if relevant

## Local development

```bash
# Clone
git clone https://github.com/sreddev/AVR-desktop-control.git
cd AVR-desktop-control/src/DenonRemote

# Build and run
dotnet run

# Publish a release build
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Project structure

```
src/DenonRemote/
├── Denon/          # Denon protocol: telnet, HTTP, event parser
├── Discovery/      # SSDP + subnet scan
├── Heos/           # HEOS CLI client and UPnP transport (seek)
├── Services/       # Orchestrator, settings, auto-update, UI strings
├── ViewModels/     # MVVM with CommunityToolkit
├── Views/          # XAML (Fluent/Mica with WPF-UI)
│   └── Pages/      # NavigationView pages (Home, Sources, Tuner, HEOS, Sound, Processing, Zones, Receiver...)
├── Controls/       # Custom controls (ChannelChip)
├── Converters/     # Value converters for bindings
└── Assets/         # Icons
```

## Contact

Author: Sergio
- GitHub: [@sreddev](https://github.com/sreddev)
- Telegram: [@sreddev](https://t.me/sreddev)
- Donate: [PayPal](https://www.paypal.com/paypalme/sreddev?locale.x=ro_RO&country.x=RO)
