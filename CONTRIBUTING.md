# Contributing to AVR Control Center

Contributions are welcome: bug reports, receiver compatibility notes and pull requests.

## Contents

- [Reporting bugs](#reporting-bugs)
- [Development setup](#development-setup)
- [Pull requests](#pull-requests)
- [Adding a receiver setting](#adding-a-receiver-setting)
- [Code style](#code-style)
- [Building a release](#building-a-release)
- [Contact](#contact)

## Reporting bugs

Open an [issue](https://github.com/sreddev/AVR-control-center/issues) with:
- your receiver model and firmware, if known
- your Windows version and the app version (Settings → About)
- the steps to reproduce the problem
- a screenshot and `%TEMP%\AvrControlCenter-crash.log`, if the app crashed

Reports that a feature does or doesn't appear on a given model are very useful, because the app shows options based on what the receiver answers.

## Development setup

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Visual Studio 2022 or VS Code are optional.

```bash
git clone https://github.com/sreddev/AVR-control-center.git
cd AVR-control-center/src/AvrControlCenter
dotnet run
```

The solution file is `AVRControlCenter.sln`; the app lives in `src/AvrControlCenter` and the unit tests (xUnit) in `tests/AvrControlCenter.Tests`. Run them from the repository root with `dotnet test`. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the code is organised.

> Receivers accept only one telnet client. Close other remote apps (or Home Assistant's Denon integration) while debugging.

## Pull requests

1. Fork the repository and create a branch: `git checkout -b feature/my-improvement`
2. Keep changes focused, build with no warnings (`dotnet build -c Release`) and keep the tests green (`dotnet test`)
3. Describe what you tested and on which receiver
4. Open a pull request against `main`

## Adding a receiver setting

Most receiver parameters need **one line** in `src/AvrControlCenter/Avr/AvrSettingsCatalog.cs`, and no UI code:

```csharp
yield return Toggle(SettingGroup.DialogDynamics, "cinemaeq", "Cinema EQ",
    "Softens harsh treble on film soundtracks.",
    "PSCINEMA EQ.", "PSCINEMA EQ. ?");
```

| Builder | Use for | Extra arguments |
|---|---|---|
| `Toggle` | ON / OFF parameters | — |
| `Choice` | one of a list | `(value, caption)` pairs; `strict: true` if another setting shares the prefix |
| `Level` | numbers | `min, max, step`, plus `factor` / `offset` / `digits` / `unit` for the token encoding |
| `Action` | commands without read-back | `(command, caption)` pairs |

- **Group**: decides the page and card. `RoomCorrection`, `DialogDynamics`, `Surround` and `Extras` go to Processing; `Video`, `DisplayPower` and `Locks` to Receiver; `Zones` and `Sleep` to Zones; `Sound` adds a control to the Sound page by key.
- **Prefix**: include the trailing space if the receiver uses one (`"PSDYNEQ "`).
- **Testing**: the setting stays hidden until the receiver answers the query, so check it on a real receiver. Add `// verified` when you've read the value back from one.

## Code style

- Match the surrounding code: file header, `/// <summary>` on public members, comments that explain *why*.
- UI strings go in `Services/Localization.cs`.
- Colours come from `Views/Palette.Dark.xaml` and `Views/Palette.Light.xaml`, text styles from `Views/Theme.xaml`. Use the existing brushes rather than hex values; a new colour needs the same key in both palettes.
- The receiver must never get a command the user didn't make. Use `ReceiverState.IsApplyingRemote` to tell user edits from receiver echoes.

## Building a release

1. Set the version in `src/AvrControlCenter/AvrControlCenter.csproj` (`<Version>`) and in `installer/setup.iss` (`AppVersion`, `OutputBaseFilename`).
2. Add a section to `CHANGELOG.md`, and run `dotnet test`: every test must pass.
3. Publish a self-contained single-file build:
   ```powershell
   dotnet publish src/AvrControlCenter/AvrControlCenter.csproj -c Release -r win-x64 --self-contained true `
     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
     -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
   ```
4. Build the installer and the MSIX package (the MSIX script publishes its own build):
   ```powershell
   & "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\setup.iss
   pwsh packaging/msix/build-msix.ps1
   ```
5. Commit, then create a GitHub release with the tag `v<version>` (for example `v3.0.5`) and attach the Setup.exe and the .msix. Don't attach a portable zip. The in-app update check compares this tag with the running version and downloads the `-Setup.exe` asset, so the tag must match the version.

The MSIX is signed with a self-signed test certificate (`CN=sreddev`). A Microsoft Store submission uses the package identity and publisher from Partner Center instead (`build-msix.ps1 -Publisher "CN=…"`), and the Store signs it.

`publish/` and `installer/out/` are git-ignored.

## Contact

Sergio
- GitHub: [@sreddev](https://github.com/sreddev)
- Telegram: [@sreddev](https://t.me/sreddev)
- Donate: [PayPal](https://www.paypal.com/paypalme/sreddev?locale.x=ro_RO&country.x=RO)
