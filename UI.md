# Plan — MBW Design Reference Document

## Context
The user is happy with the MBW (MailBlast Workspace) desktop UI built in `src/App.tsx`. They want a
Markdown reference document saved in the project that codifies the design system and conventions, so
future coding sessions (theirs or an agent's) stay consistent with the established Windows 11 / Fluent
look. This is a documentation-only task — no application code changes.

## Deliverable
Create a reference file: **`docs/DESIGN-GUIDE.md`**

It documents the design language already implemented in `src/App.tsx` and `src/index.css`, written as
"how to build MBW screens" guidance. Crucially, it must be detailed enough that **each page can be built
one-by-one from the doc alone** — every page gets its own section covering its exact layout, the tokens/
colors it uses, and its typography. If the combined doc becomes very long, it may be split into
`docs/DESIGN-GUIDE.md` (foundations) + `docs/PAGES.md` (per-page specs), cross-linked.

## Part A — Foundations (shared by every page)
1. **Design philosophy** — restrained Windows 11 / Fluent productivity tool; the "do NOT" list
   (no gradients, glassmorphism, rounded SaaS cards, giant icons, AI-dashboard vibes). Mirrors the
   original brief so the guardrails stay in the repo.
2. **App shell layout** — the four regions and their fixed roles with exact dimensions: title bar (h-9),
   explorer sidebar (248px), content surface (flex-1, `bg-win-surface`), status bar (h-6, accent blue).
   The flex/grid structure and where each page renders (inside `<main>`).
3. **Color tokens** — full table of `--color-win-*` from `src/index.css` with hex value + purpose +
   the Tailwind class (`bg-win-*`, `text-win-*`, `border-win-*`): bg, surface, sidebar, titlebar,
   hover/active/selected, text/muted/faint, border/border-strong/input, accent (+hover/fg), status,
   good/warn/bad (+bg pairs). Include a "when to use which" cheat-sheet.
4. **Typography** — font stacks (Segoe UI Variable → Inter via Google Fonts; Cascadia/mono for data),
   base 13px, the exact size scale used (11 label / 12 secondary / 13 body / 14 editor / 15 page title /
   24 stat), weights (400/500/600/700), and where `font-mono` + `tabular-nums` apply. A "type role"
   table: page title, section header, field label, body, caption, data cell, stat number.
5. **Spacing, borders, radius, elevation** — radii (4px controls, 6px panels, 8px dialogs), hairline
   `border-win-border` vs `border-win-border-strong`, control heights (h-7/h-8), page paddings, panel
   header height, dialog shadows.
6. **Reusable component recipes** — every primitive in `App.tsx` with its exact class recipe so pages
   reuse them, not reinvent: `Button` (default/accent/subtle), `Select`, `inputCls`, `Field`, `Badge`,
   `Radio`, `Check`, `PageHead`, `SidebarItem`, `Divider`, `Meta`, `VarChip`, status/preview rows,
   plus the 16px icon convention.
7. **Interaction & state conventions** — hover/active/selected states + accent rail, focus ring, auto-
   hiding scrollbars, view switching, overlay vs modal patterns.
8. **UX principles & content conventions** — the 10 brief principles; realistic Indonesian mail-merge
   data; `{Variable}` chip style.

## Part B — Per-page specs (one section per page, build-ready)
Each page section follows the SAME template so a page can be built end-to-end from it:
- **Purpose** — one line.
- **Layout** — ASCII/structural sketch + container widths (e.g. `max-w-[860px] mx-auto`), the `PageHead`
  title/subtitle/actions, and section-by-section breakdown.
- **Colors used** — which tokens for surfaces, borders, accents, status on that page.
- **Typography used** — which type roles appear and where.
- **Components used** — which shared recipes from Part A.
- **State/interactivity** — the React state it owns and behaviors.

Pages/screens to document (matching `src/App.tsx`):
1. Title bar + menus (File/Edit/View/Workspace/Help, SMTP pill, window controls)
2. Explorer sidebar (workspace name, nav items + ✓ state, expandable Configuration)
3. Status bar
4. **Email** editor (subject, toolbar, contentEditable body, Insert Variable dropdown)
5. **Database** (meta bar, search, spreadsheet table)
6. **Attachments** (mode radios, folder/file grid, drop zone, match badges)
7. **Matching** config (column + pattern, result panel + progress bar)
8. **Rename** config (pattern input, live preview table)
9. **Sending** config (fields, range, delay, checkboxes, Preview/Send)
10. **Email Preview** overlay (recipient/subject/attachment header, body, prev/next nav)
11. **Sending Progress** overlay (progress bar, live log, pause/stop)
12. **Send Complete** result (success/failed stats, log, export/back)
13. **SMTP Settings** modal dialog

## Notes
- Content is derived strictly from the existing implementation; no new tokens or components are invented.
- `docs/` directory will be created as part of writing the file(s).
- Split into `DESIGN-GUIDE.md` (Part A) + `PAGES.md` (Part B) only if a single file gets unwieldy;
  otherwise keep as one file with both parts.

## Verification
- Open `docs/DESIGN-GUIDE.md` and confirm token names/values match `src/index.css` and the component
  recipes match `src/App.tsx`.
- No build/typecheck needed — documentation only.

