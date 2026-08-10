# The Warden — Design Document

**Author:** Zyxxyz
**Domain of origin:** zyxwonderland.xyz (Technologist section)
**Repository:** the-warden (standalone, `WhymzikalZyxxyZ/the-warden`)
**Status:** Pre-development design
**Last updated:** 2026-08-10

---

## 1. Survey of prior context

Before designing this, the existing portfolio was reviewed for precedent:

- **Distribution pattern already exists.** `epitome-desktop` (Electron/TypeScript) is the org's one prior native desktop app. It's linked from the live site (`epitome/client/src/components/Layout.tsx`, `Dashboard.tsx`) via two CTAs that open its GitHub Releases page in a new tab — a ghost button in the persistent header and a featured banner anchor on the dashboard, both `rel="noopener noreferrer"`. This is the template The Warden's site integration will follow.
- **No native Windows systems project exists yet.** Everything else in the portfolio is web (Cloudflare Workers, static HTML/JS, React) or the one Electron app. There's a genuine gap: nothing demonstrates OS-level Windows programming, elevated-privilege handling, or filesystem-safety engineering.
- **Testing culture is established and public.** The main site advertises "490+ automated tests across 10+ language stacks" in memory notes. Whatever ships here needs a comparable, real test story — not a token one.
- **Naming convention:** entities are evocative, not literal (Epitome, Anonymail, The Locator, Lapis Lazuli). This project is named **The Warden**.
- **Branching/PR conventions apply to the site repo only** (`version/X.Y.Z` branches, acronym PR titles, sultry-but-precise commit messages, standard PR body). The new repo is independent and can set its own conventions, documented in its own CONTRIBUTING.md.

---

## 2. Product concept

**The Warden** is a Windows desktop utility that scans the local filesystem for temporary/junk files and flags suspicious executables using optional cloud reputation lookups — then lets the user clear them safely. It is explicitly **not** a signature-based antivirus. It is a disk-hygiene and triage tool with a safety-first deletion model, positioned in the "PC health" genre (comparable to CCleaner/BleachBit, but built with a modern, testable, transparent architecture and a real quarantine/undo system those tools generally lack).

**Tagline direction:** "It doesn't guess. It quarantines, shows you the evidence, and lets you decide."

---

## 3. Impact analysis

### Who this is for
1. **The author**, as a real Windows utility they'll actually use.
2. **Recruiters/hiring managers** who click through from the résumé/portfolio to a live GitHub repo and, ideally, run the tool. This is the highest-leverage audience — most portfolio pieces are read, not run. An executable that works in under two minutes on their own machine is a materially stronger signal than another web CRUD app.
3. **Anyone who downloads it off the site**, which means it has to behave like real software: no data loss, no false "malware removed" claims, no silent admin-privilege grabs.

### Positive impact
- Fills a concrete skills gap in the portfolio: native Windows programming (WinUI 3 / Win32 interop), privilege elevation done correctly, and — the strongest differentiator — a **filesystem-safety architecture** (quarantine, restore, typed-confirmation purge) that most "cleaner" apps in this category skip entirely. That safety design is itself the best interview talking point this project can produce.
- Demonstrates security-conscious judgment: explicitly scoping *away* from "we detect malware" (see ADR-0002) reads to a technical reviewer as maturity, not as a limitation.
- Gives the site a second, differentiated downloadable executable, reinforcing "this person ships real software" rather than "this person only ships a portfolio site."

### Risk if built carelessly
- A false-positive deletion (e.g. wiping an in-use file or a legitimate exe misclassified as junk) is the single worst outcome — it turns a portfolio asset into a liability and a bad first impression for exactly the audience it's meant to impress.
- An unsigned executable triggering Windows SmartScreen ("Windows protected your PC") *will* happen (self-signed cert, no reputation yet) — if undocumented, a recruiter may bail at that screen rather than click "Run anyway." This has to be addressed head-on in the README, not hidden.
- Requesting admin elevation for the whole app (rather than only the operations that need it) reads as sloppy to anyone who's done Windows systems work.
- Silently phoning home (telemetry, analytics) in a "diagnostics/cleanup" tool would undercut the trust story entirely — must be opt-in and disclosed if it exists at all.

### Cost
- One more repo to maintain and version independently of the site.
- A Windows-only CI runner (GitHub Actions `windows-latest`) adds build minutes cost, though within free-tier limits for a public repo.
- Ongoing: keeping the junk-file rule set current as Windows/browser cache layouts change over time.

---

## 4. Gap analysis (current state → target state)

| Area | Current state | Target state | Action |
|---|---|---|---|
| Native Windows app | None (only Electron: `epitome-desktop`) | WinUI 3 / .NET 8 native app | New repo, new stack |
| Filesystem-safety pattern | Not demonstrated anywhere in the portfolio | Quarantine-before-delete, restore, typed-confirmation purge | Core design pillar (ADR-0002) |
| Code signing / trusted distribution | Unknown/likely absent for `epitome-desktop` | Self-signed Authenticode cert + documented SmartScreen click-through, path to EV cert noted | ADR-0003 |
| CI producing a native installer | None (existing CI is FTP/Worker deploy + Electron builder in a separate repo) | GitHub Actions `windows-latest` building/testing/signing an MSIX + portable exe, attached to Releases | New repo's CI |
| Security documentation | No `SECURITY.md` or threat model anywhere in the org | `SECURITY.md` with explicit scope statement, data-handling policy, and threat model | New repo docs |
| ADRs in a public repo | Not currently practiced anywhere in the org (per repo survey) | This ADR set, checked in from day one | `docs/adr/` |
| Site cross-link | Site links to `epitome-desktop` only | Site links to both desktop apps, from a Technologist-appropriate entry point | Small follow-up PR to the site repo |
| Test coverage story | 490+ tests exists for the site; no equivalent for a native app yet | Core logic (`TheWarden.Core`) target ≥90% line coverage via xUnit, matching the site's public testing bar | New repo's test suite |

