# Task 2 — Settings Menu + First-Launch Consent: Unity Editor Wiring

Code-side changes are complete. This document is the step-by-step Unity Editor work you have to do **on the `MainMenu` scene only** before this task can be verified. **No scene, prefab, or asset files were modified by Claude** — every GameObject and component below must be created manually.

This doc assumes the `MainMenu` scene already has the structure shown in [CLAUDE.md → Scene Hierarchies → MainMenu](CLAUDE.md): `Canvas` with `BackgroundPanel`, `HeaderGroup`, `StartSessionGroup`, `OtherButtonsGroup`, `PanelsGroup` (containing scaffolded `PPTXPanel`, `SettingsPanel`, `DevPanel`).

> **Design note — script-cap deviation.** The plan called for two new MonoBehaviours (`SettingsMenuController.cs` + `ConsentScreenController.cs`). Only `SettingsMenuController.cs` is new; consent logic lives inside `MainMenuController.cs`. Runtime MonoBehaviour count: 12 → **13**.

---

## 1. Open the MainMenu scene

`Assets/Scenes/MainMenu.unity`. Confirm via the Hierarchy panel:
- `Canvas` with the five group children listed above.
- `EventSystem` GameObject.
- `MainMenuController` MonoBehaviour exists somewhere (likely on `Canvas`).

---

## 2. Wire the interactive-roots array

The consent gate hides three of your existing group GameObjects (everything except the background and the panel container).

1. Click the GameObject holding `MainMenuController` (likely `Canvas`).
2. In the Inspector, find the **Main menu interactive roots** array under the `Main Menu Controller (Script)` header.
3. Set Size = **3**, then drag into the three slots:
   - Element 0: `Canvas/HeaderGroup`
   - Element 1: `Canvas/StartSessionGroup`
   - Element 2: `Canvas/OtherButtonsGroup`
4. Leave `BackgroundPanel` and `PanelsGroup` out — `BackgroundPanel` stays visible behind the consent panel, and `PanelsGroup` must stay enabled so the consent panel inside it can show.

Optionally drag the existing menu TMP labels into the new localized-label slots on `MainMenuController`:

| MainMenuController slot | Drag this TMP |
|---|---|
| Menu Title Label | `Canvas/HeaderGroup/TitleText` |
| Start Button Label | TMP child of `Canvas/StartSessionGroup/StartSessionButton` |
| Settings Button Label | TMP child of `Canvas/OtherButtonsGroup/SettingsButton` |
| Developer Button Label | TMP child of `Canvas/OtherButtonsGroup/DeveloperButton` |
| Pptx Button Label | TMP child of `Canvas/OtherButtonsGroup/PPTXButton` |
| Duration Section Label | `Canvas/StartSessionGroup/DurationLabel` |

These are optional — leave any of them empty if you don't have that label yet. `RefreshLocalizedText()` skips nulls.

---

## 3. Populate the existing SettingsPanel

Your existing `Canvas/PanelsGroup/SettingsPanel` has this scaffold:

```
SettingsPanel
├── Backdrop                             (dark overlay)
└── Card                                 (modal container)
    ├── Title                            (TMP — currently "Settings")
    ├── Body                             (TMP — currently "Settings coming soon" placeholder)
    └── CloseButton                      (Button)
        └── Text (TMP)                   (currently "Close")
```

You'll keep `Backdrop`, `Card`, `Title`, and `CloseButton`. **Delete the placeholder `Body` TMP** — it gets replaced by the three sections below.

Target structure — **matches the existing `DevPanel` pattern** (flat subtitle labels + `Row_X` children, gold buttons full-width, gray subtitles for section headers):

