# MBW Design Guide

Source of truth: `MBW.App/Views/*.xaml`, `MBW.App/MainWindow.xaml`, `MBW.App/App.xaml`.

This guide codifies the MBW WinUI 3 UI language. All screens must use **Fluent Design `ThemeResource` tokens** and built-in control styles from `XamlControlsResources` — not custom hex palettes or hand-styled flat controls.

Reference implementation: `MBW.App/Views/EmailEditorPage.xaml`.

---

## Part A — Foundations

### 1) Design philosophy

MBW is a native Windows 11 productivity tool built with WinUI 3.

**Principles:**
1. Function first; decoration second.
2. Use system theme tokens — colors adapt to light/dark mode automatically.
3. Prefer built-in control styles (`DefaultButtonStyle`, `AccentButtonStyle`, `SubtleButtonStyle`) over manual `Background="Transparent"`.
4. Clear typography hierarchy with Fluent text fill colors.
5. Card-based panels for grouped content.
6. Consistent control height and spacing rhythm.
7. Clear states (hover, pressed, selected) via WinUI templates — not custom hover colors.
8. Fast scanning for tabular/operational data.
9. Modal/overlay only for focused tasks.
10. Content realism (Indonesian mail-merge examples, real field names).

**Do NOT:**
- Define custom `Win*` color brushes or hardcoded hex values in XAML.
- Use flat manual buttons without a WinUI style.
- Use a solid blue status bar (legacy desktop pattern).
- Mix React/Tailwind token names with WinUI resources.

### 2) App shell layout

Root shell in `MainWindow.xaml`:
- Root: `ApplicationPageBackgroundThemeBrush`
- **Title bar**: `ShellTitleBarHeight` (36px), `LayerFillColorDefaultBrush`
- **Body split**: border-top `DividerStrokeColorDefault`
  - **Explorer sidebar**: `ShellSidebarWidth` (248px), `CardBackgroundFillColorDefault`, border-right `DividerStrokeColorDefault`
  - **Content surface**: `Frame` with `ApplicationPageBackgroundThemeBrush`
- **Status bar**: auto height, `CardBackgroundFillColorDefault`, border-top `DividerStrokeColorDefault`

All page views render inside `<Frame>`:
- Email / Database / Attachments / Matching / Rename / Sending
- Overlays replace main content when active: Preview / Progress / Result

### 3) Color tokens (WinUI 3 ThemeResource)

Always use `{ThemeResource ...}` in XAML. Never use `{StaticResource Win*}`.

| ThemeResource | Purpose | Replaces (old web token) |
|---|---|---|
| `ApplicationPageBackgroundThemeBrush` | Window/page ground | `win-bg`, `win-surface` |
| `LayerFillColorDefaultBrush` | Title bar, layered chrome | `win-titlebar` |
| `CardBackgroundFillColorDefault` | Sidebar, cards, panels, status bar | `win-sidebar`, `win-surface` |
| `CardStrokeColorDefault` | Card/panel borders | `win-border-strong` |
| `DividerStrokeColorDefault` | Hairline separators | `win-border` |
| `TextFillColorPrimary` | Primary text, labels | `win-text` |
| `TextFillColorSecondary` | Secondary text, hints, status | `win-muted` |
| `TextFillColorTertiary` | Tertiary/meta/caption text | `win-faint` |
| `AccentFillColorDefaultBrush` | Primary accent fill (app mark, CTA) | `win-accent` |
| `AccentFillColorSecondaryBrush` | Selected nav item background | `win-selected` |
| `SubtleFillColorSecondaryBrush` | Workspace chip, subtle fills | `win-active`, `win-hover` |
| `SystemFillColorSuccess` | Success dot, done checkmarks | `win-good` |
| `SystemFillColorCritical` | Error states | `win-bad` |
| `SystemFillColorCaution` | Warning states | `win-warn` |

**When to use which:**
- Page/content background: `ApplicationPageBackgroundThemeBrush`
- Structural chrome (title bar): `LayerFillColorDefaultBrush`
- Sidebar, cards, status bar: `CardBackgroundFillColorDefault`
- Dividers: `DividerStrokeColorDefault`; card frames: `CardStrokeColorDefault`
- Text: Primary → Secondary → Tertiary
- Selected navigation: `AccentFillColorSecondaryBrush`
- Primary actions: `AccentButtonStyle`; secondary: `DefaultButtonStyle`; low-emphasis: `SubtleButtonStyle`

### 4) Typography

Font family is **Segoe UI Variable** (WinUI default — do not set `FontFamily` unless using monospace).

**Scale:**

