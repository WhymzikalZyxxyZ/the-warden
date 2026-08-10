# ADR-0001: Desktop UI & Runtime — WinUI 3 on .NET 8

**Status:** Accepted
**Date:** 2026-08-10

## Context

The Warden needs to run as a native Windows executable with reliable access to the filesystem, Recycle Bin, WMI-based system metrics, and (for some operations) elevated privileges. It also needs to be distributable as a downloadable executable linked from the personal site, alongside the existing `epitome-desktop` Electron app.

Three stacks were seriously evaluated:

1. **Tauri (Rust core + React/TypeScript UI).** Smallest binary, memory-safe core for the filesystem/deletion engine, and would reuse React experience from `epitome/client`. Strongest *portfolio differentiation* option (systems-level Rust is not represented anywhere in the current portfolio).
2. **Electron (TypeScript).** Fastest to build, and consistent with `epitome-desktop`. But a full Chromium+Node process is a heavy, ironic fit for a tool whose entire value proposition is "trustworthy access to your filesystem," and it would not differentiate from the app that already exists.
3. **WinUI 3 on .NET 8 (C#).** First-party Windows UI framework with direct WinRT/Win32 interop. No Node or Chromium runtime bundled. Genuinely new to the portfolio (the existing desktop app is Electron, not native .NET).

## Decision

Build The Warden on **.NET 8 (LTS) with WinUI 3 (Windows App SDK)**, using the MVVM pattern via `CommunityToolkit.Mvvm`.

The scan/quarantine/detection engine lives in a separate class library, `TheWarden.Core`, with zero UI dependencies, so it is independently unit-testable and could in principle back a different UI later without a rewrite.

## Consequences

- **Windows-only.** Acceptable — the product is scoped as a Windows-specific diagnostics tool from the outset.
- **Requires the Windows App SDK runtime**, mitigated by self-contained/AOT publishing so end users don't need to separately install it.
- **First-party WinRT/Win32 access** gives materially more accurate temp-file, Recycle Bin, and system-health data than a cross-platform abstraction layer would, without hand-rolled native interop.
- **Does not diversify the portfolio's language surface** the way a Rust/Tauri build would have — recorded here explicitly so a future v2 rewrite in Rust remains an option if that differentiation becomes a priority later.
- **Testability is preserved** by keeping `TheWarden.Core` UI-free; the coverage target (≥90%) applies to that library specifically.