```
Canvas
└── PanelsGroup
    └── SettingsPanel                    (existing — inactive when not in use)
        ├── Backdrop                     (existing — leave as-is)
        └── Card                         (existing — modal container)
            ├── Title                    (existing — TMP, gold, will display localized "Settings")
            │
            ├── LanguageLabel            (new — TMP subtitle, gray; "Language")
            ├── Row_Language             (new — horizontal layout group)
            │   ├── EnglishToggle        (Toggle)
            │   │   └── Label            (TMP — "English")
            │   └── DutchToggle          (Toggle)
            │       └── Label            (TMP — "Nederlands")
            │
            ├── PrivacyLabel             (new — TMP subtitle, gray; "Privacy & Data")
            ├── Row_ConsentStatus        (new — horizontal layout group)
            │   ├── StatusLabel          (TMP — "Audio sent to Google Cloud STT")
            │   └── StatusValue          (TMP — "YES" / "NO", gold)
            ├── ReviewConsentButton      (new — full-width gold Button)
            │   └── Text (TMP)           ("Review consent")
            ├── PrivacyPolicyButton      (new — full-width gold Button)
            │   └── Text (TMP)           ("Privacy policy")
            │
            ├── ComfortLabel             (new — TMP subtitle, gray; "VR Comfort")
            ├── Row_Brightness           (new — horizontal layout group)
            │   ├── BrightnessLabel      (TMP — "Brightness")
            │   ├── BrightnessSlider     (Slider, min 0, max 1)
            │   └── BrightnessValue      (TMP — optional, shows "100%")
            ├── Row_Vignetting           (new — horizontal layout group)
            │   ├── VignettingLabel      (TMP — "Vignetting (head motion)")
            │   └── VignettingToggle     (Toggle)
            │
            └── CloseButton              (existing — full-width gold Button)
                └── Text (TMP)           (existing — "Close" / localized "Back")
```

