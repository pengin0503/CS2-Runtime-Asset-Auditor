# GitHub Pages Project Site Design

## Goal

Publish a small Japanese-first public project page that helps Cities: Skylines II players understand what CS2 Runtime Profiler measures, how to install it, and which claims remain unverified.

## Audience and scope

- Audience: people considering or installing the public CS2 Runtime Profiler mod.
- The site is a concise project overview, not a replacement for the repository README or validation matrix.
- Include purpose, features, evidence/confidence rules, limitations, installation link, runtime-validation status, and links to source and issues.
- Clearly state that actual in-game validation remains unverified where the repository says it has not run.

## Design

- Japanese language and copy, matching the current README and in-game interface.
- Responsive, semantic HTML and CSS with visible keyboard focus and a skip link.
- Static files under `site/`; do not publish the whole `docs/` tree.
- No JavaScript, tracking, analytics, external fonts, or externally hosted assets.
- Deploy the `main` branch's `site/` directory to GitHub Pages through GitHub Actions using minimal job permissions.
- Link the repository README as the authoritative installation guide.
- Disclose the known Issue #8 behavior where unmeasured Job/Burst time can appear as `0 ms`; do not claim the runtime currently represents every unavailable value correctly.
- Disclose the known Issue #9 mismatch where the current system detail UI labels Full marker timing as Managed.

## Content accuracy rules

- Describe the mod as read-mostly and do not imply it changes gameplay or disables mods.
- Distinguish direct Full marker timing from Managed synchronous fallback.
- State that ambiguous marker attribution is omitted and Job/Burst worker cost is not guessed onto managed systems.
- Describe normal monitoring and Deep Capture as separate collection modes without claiming low overhead before in-game measurement.
- Gate the deploy job to `main`, including manual workflow dispatches.
- Do not describe overhead targets or in-game scenarios as achieved unless validation evidence is recorded.

## Acceptance criteria

1. Pages builds from `main` and only publishes `site/`; manual dispatch from other refs cannot deploy.
2. Page is usable on narrow and wide viewports and exposes meaningful headings and navigation.
3. All project links point to this repository or its GitHub Pages URL.
4. Public content does not contain private files, local paths, user reports, or unverified runtime claims.
5. README links to the live project site.
