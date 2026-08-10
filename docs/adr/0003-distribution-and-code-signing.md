# ADR-0003: Distribution, Code Signing & Update Strategy

**Status:** Proposed
**Date:** 2026-08-10

## Context

The Warden needs to be downloadable as an executable from GitHub Releases and linked from the personal site, matching the existing `epitome-desktop` pattern. Unsigned or unrecognized executables trigger Windows SmartScreen ("Windows protected your PC"), which is a real drop-off risk for the highest-value audience (a recruiter clicking through and expecting a smooth first run). A paid EV code-signing certificate (the fastest way to avoid SmartScreen entirely) is a recurring cost not justified for a portfolio project's current stage.

## Decision

- CI produces two artifacts per release: a self-contained **single-file `win-x64` executable** (no install required — best for a quick recruiter trial) and an **MSIX package** (best for a "real install" with future auto-update via App Installer).
- Both are signed with a **self-signed Authenticode certificate** stored in GitHub Actions secrets (never committed to the repo).
- The README documents the SmartScreen click-through explicitly, with a screenshot, framed as "why you're seeing this and what it means" rather than hiding it — turning an awkward moment into a demonstration of transparency.
- The path to a paid EV certificate (which builds SmartScreen reputation immediately) is documented as a future upgrade, not attempted now.
- MSIX gives an update path for later versions without requiring a full reinstall.

## Consequences

- First run still shows a SmartScreen prompt — self-signed certificates do not build reputation the way a paid EV cert does. This is a known, accepted limitation, called out proactively in the README rather than discovered by surprise.
- No recurring certificate cost at this stage.
- Two build targets (portable exe + MSIX) roughly double the packaging/test surface in CI, but cover both "just let me try it" and "install it properly" audiences.
- Release automation (GitHub Actions tag-triggered build → sign → attach to Release) mirrors the rigor of the existing site's `deploy.yml`, keeping the org's CI quality bar consistent across repos.