| Role | Size | Weight | Foreground |
|---|---|---|---|
| Page title | 24px | SemiBold | `TextFillColorPrimary` |
| Page subtitle | 13px | Normal | `TextFillColorSecondary` |
| Section / field label | 14px | SemiBold | `TextFillColorPrimary` |
| Body / nav label | 13px | Normal/Medium | `TextFillColorPrimary` |
| Caption / meta / status | 12px | Normal | `TextFillColorSecondary` |
| Sidebar section label | 11px | Bold | `TextFillColorSecondary` |
| Helper / tertiary | 12px | Normal/Italic | `TextFillColorTertiary` |
| Stat number | 24px | SemiBold | `TextFillColorPrimary` |

Use monospace (`Cascadia Mono` / `Consolas`) for IDs, counts, filenames, and `{Variable}` tokens.

### 5) Spacing, borders, radius

**Shell dimensions** (defined in `App.xaml` as layout constants only):
- Title bar height: 36px
- Sidebar width: 248px
- Status bar: auto height with `Padding="12,8"`

**Page spacing** (from `EmailEditorPage`):
- Page header: `Padding="20,16"`
- Main content: `Padding="20"`
- Card/toolbar: `Padding="12"`, `CornerRadius="4"`
- Card border: `BorderThickness="1"`, `CardStrokeColorDefault`

**Control sizing:**
- Default button: WinUI `DefaultButtonStyle` / `AccentButtonStyle` (do not hardcode height unless needed)
- TextBox: `MinHeight="36"`, `Padding="12,8"`
- Nav item button: `Height="32"`, `Padding="10,0"`
- Config sub-nav: `Height="28"`, `Padding="14,0"`

### 6) Reusable component patterns (WinUI 3)

| Pattern | Implementation |
|---|---|
| Page header | Grid with title (24px) + subtitle (13px), actions right-aligned, bottom border `DividerStrokeColorDefault` |
| Primary action | `Style="{StaticResource AccentButtonStyle}"` |
| Secondary action | `Style="{StaticResource DefaultButtonStyle}"` |
| Menu / low-emphasis button | `Style="{StaticResource SubtleButtonStyle}"` |
| Field label + input | Label `TextBlock` (14px SemiBold) + `TextBox` with `PlaceholderText` |
| Card panel | `Border` or `Grid` with `CardBackgroundFillColorDefault`, `CardStrokeColorDefault`, `CornerRadius="4"` |
| Toolbar separator | `<AppBarSeparator />` |
| Sidebar nav item | `SubtleButtonStyle` button, selected → `AccentFillColorSecondaryBrush` background |
| Status bar segment | `TextBlock` 12px, `TextFillColorSecondary` |
| Variable chip | Mono text in card with accent border |
| Icons | `SymbolIcon` (16px convention), `Foreground="{ThemeResource TextFillColorSecondary}"` |

### 7) Interaction & state conventions

- **Hover/pressed**: Provided by WinUI button/control templates — do not hand-roll hover colors.
- **Selected nav item**: `AccentFillColorSecondaryBrush` background; text `TextFillColorPrimary`.
- **Inactive nav item**: Transparent background; text/icons `TextFillColorSecondary`.
- **Done indicator**: `SymbolIcon Symbol="Accept"`, `Foreground="{ThemeResource SystemFillColorSuccess}"`.
- **Focus**: WinUI default focus visuals on controls.
- View switching: `Frame.Navigate()` from `MainWindow`.
- Configuration group: expandable panel in sidebar.

**Code-behind theme brushes** — resolve from `Application.Current.Resources`:

```csharp
// Selected nav background
Application.Current.Resources["AccentFillColorSecondaryBrush"] as Brush

// Text
Application.Current.Resources["TextFillColorPrimary"] as Brush
Application.Current.Resources["TextFillColorSecondary"] as Brush
```

### 8) UX principles & content conventions

- Use realistic Indonesian mail-merge data and field names (`Nama`, `NIM`, `Program_Studi`).
- Always show operational status (SMTP, rows, attachment matching).
- Keep workflow linear: Email → Data → Attachments → Config → Sending.
- Variables are represented as `{Variable}` tokens.

---

## Part B — Per-page build specs

Template: Purpose, Layout, ThemeResources, Typography, Components, State.

### 1) Title bar + menus

**Purpose:** App identity, top-level menu access, SMTP quick status.

**Layout:**
- `Grid` height 36, `LayerFillColorDefaultBrush`
- Left: app mark (`M` tile, `AccentFillColorDefaultBrush`) + `MBW` label
- Center-left: menu buttons File/Edit/View/Workspace/Help
- Right: SMTP status pill

