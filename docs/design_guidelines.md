# Design Guidelines

**Purpose**: Lock UX decisions, define the color system, and ensure the app meets Windows Store certification for themes and accessibility.

**Design Principles**: Low visual noise, state-driven, readable at a glance, non-distracting. Aligned with Windows Fluent design.

---

## Locked UX Decisions

### Visibility Model

- **Hybrid**: Main window for setup, Kanban, and review; small overlay for real-time focus status.
- The overlay is the primary "always on" surface; the main window is for configuration and task management.

### Warnings

- **Passive by default**: Status changes (e.g. overlay color) indicate alignment; no interruption.
- **Optional toast**: After prolonged misalignment (e.g. X minutes), an optional toast can notify the user. Configurable.

### Default State

- **Neutral**: When monitoring starts and no classification has run yet, the state is neutral (no green/red).
- Green only when clearly aligned; red/amber used sparingly for misaligned.

---

## Design Specifications

### Materials (Glassy UI)

The app uses semi-transparent, blurred surfaces (glassy/acrylic) for a non-solid, depth-infused look. Light and Dark themes use Fluent materials; High Contrast uses solid colors for accessibility.

| Surface | Light/Dark | High Contrast |
|---------|------------|---------------|
| **Window** | `DesktopAcrylicBackdrop` — frosted-glass window backdrop | Same (system-managed) |
| **Page background** | `AcrylicBrush` — tint from elevation color, TintOpacity ~0.65, FallbackColor when acrylic unavailable | `SolidColorBrush` (FbPageBackground) |
| **Column background** | `AcrylicBrush` — tint from elevation color, TintOpacity ~0.65 | `SolidColorBrush` (FbColumnBackground) |
| **Card background** | `AcrylicBrush` — tint from elevation color, TintOpacity ~0.7 | `SolidColorBrush` (FbCardBackground) |

- **Tint colors** match the elevation palette (see Color Palette). FallbackColor equals the same solid color so that when acrylic is not available (e.g. window inactive, policy), the UI still matches the elevation layers.
- **High Contrast**: All surface brushes for page, column, and card remain `SolidColorBrush` using theme colors; no acrylic, to preserve contrast and predictability.

### Elevation Layers (Store-Critical)

The UI uses 3 distinct elevation layers for clear visual hierarchy:

| Layer | Role | Dark Mode | Light Mode |
|-------|------|-----------|------------|
| 1 (Darkest) | Page Background | `#110E1A` | `#EDE9F5` |
| 2 (Medium) | Column Background | `#1C1730` | `#F5F3FA` |
| 3 (Lightest) | Card Background | `#2A2242` | `#FFFFFF` |

This ensures:
- Clear separation between page, columns, and cards
- No contrast failures (e.g., white on white)
- Proper depth perception

### Color Palette

**Dark Mode (Primary)**

| Role | Color | Hex |
|------|-------|-----|
| Page Background | Deep Purple | `#110E1A` |
| Column Background | Medium Purple | `#1C1730` |
| Card Background | Light Purple | `#2A2242` |
| Card Border | Border Purple | `#3A3058` |
| Column Border | Column Border | `#2A2444` |
| Primary Accent | Violet | `#A78BFA` |
| Aligned/Active | Green | `#4ADE80` |
| Neutral | Violet | `#A78BFA` |
| Misaligned | Orange | `#FB923C` |
| Text Primary | White | `#FFFFFF` |
| Text Secondary | Gray | `#A1A1AA` |

**Light Mode**

| Role | Color | Hex |
|------|-------|-----|
| Page Background | Light Purple | `#EDE9F5` |
| Column Background | Lighter Purple | `#F5F3FA` |
| Card Background | White | `#FFFFFF` |
| Card Border | Border | `#D8D0E8` |
| Column Border | Border | `#E0DAF0` |
| Primary Accent | Violet | `#7C3AED` |
| Aligned/Active | Green | `#22C55E` |
| Neutral | Violet | `#8B5CF6` |
| Misaligned | Orange | `#F97316` |
| Text Primary | Dark Gray | `#1F1F1F` |
| Text Secondary | Gray | `#6B7280` |

### Typography

- **Font Family**: Segoe UI Variable (WinUI 3 default)
- **Column Headers**: 12px, SemiBold, letter-spacing 80, uppercase
- **Task Titles**: 14px, Medium weight, line-height 20px
- **Secondary Text**: 11px, Regular
- **Button Text**: 12-13px, SemiBold for primary, Normal for secondary

### Spacing and Layout

| Token | Value |
|-------|-------|
| Page Margin | 32px |
| Column Spacing | 16px |
| Card Spacing | 10px |
| Card Padding | 12-14px |
| Column Padding | 16px |
| Button Padding | 16px x 8px (accent), 8px x 4px (card action) |

### Corner Radius

| Token | Value | Usage |
|-------|-------|-------|
| Small | 6px | Buttons, TextBoxes |
| Medium | 10px | Cards |
| Large | 12px | Columns, Panels |

---

## Card Structure