---

## 5. Tech stack

| Layer | Choice | Why |
|---|---|---|
| Runtime | .NET 8 (LTS) | Modern, first-party Windows support, strong tooling, AOT/self-contained publish for a single-file exe |
| UI framework | WinUI 3 (Windows App SDK) | First-party native Windows UI; genuinely new to the portfolio (vs. the existing Electron app); direct access to Win32/WinRT APIs needed for accurate temp-file, Recycle Bin, and system-health data |
| Architecture | MVVM via `CommunityToolkit.Mvvm` | Keeps the scan/quarantine engine (`TheWarden.Core`) UI-agnostic and independently unit-testable |
| Core engine | `TheWarden.Core` class library, no UI dependencies | Filesystem walker, declarative rule engine (JSON/YAML rule packs), quarantine manager, hashing (SHA-256) — this is the library that carries the test-coverage number |
| Detection | Local heuristic rule packs + **optional, opt-in** VirusTotal hash-reputation lookup (user supplies their own free API key, stored via Windows Credential Locker, never bundled/hardcoded) | Realistic, honest scope — see ADR-0002 |
| System/health data | WMI + Windows Shell/Storage APIs | Disk usage by category, startup impact, junk breakdown — the "PC health dashboard" framing that makes this a *diagnostics* tool, not just a delete-button |
| Logging | Serilog, local rotating file only | Full auditability of every scan/quarantine/restore/purge action, zero network telemetry by default |
| Packaging | Self-contained `win-x64` single-file publish + MSIX package | MSIX gives future auto-update via App Installer; portable exe covers users who don't want an installer |
| Signing | Self-signed Authenticode cert in CI secrets (documented reputation-building path to a paid EV cert) | ADR-0003 |
| Testing | xUnit + coverage collector on `TheWarden.Core`; smoke-level UI tests optional | Matches the site's existing public testing bar |
| CI/CD | GitHub Actions, `windows-latest` matrix: restore → build → test → coverage → sign → publish to Release | Mirrors the rigor of the existing site's `deploy.yml` |

**Why not Tauri/Rust or Electron** (both seriously considered): Tauri would have been the stronger *pure differentiation* play (memory-safe Rust core, tiny binary) and Electron the fastest to ship and consistent with `epitome-desktop` — but WinUI 3/.NET was the chosen direction because first-party WinRT/Win32 access produces materially more accurate temp-file, Recycle Bin, and system-health data than either alternative, without a native-interop layer bolted on. Recorded as the live decision in ADR-0001; the Tauri path is kept in the ADR as the documented alternative in case a v2 rewrite is ever justified.

---

## 6. Recruiter-standout plan

- **README** with build/coverage badges, an architecture diagram, and a short GIF of a real scan → quarantine → restore cycle.
- **`SECURITY.md`** stating plainly what the tool does and does *not* claim to do, its data-handling policy (local-only, no telemetry, BYO API key), and a short threat model.
- **`docs/adr/`** checked in from the first commit — this document set. ADRs in a small solo-dev repo are an above-the-curve signal.
- **CI badge + coverage badge** on `TheWarden.Core`, targeting ≥90% to match the site's existing public number.
- **`CONTRIBUTING.md`** and issue templates, even though this is a solo project — signals the author knows what a maintained OSS repo looks like.
- **Explicit SmartScreen callout** in the README with a screenshot, so a recruiter isn't surprised or scared off at first run.
- **Site cross-link**, following the exact `epitome-desktop` header/dashboard pattern (see ADR-0004).

---

## 7. Proposed repo layout

```
the-warden/
  TheWarden.sln
  src/
    TheWarden.Core/          # pure C#, no UI deps — the tested engine
      Scanning/
      Rules/
      Quarantine/
      Reputation/            # optional VirusTotal client
      Health/                # WMI-backed disk/startup metrics
    TheWarden.App/           # WinUI 3 shell, MVVM views/viewmodels
  tests/
    TheWarden.Core.Tests/
  docs/
    adr/
      0001-desktop-ui-and-runtime.md
      0002-detection-and-safety-model.md
      0003-distribution-and-code-signing.md
      0004-repo-separation-and-website-integration.md
  .github/workflows/ci.yml
  SECURITY.md
  CONTRIBUTING.md
  README.md
```

---

## 8. Next steps (not yet executed)

1. Create the `WhymzikalZyxxyZ/the-warden` GitHub repo.
2. Scaffold the solution per §7.
3. Stand up CI (build + test on every push; sign + release on tag).
4. Small follow-up PR to the site repo adding a Technologist-section download CTA, mirroring commit `6f2cdc5`.

None of the above has been done yet — this document and the four ADRs are the design deliverable requested before development starts.
