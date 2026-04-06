# VR Speaking Trainer — Visual Guidelines

Theme: **Dark Academic** — dark navy backgrounds, gold accents, warm off-white text.

---

## Color Palette

| Role | Hex | Alpha |
|---|---|---|
| Background | `#1A1A2E` | 255 |
| Panel | `#16213E` | 220 |
| Accent / Gold | `#C9A84C` | 255 |
| Text primary | `#F0E6D3` | 255 |
| Text secondary | `#B8A99A` | 255 |
| Progress bar fill | `#C9A84C` | 255 |
| Progress bar track | `#2D2D44` | 255 |
| Button background | `#C9A84C` | 255 |
| Button text | `#1A1A2E` | 255 |

---

## Typography (TextMeshPro)

| Usage | Size | Style | Color |
|---|---|---|---|
| Screen title | 36–52pt | Bold | `#C9A84C` |
| Subtitle / secondary title | 26–32pt | Normal | `#B8A99A` |
| Large score / number | 96–120pt | Bold | `#C9A84C` |
| Grade letter | 48–56pt | Bold | `#F0E6D3` |
| Caption / feedback string | 28–32pt | Italic | `#B8A99A` |
| Body / metric labels | 26–30pt | Normal | `#F0E6D3` |
| Button label | 30–36pt | Bold | `#1A1A2E` |

No `\n` in TMP strings — use separate UI elements for line breaks.

---

## UI Components

**Panels**
- Image component, color `#16213E`, alpha 220.
- Used as background cards behind control groups.

**Buttons**
- Image color `#C9A84C`. Child TMP text color `#1A1A2E`, bold.
- No border or shadow — flat style.

**Slider (duration)**
- Background track: `#2D2D44`.
- Fill area: `#C9A84C`.
- Handle: `#C9A84C`, size 30×30.

**Toggle (debug)**
- Background box: `#2D2D44`.
- Checkmark: `#C9A84C`.
- Label: 24pt, `#B8A99A`.

**Progress bars (Results screen)**
- Image component, Image Type = `Filled`, Fill Method = `Horizontal`, Fill Origin = `Left`.
- Fill image color: `#C9A84C`.
- Place a separate `#2D2D44` Image behind as the track.

---

## Scene Reference

| Scene | Camera bg | Key elements |
|---|---|---|
| MainMenu | `#1A1A2E` | Full-screen `#16213E` panel, gold title, styled slider + toggle + button |
| Results | `#1A1A2E` | Large gold score, grade letter, three progress bar rows, gold Back button |
| Session (pause menu) | — world space | `#16213E` panel, gold "PAUSED" title, gold buttons |

---

## Canvas Settings (MainMenu & Results)

- Render Mode: `Screen Space - Overlay`
- Canvas Scaler: `Scale With Screen Size`, Reference Resolution `1080 × 1920`, Match `0.5`
