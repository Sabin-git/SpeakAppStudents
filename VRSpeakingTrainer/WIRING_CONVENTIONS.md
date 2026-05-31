# Wiring Conventions — Unity UI Primer

Shared reference for every `WIRING_TASK_N.md` in this project. Covers the UI layout primitives that come up repeatedly: Layout Groups, Content Size Fitter, Layout Element, ToggleGroup, ScrollRect.

Future wiring docs should **reference this file** rather than re-explaining these basics. Task-specific wiring docs only describe what's unique to that task.

For modal panel structure (Backdrop + Card pattern, colour palette, hierarchy conventions), see [CLAUDE.md → Modal panel style](CLAUDE.md). This doc covers the *how* of building UI; CLAUDE.md covers the *what* of the project's conventions.

---

## Horizontal Layout Group

Arranges its child UI elements in a horizontal row.

### Create
1. In the Hierarchy, right-click the parent (e.g. `Card`) → **Create Empty**. Empty GameObject with a RectTransform.
2. Rename to match purpose (e.g. `Row_Brightness`, `Row_Language`).
3. With it selected, Inspector → **Add Component** → type `Horizontal Layout Group`.

### Recommended defaults for label-and-control rows

| Field | Value |
|---|---|
| Padding | Left=8, Right=8, Top=4, Bottom=4 |
| Spacing | 12 |
| Child Alignment | Middle Left |
| Control Child Size | Width ☐, Height ☐ |
| Use Child Scale | Width ☐, Height ☐ |
| Child Force Expand | Width ☐, Height ☐ |

**Why these defaults:**
- *Control Child Size = OFF* — children keep their own width/height. Only turn ON if the layout group should force-fit them.
- *Force Expand Width = OFF* — children don't stretch to fill empty space. Turn ON only for the one child you want to absorb the slack.
- *Middle Left* — most label-control rows want left-aligned text, vertically centred.

### Row height

The Row won't have a height by default. Pick one:
- **Fixed height (preferred for static rows):** Rect Transform → set Height to a fixed value (e.g. 40 or 50). Matches the DevPanel pattern — predictable visuals, no surprises if children change. Use this by default.
- **Auto-size to children:** add a **Content Size Fitter** with **Vertical Fit = Preferred Size**. The Row sizes itself to its tallest child plus padding. Only needed when the row's content is genuinely variable-length (e.g. a label that may wrap onto multiple lines).

### Push label and value apart (label-left, value-right)

For rows like `Row_ConsentStatus` (label on left, value on right):

1. Add a child empty GameObject between them named `Spacer`.
2. Add a **Layout Element** component to `Spacer`.
3. Set **Flexible Width = 1**, leave other Layout Element fields at -1 (means "no preferred").

The flexible spacer expands to fill, pushing left and right children apart.

*Alternative:* add **Layout Element** with **Flexible Width = 1** directly on the leftmost label — it expands its allocation, same visual effect.

---

## Vertical Layout Group

Arranges children in a vertical column. Used on `Card` GameObjects to stack subtitle labels, rows, and primary buttons.

### Create
Same as horizontal — Add Component → type `Vertical Layout Group`.

### Recommended defaults for a modal Card

| Field | Value |
|---|---|
| Padding | Left=16, Right=16, Top=16, Bottom=16 |
| Spacing | 12 |
| Child Alignment | Upper Center |
| Control Child Size | Width ☑, Height ☐ |
| Child Force Expand | Width ☑, Height ☐ |

**Why these defaults:**
- *Control + Force Expand Width = ON* — every row and button stretches to the card's full width, giving consistent left/right margins. This matches the DevPanel look.
- *Height stays OFF* — each child decides its own height; the card grows tall to accommodate them.

### Hierarchy order = display order

Children render top-to-bottom in **Hierarchy order**, not position order. Reorder by dragging in the Hierarchy panel.

### Pair with Content Size Fitter (optional)

For Cards with **dynamic or variable content**, add a **Content Size Fitter** on the same Card GameObject with **Vertical Fit = Preferred Size** — the Card auto-grows to fit all its children.

For Cards with **fixed, known content** (the usual case in this project), set Card Width and Height manually on its Rect Transform and skip the Content Size Fitter. This matches the **DevPanel pattern** — simpler, predictable, what to default to. The pre-existing `DevPanel/Card` is the reference template.

---

## Content Size Fitter

Makes a Rect Transform auto-size to match its content's preferred size.

**Default position:** skip it. Most Cards and Rows in this project use fixed sizes set manually on the Rect Transform — that matches DevPanel and keeps visuals predictable. Add Content Size Fitter only when content is genuinely dynamic or variable-length.

### Create
Add Component → `Content Size Fitter`.