**ThemeResources:** `LayerFillColorDefaultBrush`, `TextFillColorPrimary`, `TextFillColorSecondary`, `AccentFillColorDefaultBrush`, `SystemFillColorSuccess`

**Typography:** App name 13px SemiBold; menu 13px; SMTP 12px Secondary

**Components:** `SubtleButtonStyle` for menu and SMTP buttons

### 2) Explorer sidebar

**Purpose:** Workspace context and workflow navigation.

**Layout:**
- Width 248px, `CardBackgroundFillColorDefault`, right border `DividerStrokeColorDefault`
- Section label "WORKSPACE" (11px Bold, Secondary)
- Active workspace chip: `SubtleFillColorSecondaryBrush`, `CornerRadius="4"`
- Main nav: Email, Database, Attachments
- Expandable Configuration group with children
- Footer meta line (11px Tertiary)

**ThemeResources:** `CardBackgroundFillColorDefault`, `DividerStrokeColorDefault`, `AccentFillColorSecondaryBrush` (selected), `SubtleFillColorSecondaryBrush` (workspace chip), `SystemFillColorSuccess` (done checks)

**Components:** `SubtleButtonStyle` nav buttons, `SymbolIcon`, `FontIcon` chevron

### 3) Status bar

**Purpose:** Persistent runtime status summary.

**Layout:**
- Auto height, `Padding="12,8"`
- `CardBackgroundFillColorDefault` background, top border `DividerStrokeColorDefault`
- Segmented status text + right-aligned "Ready"

**ThemeResources:** `CardBackgroundFillColorDefault`, `DividerStrokeColorDefault`, `TextFillColorSecondary`

**Typography:** 12px Secondary; "Ready" SemiBold

### 4) Email editor

**Purpose:** Compose reusable personalized email template.

**Reference:** `EmailEditorPage.xaml`

**Layout:**
- Page header (20,16 padding) + Preview / Continue actions
- Two-column body: editor left, variables panel right (280px)
- Subject field, formatting toolbar card, WebView2 editor card
- Page status bar at bottom

**ThemeResources:** Full Fluent card/text/button token set

**Components:** `DefaultButtonStyle`, `AccentButtonStyle`, `TextBox`, `ComboBox`, `ListView`, `MenuFlyout`, `AppBarSeparator`, `WebView2`

### 5) Database

**Purpose:** Inspect source rows and columns from imported sheet.

**Layout:**
- Page title 24px + subtitle
- Action buttons, meta strip, searchable table
- Card-wrapped empty state

**ThemeResources:** Same page pattern as Email editor

### 6) Attachments

**Purpose:** Configure attachment source and matching mode.

**Layout:**
- Page header + card panels for mode selection and file grid

### 7) Matching config

**Purpose:** Define DB-to-filename mapping rule.

**Layout:**
- Page header + card with fields and result panel

### 8) Rename config

**Purpose:** Preview rename pattern output.

**Layout:**
- Page header + fields + preview table in card

### 9) Sending config

**Purpose:** Final sending parameters and launch.

**Layout:**
- Page header + form fields + accent Send button

### 10) Email Preview overlay

**Purpose:** Verify recipient-level personalization.

**Layout:**
- Full content area, centered card `max-width ~760px`

### 11) Sending Progress overlay

**Purpose:** Live send progress.

**Layout:**
- Centered card with `ProgressBar`, log list, Pause/Stop actions

### 12) Send Complete result

**Purpose:** Final outcome and next actions.

**Layout:**
- Centered card, stat numbers 24px, log list, Export/Back actions

### 13) SMTP Settings dialog

**Purpose:** Configure SMTP transport.

**Layout:**
- `ContentDialog` or modal with `DefaultButtonStyle` / `AccentButtonStyle` footer

---

## XAML checklist (every new screen)

- [ ] `Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"` on `Page`
- [ ] Text uses `TextFillColorPrimary` / `Secondary` / `Tertiary` — no hardcoded colors
- [ ] Buttons use `DefaultButtonStyle`, `AccentButtonStyle`, or `SubtleButtonStyle`
- [ ] Cards use `CardBackgroundFillColorDefault` + `CardStrokeColorDefault`
- [ ] Dividers use `DividerStrokeColorDefault`
- [ ] No `Win*` static resources
- [ ] No `Foreground="White"` except on accent-filled surfaces

---

## Migration note

The previous React prototype used custom CSS tokens (`win-bg`, `win-accent`, etc.). Those are **deprecated**. The WinUI app uses Fluent `ThemeResource` tokens exclusively so the UI stays native, theme-aware, and consistent with `EmailEditorPage`.
