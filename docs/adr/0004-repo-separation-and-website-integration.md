# ADR-0004: Repository Separation & Website Integration

**Status:** Accepted
**Date:** 2026-08-10

## Context

The existing site has one precedent for a downloadable desktop app: `epitome-desktop` lives in its own repository (not inside the main `ZyxxyzWhymzykalWunderland` monorepo-style site), and the site links out to its GitHub Releases page from two touchpoints — a header ghost button (hidden on mobile) and a featured banner CTA on the dashboard (commit `6f2cdc5`), both opening in a new tab with `rel="noopener noreferrer"`.

## Decision

- The Warden ships as a **new, standalone repository**: `WhymzikalZyxxyZ/the-warden`. No application code is added to the main site repo.
- The site repo gets a **small, additive follow-up PR** (on its own `version/X.Y.Z` branch, per the site's established branching convention) adding a download entry point for The Warden. Given the site's section structure, this belongs in the **Technologist** section (`technologist/systemoperator` is the closest existing thematic neighbor — a PC-facing systems tool), with a link to `github.com/WhymzikalZyxxyZ/the-warden/releases`, `rel="noopener noreferrer"`, `target="_blank"`.
- The Warden's own README links back to the personal site, and vice versa, so either entry point leads a visitor to the full picture.

## Consequences

- Keeps the site's existing CI (`deploy.yml`: lint + test + FTP deploy) completely untouched — no new build steps, no new deploy risk to the live site.
- Two independently versioned repos need manual cross-reference upkeep (e.g. updating the site's CTA copy/link if The Warden's release cadence or URL structure changes) — acceptable given the low frequency of that change.
- Establishes a repeatable pattern: future downloadable tools follow the same "own repo + site CTA" shape rather than each inventing a new integration approach.
- The site-side change is intentionally minimal (a link, following an already-reviewed pattern) — it is not expected to need its own ADR, just a PR through the standard process (Changes/Decisions/Code Survey/Test plan body).
