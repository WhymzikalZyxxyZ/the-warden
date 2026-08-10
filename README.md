# The Warden

A native Windows diagnostics and disk-hygiene utility — scans well-known junk locations (temp, browser caches, thumbnail cache, Windows Update leftovers, crash dumps), optionally flags suspicious executables against a threat-reputation service, and quarantines what you approve. Nothing is ever permanently deleted without an explicit, typed confirmation.

Built with **.NET 8** and **WinUI 3**. No cloud required. No account required. No telemetry.

> **What this is not:** an antivirus. The Warden doesn't run signature-based malware detection — see [Scope & Safety Model](#scope--safety-model) below. That boundary is deliberate; see [`docs/adr/0002`](docs/adr/0002-detection-and-safety-model.md) for the full reasoning.

---

## Download & Run

Go to the [**Latest Release**](../../releases/latest) and grab the build for your machine:

| Format | Download | Notes |
|---|---|---|
| **Portable** | `TheWarden.App.exe` | No install — download and double-click |
| **MSIX** | `TheWarden.App.msix` | Planned — see [Roadmap](#roadmap); not yet published |

> **SmartScreen warning?** Click **More info → Run anyway**. The app is signed with a self-signed certificate rather than a paid EV certificate, so it hasn't built up SmartScreen reputation yet — see [`docs/adr/0003`](docs/adr/0003-distribution-and-code-signing.md) for why, and the plan to eliminate this.

No Node.js, no .NET SDK, no terminal required to run it — the published executable is fully self-contained.

---

## Scope & Safety Model

This is the part of the project that's actually worth reading, because it's the whole design philosophy in one table.

| The Warden does | The Warden does not |
|---|---|
| Classify files in known junk locations via a versioned, editable rule pack | Run signature-based malware detection |
| Optionally hash-check executables against VirusTotal (**opt-in, your own API key**) | Auto-delete anything based on a reputation flag |
| Move everything it removes into a restorable **Quarantine** | Ever hard-delete without your explicit action |
| Require a literal typed confirmation (`DELETE`) to permanently purge quarantine | Silently purge, ever |
| Request elevation only for the specific operations that need it | Run as administrator app-wide |
| Log every scan/quarantine/restore/purge locally | Phone home — no telemetry, by default or otherwise |

Full reasoning: [`docs/adr/0002-detection-and-safety-model.md`](docs/adr/0002-detection-and-safety-model.md).

---

## Your Data

Everything lives on your machine. The Warden never connects to a remote server unless you've opted in to VirusTotal lookups, and even then only the specific file hash is sent.

| Item | Location |
|---|---|
| Quarantine folder + manifest | `%LOCALAPPDATA%\TheWarden\Quarantine\` |
| Local activity log | `%LOCALAPPDATA%\TheWarden\Logs\` |
| VirusTotal API key (if configured) | Windows Credential Locker (DPAPI-backed) — never plain text on disk |

See [`SECURITY.md`](SECURITY.md) for the full data-handling policy and threat model.

---

## What's Inside

- **Scanning** — a declarative, versioned rule pack matches known-junk locations (system/user temp, Chrome/Edge caches, thumbnail cache, Windows Update leftovers, crash dumps)
- **Reputation lookups** — optional SHA-256 hash checks against VirusTotal for executables in commonly-abused locations
- **Quarantine** — every removal is a move-with-manifest, restorable at any time
- **Typed-confirmation purge** — the only irreversible action in the app, gated behind literally typing `DELETE`
- **PC health dashboard** — WMI-backed disk usage and startup-item counts, so a scan tells you more than just "junk found"
- **Elevation on demand** — the app runs unelevated by default and only prompts for admin rights on the specific operations that need it

---

## Architecture & Engineering Highlights

```
TheWarden.sln
├─ src/TheWarden.Core        # pure C#, no UI — the tested engine
│   ├─ Rules/                 # declarative junk-classification rule pack + engine
│   ├─ Scanning/               # filesystem walker → FileFinding[]
│   ├─ Quarantine/             # move-not-delete, manifest, restore, typed-confirm purge
│   ├─ Reputation/             # opt-in VirusTotal hash lookup
│   └─ Health/                 # WMI-backed disk/startup "PC health" summary
├─ src/TheWarden.App          # WinUI 3 shell (MVVM via CommunityToolkit.Mvvm)
├─ tests/TheWarden.Core.Tests # xUnit — ~94% line coverage on TheWarden.Core
└─ docs/adr/                  # architecture decision records
```

`TheWarden.Core` has zero UI dependencies by design — every scan, quarantine, and reputation decision is independently unit tested against fakes (`IDirectoryReader`, `ISystemHealthProbe`, `IClock`, `IEnvironmentExpander`), not the live OS. `TheWarden.App` is a thin MVVM coordinator over it.

Every non-obvious decision — why WinUI 3 over Electron/Tauri, why the deletion model is quarantine-first, why signing is self-signed for now, why this lives in its own repo — is written down as an ADR rather than left implicit:

- [`0001` — Desktop UI & Runtime](docs/adr/0001-desktop-ui-and-runtime.md)
- [`0002` — Detection & Safety Model](docs/adr/0002-detection-and-safety-model.md)
- [`0003` — Distribution & Code Signing](docs/adr/0003-distribution-and-code-signing.md)
- [`0004` — Repo Separation & Website Integration](docs/adr/0004-repo-separation-and-website-integration.md)

The full design document, including impact and gap analysis, is in [`DESIGN.md`](DESIGN.md).

---

## For Developers

If you want to run from source or contribute:

```bash
# Prerequisites: .NET 8 SDK, Windows 10 (19041+) or Windows 11
git clone https://github.com/WhymzikalZyxxyZ/the-warden.git
cd the-warden
dotnet restore
dotnet build TheWarden.sln
```

Run the app directly:

```bash
dotnet run --project src/TheWarden.App/TheWarden.App.csproj
```

Run the test suite:

```bash
dotnet test tests/TheWarden.Core.Tests/TheWarden.Core.Tests.csproj
```

Publish a portable executable:

```bash
dotnet publish src/TheWarden.App/TheWarden.App.csproj -r win-x64 -c Release --self-contained true
```

No separate Windows App SDK install is needed — it's pulled automatically via NuGet. See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the full workflow.

CI (`.github/workflows/ci.yml`) builds and tests on every push, and publishes signed release artifacts on version tags.

---

## Roadmap

- [ ] MSIX packaging in CI (currently portable exe only — see ADR-0003)
- [ ] EV code-signing certificate to eliminate the SmartScreen prompt
- [ ] User-editable rule packs surfaced in the UI (currently code-defined defaults)
- [ ] Scheduled/background scanning

---

## Related

Built and maintained by [Zyxxyz](https://zyxwonderland.xyz) — part of the [Whymzykal Wunderland](https://zyxwonderland.xyz) portfolio, alongside [EPITOME Desktop](https://github.com/WhymzikalZyxxyZ/epitome-desktop), a desktop app for writers.

---

## License

MIT