**Style notes — match DevPanel exactly:**
- Subtitle labels (`LanguageLabel`, `PrivacyLabel`, `ComfortLabel`) sit as direct children of `Card`, immediately above their related rows — same pattern as DevPanel's `SessionLabel` / `SpeechLabel`.
- "Row_" prefix is used only when a single line groups a label + control (matches DevPanel's `Row_DebugMode`, `Row_Duration`, etc.). Standalone primary buttons (`ReviewConsentButton`, `PrivacyPolicyButton`, `CloseButton`) are flat children of `Card` with no `Row_` wrapper — same as DevPanel's `SkipSessionButton` and `CloseButton`.
- Gold colour for primary buttons, gray for subtitles, white-ish for body labels. Full palette in §9 below.

**Layout tips:**
- **Add a `Vertical Layout Group` to `Card`** (if not already present) so children stack top-to-bottom in Hierarchy order without manual positioning. Configure: Child Force Expand Width = ON, Height = OFF; Padding = ~16; Spacing = ~12. Add a `Content Size Fitter` with Vertical Fit = Preferred Size so Card auto-sizes around its content (same recipe as your DevPanel's Card).
- **Add a `ToggleGroup` component to `Row_Language`**. On each toggle (`EnglishToggle`, `DutchToggle`), drag `Row_Language` into the toggle's **Group** field so they behave as radio buttons.
- The `CloseButton` label can stay as "Close" — the localization key (`settings_back` in en.json) controls the displayed text at runtime regardless of the GameObject name.
- The same flat pattern applies to the upcoming PPTX panel (Task 3) — keep it consistent so all three modal panels (DevPanel, SettingsPanel, PPTXPanel) share visual language.

`SettingsPanel` stays inactive in the Editor — `OnSettingsPressed` toggles it on at runtime.

Centering: you mentioned the Card is currently off-center — a separate issue to fix later. Setting Card's `RectTransform` anchors to (0.5, 0.5) min and max, and `Pivot` to (0.5, 0.5), with `anchoredPosition` (0, 0) is the standard centering recipe.

---

## 4. Create the Privacy Policy sub-panel

Add as a new child inside `PanelsGroup` (sibling of `SettingsPanel`). **Same Backdrop+Card modal pattern as `DevPanel` / `SettingsPanel`** — duplicate one of those as a starting point if helpful.

```
Canvas
└── PanelsGroup
    ├── PPTXPanel                        (existing)
    ├── SettingsPanel                    (populated in step 3)
    ├── DevPanel                         (existing — visual reference)
    └── PrivacyPolicyPanel               (new — inactive by default)
        ├── Backdrop                     (dark overlay)
        └── Card                         (modal container)
            ├── Title                    (TMP — "Privacy Policy", gold)
            ├── BodyScroll               (ScrollRect)
            │   └── Viewport
            │       └── Content
            │           └── Body         (TMP, ~24pt, long text)
            └── CloseButton              (full-width gold Button — "Back")
                └── Text (TMP)
```

Start it **inactive**. Opened by `SettingsMenuController` when the user taps "Privacy policy".

---

## 5. Create the Consent + ConsentBlocked panels

Add as new children inside `PanelsGroup`, siblings of `SettingsPanel`. **Same Backdrop+Card pattern.** For these panels, the Card should fill more of the screen (a "page" feel rather than a small modal) since the consent screen is the only thing visible at first launch.

```
Canvas
└── PanelsGroup
    ├── PPTXPanel                        (existing)
    ├── SettingsPanel                    (populated in step 3)
    ├── DevPanel                         (existing)
    ├── PrivacyPolicyPanel               (from step 4)
    │
    ├── ConsentPanel                     (new — full-canvas modal)
    │   ├── Backdrop                     (dark overlay)
    │   └── Card                         (larger modal container)
    │       ├── Title                    (TMP — "Consent Required", gold, 40pt)
    │       ├── BodyScroll               (ScrollRect, optional)
    │       │   └── Viewport
    │       │       └── Content
    │       │           └── Body         (TMP — short consent body)
    │       ├── AcceptButton             (full-width gold Button)
    │       │   └── Text (TMP)           ("I consent")
    │       └── DeclineButton            (full-width dim Button — `#2D2D44`)
    │           └── Text (TMP)           ("I do not consent")
    │
    └── ConsentBlockedPanel              (new — inactive by default)
        ├── Backdrop                     (dark overlay)
        └── Card                         (modal container)
            ├── Title                    (TMP — "Consent required to proceed", gold)
            ├── Body                     (TMP — explainer line)
            └── RetryButton              (full-width gold Button)
                └── Text (TMP)           ("Retry")
```

`ConsentPanel` initial Active state doesn't matter — `MainMenuController.Start()` toggles it based on `Consent_Granted`. Setting it to enabled in the Editor is convenient for visual checking.

`ConsentBlockedPanel` starts **inactive**.

Since both panels live inside `PanelsGroup` and `PanelsGroup` stays active during the consent flow, they'll display correctly. The three interactive groups (HeaderGroup, StartSessionGroup, OtherButtonsGroup) hide behind the consent overlay via the array wired in step 2.

---

## 6. Create the runtime overlays (brightness + vignette)

These overlays must visibly affect both the MainMenu **and** the Session scene at runtime, so they need to exist in both scenes.

### Approach (no new script needed)

Create the same overlay canvas separately in both scenes — `MainMenu` and `Session`. Each scene's canvas is independent; the script reads PlayerPrefs values, so brightness/vignetting settings persist across scene loads regardless.

#### In MainMenu scene

1. Right-click the Hierarchy root (not under `Canvas`) → `UI → Canvas`. Name it `RuntimeOverlayCanvas`.
2. Inspector settings on the Canvas component:
   - Render Mode: **Screen Space - Overlay**
   - Sort Order: **999** (must draw on top of everything)
3. Add a `Canvas Scaler` component if not present:
   - UI Scale Mode: **Scale With Screen Size**
   - Reference Resolution: **1080 × 1920**
   - Screen Match Mode: **Match Width Or Height**, Match: **0.5**
4. Under `RuntimeOverlayCanvas` create two children:

   ```
   RuntimeOverlayCanvas
   ├── BrightnessOverlay        (Image, color = #000000, alpha = 0,
   │                             stretch anchors fill parent,
   │                             Raycast Target = OFF)
   └── VignetteOverlay          (Image, sprite = vignette PNG (see below),
                                 color tint = #000000, alpha = 0,
                                 stretch anchors fill parent,
                                 Raycast Target = OFF)
   ```

5. Both child Images need:
   - Anchors set to stretch in both dimensions (min 0,0 / max 1,1, all offsets 0)
   - **Raycast Target = OFF** so they don't intercept clicks

#### In Session scene

Repeat steps 1–5 above in the `Session` scene. Same names, same configuration. The Session-scene `SettingsMenuController` won't exist there (it lives on MainMenu), but the overlay Images themselves just need to be present — when a script writes their alpha, they react. The wiring side comes in step 7.

### Vignette texture

The project doesn't ship a vignette PNG. Two options:

1. **Recommended:** import any free radial-alpha vignette PNG (CC0 from OpenGameArt, Kenney, or Unity Asset Store). 512×512 with transparent centre and dark edges is sufficient. Drop under `Assets/Textures/UI/`, set Texture Type = **Sprite (2D and UI)**, assign as `VignetteOverlay`'s Source Image.
2. **Fallback:** leave the default UI sprite. The toggle still gates the overlay but the "vignette" looks like a uniform full-screen tint. Acceptable for testing.

---

## 7. Add SettingsMenuController to the Canvas

Add the `SettingsMenuController` component to **the same GameObject that hosts `MainMenuController`** (typically `Canvas`).

In the Inspector, drag every reference:

| SettingsMenuController slot | Drag this GameObject / Component |
|---|---|
| **Settings Panel Root** | `Canvas/PanelsGroup/SettingsPanel` |
| **Privacy Policy Panel Root** | `Canvas/PanelsGroup/PrivacyPolicyPanel` |
| Title Label | `SettingsPanel/Card/Title` |
| Back Button | `SettingsPanel/Card/CloseButton` |
| Back Button Label | `SettingsPanel/Card/CloseButton/Text (TMP)` |
| Language Section Label | `SettingsPanel/Card/LanguageLabel` |
| Lang English Toggle | `SettingsPanel/Card/Row_Language/EnglishToggle` |
| Lang Dutch Toggle | `SettingsPanel/Card/Row_Language/DutchToggle` |
| Lang English Label | `Row_Language/EnglishToggle/Label` |
| Lang Dutch Label | `Row_Language/DutchToggle/Label` |
| Privacy Section Label | `SettingsPanel/Card/PrivacyLabel` |
| Privacy Status Label | `SettingsPanel/Card/Row_ConsentStatus/StatusLabel` |
| Privacy Status Value | `SettingsPanel/Card/Row_ConsentStatus/StatusValue` |
| Review Consent Button | `SettingsPanel/Card/ReviewConsentButton` |
| Review Consent Label | `ReviewConsentButton/Text (TMP)` |
| Privacy Policy Button | `SettingsPanel/Card/PrivacyPolicyButton` |
| Privacy Policy Label | `PrivacyPolicyButton/Text (TMP)` |
| Privacy Policy Title Label | `PrivacyPolicyPanel/Card/Title` |
| Privacy Policy Body Label | `PrivacyPolicyPanel/Card/BodyScroll/Viewport/Content/Body` |
| Privacy Policy Back Button | `PrivacyPolicyPanel/Card/CloseButton` |
| Privacy Policy Back Label | `PrivacyPolicyPanel/Card/CloseButton/Text (TMP)` |
| Comfort Section Label | `SettingsPanel/Card/ComfortLabel` |
| Brightness Label | `SettingsPanel/Card/Row_Brightness/BrightnessLabel` |
| Brightness Slider | `SettingsPanel/Card/Row_Brightness/BrightnessSlider` |
| Vignetting Label | `SettingsPanel/Card/Row_Vignetting/VignettingLabel` |
| Vignetting Toggle | `SettingsPanel/Card/Row_Vignetting/VignettingToggle` |
| **Brightness Overlay** | `RuntimeOverlayCanvas/BrightnessOverlay` (the MainMenu copy) |
| **Vignette Overlay** | `RuntimeOverlayCanvas/VignetteOverlay` (the MainMenu copy) |
| Head Transform | leave empty (auto-finds `Camera.main` each scene) |

---

## 8. Wire the new MainMenuController slots

On the same `MainMenuController` (already in your scene), drag:

| MainMenuController slot | Drag |
|---|---|
| Main Menu Interactive Roots [0..2] | `HeaderGroup`, `StartSessionGroup`, `OtherButtonsGroup` (already done in step 2) |
| Settings Menu | the `SettingsMenuController` component you just added |
| Consent Panel Root | `Canvas/PanelsGroup/ConsentPanel` |
| Consent Title Label | `ConsentPanel/Card/Title` |
| Consent Body Label | `ConsentPanel/Card/BodyScroll/Viewport/Content/Body` |
| Consent Accept Button | `ConsentPanel/Card/AcceptButton` |
| Consent Accept Label | `ConsentPanel/Card/AcceptButton/Text (TMP)` |
| Consent Decline Button | `ConsentPanel/Card/DeclineButton` |
| Consent Decline Label | `ConsentPanel/Card/DeclineButton/Text (TMP)` |
| Consent Blocked Panel Root | `Canvas/PanelsGroup/ConsentBlockedPanel` |
| Consent Blocked Title Label | `ConsentBlockedPanel/Card/Title` |
| Consent Blocked Body Label | `ConsentBlockedPanel/Card/Body` |
| Consent Retry Button | `ConsentBlockedPanel/Card/RetryButton` |
| Consent Retry Label | `ConsentBlockedPanel/Card/RetryButton/Text (TMP)` |

You do **not** have to re-wire the existing SettingsButton onClick — `OnSettingsPressed()` already routes through the new `SettingsMenuController`. Leave the existing button onClick → `MainMenuController.OnSettingsPressed`.

---

## 9. Dark academic colour values

Apply these to every Image / TMP on the new panels (matches `VISUALS.md`):

| Role | Hex | Alpha |
|---|---|---|
| Panel background (SettingsPanel, ConsentPanel, ConsentBlocked, PrivacyPolicy) | `#16213E` | 220 |
| Section labels (Language / Privacy / VR Comfort headers) | `#B8A99A` | 255 |
| Body text | `#F0E6D3` | 255 |
| Title text | `#C9A84C` | 255 |
| Primary button (Back / Accept / Retry / Review consent / Privacy policy) — Image | `#C9A84C` | 255 |
| Primary button label TMP | `#1A1A2E` | 255 |
| Secondary button (Decline) — Image | `#2D2D44` | 255 |
| Secondary button label TMP | `#F0E6D3` | 255 |
| Brightness slider track | `#2D2D44` | 255 |
| Brightness slider fill + handle | `#C9A84C` | 255 |
| Toggle background | `#2D2D44` | 255 |
| Toggle checkmark | `#C9A84C` | 255 |
| Privacy status YES text | `#C9A84C` | 255 |
| Privacy status NO text | `#B8A99A` | 255 |
| BrightnessOverlay Image | `#000000` | 0 (script drives alpha) |
| VignetteOverlay Image tint | `#000000` | 0 (script drives alpha) |

---

## 10. Test checklist

Hit Play and confirm:

1. **First launch.** Clear `PlayerPrefs` (`Edit → Clear All PlayerPrefs` or run `PlayerPrefs.DeleteAll()` via a context-menu helper). Play.
   - ConsentPanel appears.
   - HeaderGroup + StartSessionGroup + OtherButtonsGroup are hidden — Start button is not clickable.
   - BackgroundPanel and PanelsGroup remain active so the consent panel itself shows.
2. **Decline.** Tap "I do not consent" → ConsentBlockedPanel appears with the Retry button. Tap Retry → returns to the consent prompt.
3. **Accept.** Tap "I consent" → ConsentPanel hides, HeaderGroup/StartSessionGroup/OtherButtonsGroup re-appear.
4. **Settings open/close.** Tap Settings → SettingsPanel opens with three sections populated. Tap Back → SettingsPanel hides, menu visible.
5. **Language switch.** With language = English, tick the Dutch toggle. Settings panel labels switch to Dutch within a frame. Close Settings → MainMenu labels (title, button labels) are also Dutch. Confirm `PlayerPrefs.GetString("Language") == "nl"`.
6. **Brightness slider.** Drag from 1 → 0. Screen darkens progressively. Set back to 1 — normal. `PlayerPrefs.GetFloat("Settings_Brightness")` matches.
7. **Vignetting.** Toggle ON. Rotate the Game-view camera quickly (right-click + drag, or Alt+drag depending on your layout). At rotation > ~60°/s the vignette fades in; below, fades out. Toggle OFF → no vignette regardless of motion.
8. **Privacy status.** Privacy section reads "Audio sent to Google Cloud STT: YES" after consent granted, "NO" after declining.
9. **Review consent.** Settings → Review consent → ConsentPanel reappears over the menu. Accepting or declining updates `Consent_Granted` and the privacy status readout.
10. **Privacy policy.** Settings → Privacy policy → PrivacyPolicyPanel appears with the ~200-word body. Scroll. Back → Settings.
11. **Persistence.** Stop and restart Play Mode. Consent state, language, brightness, vignetting all persist.
12. **Dev panel untouched.** Open the Developer panel — its layout and behaviour are identical to before Task 2.

---

## 11. Known wiring gotchas

- **Toggle ordering for radio.** If the Language toggles flicker or both appear active, the ToggleGroup is missing from one of them — check that both `EnglishToggle` and `DutchToggle` reference the same `ToggleGroup` component on `LanguageSection`.
- **Brightness not visible in Game view.** Confirm `RuntimeOverlayCanvas` Sort Order is higher than every other canvas in the scene (use 999). Cardboard rigs sometimes spawn an additional canvas for the dual-eye render — your overlay must sit above it.
- **Vignette never appears.** Threshold defaults to 60°/s; a casual camera nudge in the Game view may not exceed it. Hold the right mouse button and sweep quickly to test. Tunables on `SettingsMenuController`: `vignetteOnThreshold`, `vignetteFullThreshold`, `vignetteMaxAlpha`, `vignetteFadeSpeed`.
- **Privacy policy body doesn't wrap.** TMP Body should have Auto Size OFF, fixed point size (24–28pt), and a `ContentSizeFitter` on the Content GameObject with Vertical Fit = Preferred Size, so the ScrollRect scrolls cleanly.
- **Brightness/vignette only work in MainMenu, not Session.** You forgot step 6's second half — duplicate `RuntimeOverlayCanvas` in the Session scene. Each scene needs its own copy of these two Images for the overlays to render there.