Cards have a clear internal hierarchy:

```
+--+---------------------------+
|  | Status Badge (optional)   |
|A | Task Title               |
|C |                           |
|C | ─────────────────────────|
|E | Actions: [Primary] [Sec] |
|N |                           |
|T |                           |
+--+---------------------------+
```

### Left Accent Bar

- Width: 3px
- Purpose: Explicit state indicator (not relying on column alone)
- Colors:
  - To Do: Neutral (violet `FbNeutralAccentBrush`)
  - In Progress: Active (green `FbAlignedAccentBrush`)
  - Done: Completed (green, 50% opacity)

### Card Content Structure

1. **Status Badge** (optional): Small dot + text (e.g., "Active", "Completed")
2. **Task Title**: Primary text, high contrast, max 3 lines
3. **Divider**: 1px separator line
4. **Actions**: Aligned horizontally, primary action first

### Action Buttons

- **Primary Action** (Start, Done): Accent color text, semibold
- **Secondary Actions** (Stop, Delete): Muted text color
- **Delete**: Uses icon (trash) instead of colored text
- No colored text for destructive actions (avoid relying on color alone)

---

## Column Headers

Headers include:
- **Title**: Uppercase, letter-spaced (e.g., "TO DO", "IN PROGRESS", "DONE")
- **Count Badge**: Item count next to title (e.g., "TO DO 3")
- **Divider**: Subtle line below header separating from task list

This improves scannability and provides context without relying on visual inspection of cards.

---

## State Indicators

State is never implied by column position alone. Explicit indicators:

| State | Left Accent | Badge | Notes |
|-------|-------------|-------|-------|
| To Do | Violet | None | Neutral, waiting |
| In Progress | Green | "Active" + dot | Currently working |
| Done | Green (50% opacity) | "Completed" + checkmark | Finished |

Future states (AI classification):
- Aligned: Green accent bar + "Focused" badge
- Misaligned: Orange accent bar + "Off-task" badge
- Neutral: Violet accent bar (pending classification)

---

## Accessibility and Store Certification

### Rules

- **Never encode meaning with color alone.** Always pair color with:
  - Icon (checkmark, dot, trash icon)
  - Text label ("Active", "Completed", "Delete")
  - Left accent bar position
- **Contrast**: 3 elevation layers ensure sufficient contrast
- **Focus indicators**: WinUI default focus rectangles preserved
- **Keyboard navigation**: All controls accessible via Tab/Enter
- **High Contrast**: Surface backgrounds use solid colors (no acrylic) for predictable contrast

### Store Certification Checklist (UI/UX)

- [ ] **Light mode**: All text readable; clear elevation layers
- [ ] **Dark mode**: All text readable; no unthemed light backgrounds
- [ ] **Contrast**: 3 distinct elevation layers (page < column < card)
- [ ] **Color not sole indicator**: Status always paired with icon/text/accent bar
- [ ] **Scaling**: Usable at 100% and 200% scale
- [ ] **Focus indicators**: Visible for keyboard users
- [ ] **Keyboard**: Main flows work with keyboard
- [ ] **Automation**: `AutomationProperties.Name` on columns and buttons
- [ ] **No excessive glow**: Solid borders, minimal effects; Acrylic/Mica are standard Fluent materials

---

## File Layout

- `Themes/Colors.xaml` — ThemeDictionaries with `Fb*` Color resources only
- `Themes/Brushes.xaml` — Semantic brushes: ThemeDictionaries for surface brushes (AcrylicBrush in Light/Dark, SolidColorBrush in High Contrast); root holds border, accent, state, and text SolidColorBrush resources
- `Themes/Styles.xaml` — Shared styles using semantic brushes
- All merged in `App.xaml` in order: Colors, Brushes, Styles

---

## Style Reference (Styles.xaml)

| Style Name | Target | Usage |
|------------|--------|-------|
| `ColumnStyle` | Border | Kanban column container |
| `CardStyle` | Border | Task card container (no padding, accent bar inside) |
| `FbAccentButtonStyle` | Button | Primary actions (Add Task) |
| `FbOutlineButtonStyle` | Button | Secondary actions (Cancel) |
| `FbCardPrimaryActionStyle` | Button | Primary card action (Start, Done) |
| `FbCardActionButtonStyle` | Button | Secondary card actions (Stop, Delete) |
| `FbColumnHeaderStyle` | TextBlock | Column headers (uppercase) |
| `FbColumnCountStyle` | TextBlock | Item count badge |
| `FbTaskTitleStyle` | TextBlock | Task description text |
| `FbSecondaryTextStyle` | TextBlock | Status badges, metadata |
| `FbDividerStyle` | Border | Separator lines |

### Design Tokens

| Token | Value |
|-------|-------|
| `FbCornerRadiusSmall` | 6px |
| `FbCornerRadiusMedium` | 10px |
| `FbCornerRadiusLarge` | 12px |
| `FbCardPadding` | 14px |
| `FbColumnPadding` | 16px |
| `FbAccentBarWidth` | 3px |
