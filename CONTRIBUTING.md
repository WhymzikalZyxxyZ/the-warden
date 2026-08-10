# Contributing

This is currently a solo-maintained portfolio project, but it's built and
documented like a project that expects contributors — issues and PRs are
welcome.

## Getting set up

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 (19041+) or Windows 11 — `TheWarden.App` is Windows-only by
  design (see [`docs/adr/0001`](docs/adr/0001-desktop-ui-and-runtime.md))
- No separate Windows App SDK install needed — it's pulled via NuGet.

```bash
git clone https://github.com/WhymzikalZyxxyZ/the-warden.git
cd the-warden
dotnet restore
dotnet build TheWarden.sln
dotnet test tests/TheWarden.Core.Tests/TheWarden.Core.Tests.csproj
```

## Project structure

`TheWarden.Core` holds all real logic and has no UI dependency — if you're
changing scan/quarantine/reputation/health behavior, it almost certainly
belongs there, with tests alongside it in
`tests/TheWarden.Core.Tests`. `TheWarden.App` should stay a thin MVVM
coordinator over `TheWarden.Core`.

## Testing expectations

- New logic in `TheWarden.Core` should come with unit tests. The project
  currently sits at ~94% line coverage on `TheWarden.Core` — please don't
  drop it materially.
- Tests that touch real OS state (WMI, live filesystem paths) should go
  through the existing fake interfaces (`IDirectoryReader`,
  `ISystemHealthProbe`, `IClock`, `IEnvironmentExpander`) rather than hitting
  the real OS — see any existing test file for the pattern.
- `QuarantineManagerTests` is the one place that legitimately uses real
  temporary directories, since quarantine is fundamentally a filesystem
  contract worth testing against the real filesystem.

## Rule packs

The built-in junk-classification rules live as data in
[`RulePack.Default()`](src/TheWarden.Core/Rules/RulePack.cs), not scattered
through code. If a rule misclassifies something, the fix is almost always a
change to that rule pack, not new logic.

## Safety-model changes

Any change touching deletion behavior (`QuarantineManager`, elevation
requests, the purge confirmation flow) should reference
[`docs/adr/0002`](docs/adr/0002-detection-and-safety-model.md) and, if it
changes the model materially, come with a new ADR rather than silently
diverging from the documented one.

## Commit / PR style

Plain, descriptive commit messages and PR titles are fine here — this repo
doesn't inherit the personal site's branching/naming conventions. Please do
include a short "why," not just "what," in the PR description.
