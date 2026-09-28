# GitHub Pages Project Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish a concise Japanese project page for CS2 Runtime Profiler from the repository's `main` branch.

**Architecture:** Keep the website as standalone static HTML/CSS in `site/`, separate from internal validation and design documents. A GitHub Actions workflow uploads only `site/` to Pages and deploys with minimal permissions; the README points to the published page.

**Tech Stack:** HTML5, CSS, GitHub Actions, GitHub Pages.

**Spec:** `docs/superpowers/specs/2026-09-27-github-pages-project-site-design.md`

## Global Constraints

- Work directly on `main`; do not create another branch or PR.
- Publish only `site/`; do not publish the repository's internal `docs/` tree.
- Use Japanese copy consistent with the README.
- Keep the page static, with no JavaScript, analytics, external fonts, or remote assets.
- Preserve the evidence model: Full is direct marker timing; Managed is synchronous fallback; do not assign Job/Burst worker time by guesswork.
- Keep in-game validation and overhead claims marked unverified until measured.
- Disclose the current Job/Burst zero-display limitation in Issue #8 and Full/Managed detail-label limitation in Issue #9.

## Review Focus

- Workflow never deploys a pull request or publishes files outside `site/`; verify triggers and artifact path.
- Workflow permissions are limited to contents read for build and Pages/id-token write for deploy; inspect the YAML.
- Relative stylesheet and repository links work on the project-page base path; validate every local file link.
- Page text does not imply in-game testing or performance guarantees; compare claims to README and validation matrix.
- Keyboard navigation and narrow layouts remain usable; inspect semantic landmarks, skip link, focus styling, and responsive CSS.

---

### Task 1: Add the static project page and Pages workflow

**Files:**
- Create: `site/index.html`
- Create: `site/styles.css`
- Create: `.github/workflows/pages.yml`
- Modify: `README.md`
- Create: `docs/superpowers/specs/2026-09-27-github-pages-project-site-design.md`
- Create: `docs/superpowers/plans/2026-09-27-github-pages-project-site.md`

**Interfaces:**
- The workflow publishes only the static directory `site/` to GitHub Pages.
- The page links installation and validation details to repository documentation.

- [x] Verify repository claims against `README.md` and `docs/validation/runtime-validation.md`.
- [x] Add the semantic Japanese landing page and responsive styles.
- [x] Add a least-privilege Pages workflow that uploads only `site/` and permits deploys only from `main`.
- [x] Add the project-site link to the README.
- [x] Review page copy against Issues #8–#12; disclose known zero-display and Full/Managed detail-label issues and avoid implying measured low overhead.
- [x] Improve muted-text contrast and retain keyboard and narrow-screen affordances.
- [x] Validate HTML structure, local assets, workflow YAML, page copy, and responsive/keyboard affordances.
- [x] Commit the completed static site and workflow directly to `main`.
- [x] Set Pages to use GitHub Actions as the publishing source.
- [x] Verify the deployment URL after the main commit.

**Verification:**
- Parse `site/index.html` with Python's standard-library HTML parser; verify one `main`, a page title, `lang="ja"`, and that all local asset paths exist.
- Parse `.github/workflows/pages.yml` with an available YAML parser or GitHub Actions validation; verify push trigger is limited to `main` and artifact path is `site/`.
- Run the existing Pure Core and UI checks if their toolchains are available; the site itself has no application runtime or JavaScript tests.
- The local environment does not provide `dotnet`; application tests could not be rerun here. No game/runtime code changed in this site task.
- Inspect the completed Pages deployment and confirm the live page responds at the repository Pages URL.
