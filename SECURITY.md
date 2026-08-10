# Security Policy

## What The Warden is — and isn't

The Warden is a **disk-hygiene and triage tool**: it identifies files in
well-known temporary/cache locations and, optionally, flags executables with
a poor reputation on VirusTotal. It is **not antivirus software** and makes
no claim to detect malware via signatures or heuristics. If you need active
malware protection, use Windows Defender or a dedicated AV product alongside
this tool, not instead of it.

This scope boundary is intentional — see
[`docs/adr/0002-detection-and-safety-model.md`](docs/adr/0002-detection-and-safety-model.md)
for the full reasoning.

## Data handling

- **No network calls by default.** The junk-scanning and quarantine features
  are 100% local — nothing leaves your machine.
- **VirusTotal lookups are opt-in.** They only run if you supply your own
  VirusTotal API key in settings. That key is stored via the Windows
  Credential Locker (DPAPI-backed), never written to disk in plain text,
  never bundled with the app, and never transmitted anywhere except directly
  to VirusTotal's API from your machine.
- **No telemetry, analytics, or crash reporting.** Logs are written locally
  (see below) and never transmitted.
- **Logs** are written to a local rotating log file recording every scan,
  quarantine, restore, and purge action, for your own auditability. They are
  never uploaded anywhere by the app.

## Deletion safety model

Nothing The Warden does is a direct, permanent delete:

1. Files matched by a junk rule or flagged by reputation lookup are **moved**
   to a local Quarantine folder, with a manifest recording the original
   location and the reason.
2. Quarantined files can be **restored** to their original location at any
   time.
3. Permanently deleting a quarantined file requires an explicit **Purge**
   action that only targets items already past a user-configured retention
   window, and requires typing the literal phrase `DELETE` to confirm — there
   is no default or one-click path to irreversible deletion.

## Elevation

The app runs unelevated (`asInvoker`) by default. Operations on
administrator-only locations (e.g. the system Temp directory,
`SoftwareDistribution\Download`) prompt for elevation only when needed, not
app-wide.

## Reporting a vulnerability

If you find a security issue — a path that bypasses quarantine, a privilege
escalation, an injection vector in rule-pack parsing, or anything else —
please email **zyxxyz@zyxwonderland.xyz** rather than opening a public issue.
I'll acknowledge within a few days and credit you in the release notes
(unless you'd prefer otherwise) once it's fixed.