### Fields

| Field | Common Value |
|---|---|
| Horizontal Fit | Unconstrained (most cases) |
| Vertical Fit | Preferred Size (for scrollable content) |

### When it's *required*
- **On the `Content` GameObject inside a `ScrollRect/Viewport/Content` chain** — without it, the content can't grow beyond the viewport, so scrolling does nothing. This is the load-bearing case (Privacy Policy body, long consent text).

### When it's *useful but optional*
- On a Row whose label text may wrap to multiple lines (so the Row grows taller automatically). Alternative: keep the text short, set a fixed Row height.
- On a Card whose row count or content changes at runtime (Cards built dynamically by code). For static authored Cards, manual sizing is simpler.

### Gotchas
- Must be combined with a Layout Group on the *same* GameObject for the preferred size to be computed.
- Don't set both Horizontal and Vertical to Preferred Size unless you want the element to size in both axes — usually one is enough.
- If a Card with Content Size Fitter looks wrong, suspect that a child has no preferred height (e.g. a stretched Image) — the Card collapses.

---

## Layout Element

Per-child override for size in a Layout Group. Use to give a specific child a minimum / preferred / flexible width or height that overrides the layout group's defaults.

### Create
Add Component → `Layout Element` on the **child**, not the layout group's parent.

### Fields

| Field | Meaning |
|---|---|
| Min Width / Min Height | Hard lower bound |
| Preferred Width / Preferred Height | Target size when there's enough space |
| Flexible Width / Flexible Height | Share of leftover space relative to siblings (0–1+) |
| Ignore Layout | Removes this child from the layout group's calculations |

### When to use

- **Give a label a fixed minimum width** so columns line up: set Min Width to 100.
- **Make one child fill the leftover space:** set Flexible Width = 1 (others stay at 0 or -1).
- **Reserve space for a slider/value:** set Preferred Width to the slider's natural size.

### Default value `-1`
A field of `-1` means "no preference — use the layout group's default." Don't think of it as zero.

---

## ToggleGroup (for radio buttons)

Makes a set of Toggles behave as mutually-exclusive radio buttons.

### Create
1. Add Component → `Toggle Group` on the **parent** GameObject that contains the toggles (e.g. `Row_Language`).
2. On each child Toggle, drag the parent (with the `Toggle Group` component) into the toggle's **Group** field.

### Settings on ToggleGroup
- **Allow Switch Off** — if checked, the user can click an active toggle to turn it off (no toggle selected). Usually leave **unchecked** for a radio behaviour.

### Gotchas
- If two toggles appear active at the same time, the Group reference is missing on one. Re-check both children.
- The first active toggle in Hierarchy order wins on scene start. Set the default active toggle's `Is On` checkbox to true in the Inspector.

---

## ScrollRect (for long scrollable text or content)

Used for the Privacy Policy body, long consent text, or any container that may overflow.

### Hierarchy

```
ScrollableArea                       (your container, has ScrollRect component)
├── Viewport                         (Image, optional mask, has Mask + Image components)
│   └── Content                      (RectTransform, has Vertical Layout Group + Content Size Fitter)
│       └── Body                     (TMP — long text, Auto Size = OFF, fixed point size)
└── Scrollbar (Vertical)             (optional, drag here)
```

### Create
1. Right-click parent → **UI → Scroll View**. Unity creates the whole hierarchy for you (ScrollableArea + Viewport + Content + horizontal/vertical Scrollbar).
2. Delete the horizontal Scrollbar if you don't need horizontal scrolling.
3. On the ScrollRect component, uncheck **Horizontal** and leave **Vertical** checked.
4. Add your text content under `Content`. Add **Content Size Fitter** with **Vertical Fit = Preferred Size** on `Content`.
5. TMP Body: Auto Size = **OFF**, fixed point size (24–28pt for body), Word Wrap = ON, Vertical Overflow = Overflow (Content Size Fitter handles the actual sizing).

### Common settings on ScrollRect

| Field | Value |
|---|---|
| Horizontal | ☐ |
| Vertical | ☑ |
| Movement Type | Clamped |
| Inertia | ☑ |
| Scroll Sensitivity | 25 |

### Gotchas
- **Text doesn't scroll** — Content is missing Content Size Fitter, or the TMP has Auto Size ON (which makes the text shrink to fit instead of overflowing into scrollable space).
- **Content clips weirdly** — Viewport is missing a Mask component (default 2D Mask is fine).
- **Scrollbar doesn't shrink** — Viewport's anchors aren't stretching to fill the ScrollRect.

---

## Anchor Presets (positioning quick reference)

Every UI element has anchor points that determine how it positions/scales relative to its parent. The anchor preset dropdown (top-left of the Rect Transform) covers 80% of cases.

