# ADR-0002: Detection & Deletion Safety Model

**Status:** Accepted
**Date:** 2026-08-10

## Context

The original request was for a tool that "removes malicious or temporary files." Temp-file cleanup is a well-understood, low-risk problem. "Removes malicious files" is a much bigger claim: a real signature/heuristic malware-detection engine is a multi-year effort even for funded teams, and a solo project that quietly ships something weaker than that but markets it as "malware removal" creates real liability (false positives deleting legitimate files) and real credibility risk (a technical reviewer — e.g. a recruiter with security background — will see through an overstated claim immediately, which is worse than not making it).

There's also a sharper irony risk: a poorly-signed, aggressively-deleting "cleaner" tool is exactly the shape of software that legitimate antivirus products flag as a PUP (potentially unwanted program). Shipping something that gets Defender-flagged on a recruiter's machine would be actively worse than not shipping at all.

## Decision

Scope the product honestly, in two tiers:

1. **Junk/temp classification (core feature, always on):** a declarative, versioned rule pack matches known-safe categories — Windows Temp, `%LOCALAPPDATA%\Temp`, browser caches, thumbnail cache, Windows Update leftovers, crash dumps, old Recycle Bin contents, etc. This is deterministic and low-risk.
2. **Threat-reputation flagging (opt-in, not on by default):** for executables/scripts in commonly-abused locations (e.g. Startup folders, Temp, Downloads), compute a SHA-256 hash and, only if the user has supplied their own free VirusTotal API key, look up its reputation. This is presented as a **flag for the user's own judgment**, never as an automatic deletion trigger.

**No operation ever hard-deletes a file directly.** Every removal — junk or flagged — moves the file into a structured, timestamped **Quarantine** folder with a manifest recording original path, reason, and timestamp. Quarantined items can be restored with one click. A separate, explicit **Purge Quarantine** action (deleting items permanently) requires the user to type a literal confirmation phrase before it executes, and only operates on items already sitting in quarantine past a user-configurable age.

The product is marketed and documented as a **disk-hygiene and triage tool with optional threat-reputation flagging** — explicitly not an antivirus or malware-removal product. `SECURITY.md` states this boundary in plain language.

## Consequences

- Cannot claim "malware removal" in the marketing sense. This is a deliberate, disclosed limitation, not an oversight.
- The quarantine/restore/typed-purge system adds real engineering scope beyond a simple delete button — this is accepted because it is also the project's strongest differentiator and interview talking point.
- Reduces legal and reputational liability substantially versus an auto-delete model.
- VirusTotal API key is user-supplied and stored via Windows Credential Locker (DPAPI-backed) — never bundled, never transmitted anywhere except VirusTotal's API directly from the user's machine.
- Rule packs (what counts as "junk") are versioned and user-editable, so incorrect classifications are correctable without a code change.