### Common presets

| Anchor preset | Behaviour |
|---|---|
| **Top Left / Top Right / Bottom Left / Bottom Right** | Pinned to that corner. Size stays fixed. |
| **Middle Center** | Centred in parent. Size stays fixed. Useful for modal cards. |
| **Stretch Top / Stretch Bottom** | Stretches horizontally, pinned to top/bottom. Useful for headers/footers. |
| **Stretch Stretch** (the four-arrow icon) | Fills parent in both dimensions. Useful for backdrops, overlays, ScrollRect viewports. |

### Hold Alt while clicking
Sets both anchor AND position together. Without Alt, only the anchor changes — position can shift unexpectedly.

### Hold Shift while clicking
Sets both anchor AND pivot together. Useful for centring (Shift+Alt+click Middle Center makes a perfectly centred element).

---

## Image components for backdrops and buttons

### Backdrop (dark overlay behind a modal)
- Image component, Color = `#000000`, Alpha = 180 (out of 255).
- Anchor preset = Stretch Stretch.
- **Raycast Target = ON** if you want the backdrop to block clicks behind it. **OFF** if not.

### Card (the actual modal content panel)
- Image component, Color = `#16213E` (dark navy), Alpha = 255.
- Source Image = a rounded-rectangle sprite if available; otherwise leave default (sharp corners).
- Anchor preset = Middle Center, fixed Width/Height (or use Content Size Fitter to auto-size).

### Primary button
- Button + Image. Image Color = `#C9A84C` (gold).
- Child TMP: color = `#1A1A2E` (almost black), font style = Bold.
- No border, no shadow — flat style per VISUALS.md.

### Toggle (checkbox style)
- Use Unity's default Toggle prefab as a starting point (right-click → UI → Toggle).
- Background Image color = `#2D2D44` (dark plate).
- Checkmark Image color = `#C9A84C` (gold).
- Label TMP color = `#F0E6D3` (warm off-white).

### Common gotcha — Raycast Target
- Decorative images (backdrops behind elements that should still be clickable, brightness/vignette overlays) should have **Raycast Target = OFF** so they don't swallow clicks.
- Clickable images (buttons, the backdrop if you want it to dismiss the modal on click) must have **Raycast Target = ON**.

---

## TextMeshPro basics

### Recommended settings for body text

| Field | Value |
|---|---|
| Font Asset | Project's default TMP asset (whatever the existing labels use) |
| Font Size | Body 24–28pt, subtitle 20–22pt, title 36–48pt |
| Auto Size | OFF for ScrollRect content, ON for fixed-size labels that should fit |
| Word Wrap | ON |
| Overflow | Overflow (for scrollable content) or Truncate (for fixed-size labels) |
| Color | per VISUALS.md (gold for titles, gray for subtitles, off-white for body) |

### Auto Size gotcha
With Auto Size ON, the TMP shrinks the font to fit. With it OFF, the text overflows the bounds (and can be made scrollable via Content Size Fitter + ScrollRect). For modal body text inside a ScrollRect, **always set Auto Size OFF**.

---

## Common gotchas across all UI

- **Children stacked on top of each other instead of side by side** — added Vertical instead of Horizontal Layout Group.
- **Children crushed to zero width** — Control Child Size = ON without preferred widths set on children. Either turn OFF or add Layout Element with Preferred Width.
- **Greyed-out Width/Height in Rect Transform** — a Layout Group on the parent is controlling that field. Normal under Vertical or Horizontal Layout Group; use Layout Element to override per-child.
- **Spacing/padding seems to have no effect** — a higher layout group is overriding. Walk up the parent chain.
- **Click goes through a modal to the menu behind it** — the modal's Backdrop has Raycast Target OFF, or there's no Backdrop at all.
- **Text doesn't scroll inside a ScrollRect** — Content is missing Content Size Fitter, or TMP has Auto Size ON.
- **Two radio toggles both appear active** — they're not in the same ToggleGroup.
- **UI doesn't show up at all** — Canvas Sort Order is lower than another canvas in the scene that's drawing over it. Use Sort Order 999 for emergency overlays.

---

## Reading order for new wiring docs

When opening a `WIRING_TASK_N.md` for the first time:

1. Skim **this** file (`WIRING_CONVENTIONS.md`) so you have the layout primitives in your head.
2. Skim **CLAUDE.md → Scene Hierarchies** to know what already exists in each scene.
3. Skim **CLAUDE.md → Modal panel style** to know the panel pattern.
4. Read the task-specific `WIRING_TASK_N.md` end-to-end before opening Unity.
5. Open Unity and follow the task's steps in order.
