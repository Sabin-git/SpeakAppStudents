# VR Public Speaking Trainer - SpeakAppStudents

Mobile VR public-speaking practice app for Android using Unity and Google Cardboard. The user presents in a virtual classroom; the app listens to speech in real time, computes speaking metrics, tracks gaze across named zones (and per individual audience member), and drives a rule-based virtual audience that reacts to delivery and eye contact. At the end of a session it shows a feedback report combining speech, gaze, and pacing scores plus a gaze heat map.

This repository contains the active Unity project in [VRSpeakingTrainer](VRSpeakingTrainer). This README summarizes the current architecture, feature set, constraints, and direction. The files in `initial-docs-outdated/` are historical planning notes; parts of them no longer match the current implementation.

For the authoritative, always-current runtime architecture (scene hierarchies, full script index, every PlayerPrefs key, rule tables) see [VRSpeakingTrainer/CLAUDE.md](VRSpeakingTrainer/CLAUDE.md).

## Project Goals

- Practice public speaking in a virtual classroom on Android with Google Cardboard.
- Real-time speech recognition (Google Cloud STT) with a developer mock mode for offline testing.
- Keep audience adaptation deterministic and explainable through a rule-based system (no ML / black boxes).
- Provide an end-of-session feedback report from speech and gaze metrics.
- Be usable in a real user study: bilingual, consent-gated, and configurable per participant.

## Current Design Summary

- Engine: Unity 6.4 (worked on with `6000.4.1f1`)
- VR SDK: Google Cardboard XR Plugin
- Speech recognition: Google Cloud Speech-to-Text REST API (with developer mock mode for offline testing)
- Input: Unity **Input System** package (the legacy `UnityEngine.Input` class is not used and throws under the active settings)
- Languages: English and Dutch — UI strings, STT language code, filler-word lists, and WPM thresholds all switch together (default English)
- Target platform: Android, IL2CPP, ARM64, min API 26, target API 35
- UI: TextMeshPro, localized via a JSON dictionary (`StreamingAssets/en.json` / `nl.json`)
- Assets: Unity built-ins, ProBuilder, Mixamo (free only)

### Scene Structure

- `MainMenu`: start flow, session setup, Settings, first-launch consent gate, Notes (light PPTX) editor, Developer panel
- `Session`: XR rig, classroom, lectern, audience, all gameplay scripts
- `Results`: post-session feedback (scores, grade, raw metrics, gaze heat map)

### Classroom Spec

- Presenter stands at the front of the room facing the audience.
- Blackboard is on the back wall behind the audience and is decorative only.
- Audience is 10 avatars in 2 rows of 5 (active count is configurable 1–10).
- The lectern is the only interactive surface. It displays the presenter's **notes** (free-form text entered in the Notes panel). A slide-image panel exists for future full-PPTX support and is hidden while notes are shown.
- Gaze targets are `AudienceTarget` and `LecternTarget`.

### Session Flow

- User sets duration and (optionally) writes notes on `MainMenu`, then starts.
- Session runs in the `Session` scene with a screen-space countdown timer and live WPM/transcript HUD.
- Notes navigation: arrow / PageUp-Down keys (keyboard or Bluetooth presenter remote), or the **Android hardware volume buttons** on device.
- Android back / Escape opens the pause overlay: Finish (save → Results), Exit (no save → MainMenu), Resume.
- Session timeout ends the session, writes metrics, and goes to `Results`.

## Current Implementation Status

The core loop is fully functional end to end: session start → speech recognition → speech metrics → rule-based audience AI → head/gaze tracking → session end → scored results + gaze heat map. A developer panel allows testing without the real STT API.

**Core build stages (1–6) are complete.** The visual overhaul (Results scoring + heat map), developer tooling, and most avatar-animation work are also in place.

**User-study readiness work (this branch) — landed:**

- **Dutch language support** — full bilingual EN/NL: UI strings, STT language code (`en-US` / `nl-NL`), filler-word lists, and WPM thresholds switch together from one `Language` pref.
- **Settings menu + first-launch consent gate** — user-facing Settings panel (Language, Privacy/consent, Audience responsiveness, Advanced). GDPR-style consent screen blocks the app until accepted (audio is sent to Google Cloud STT).
- **Audience animation fixes** — per-avatar variant cycling (8–15 s) with anti-sync offsets; staggered state transitions so the crowd no longer changes mood in unison; fixed the "fall through chair" bug; Restless wired to its own seated clip.
- **Audience responsiveness levels** — Easy / Medium / Hard scale every rule threshold (pause, filler, WPM band, sustained-out-of-band, min-hold) and the per-avatar transition stagger.
- **Gaze control + heat map (Task 4)** — per-avatar gaze accumulation; sustained eye contact monotonically engages an individual avatar (literature-backed); Results screen heat map of where the user looked, top-3 most-looked-at avatars, and per-zone (audience / lectern / other) gaze times.
- **Feature A — Developer button visibility** — hidden by default; a Settings → Advanced toggle reveals it (participants never see the dev panel).
- **Feature B — Microphone on/off (silent mode)** — a Settings → Privacy toggle disables the mic/STT entirely; the audience receives no speech and naturally drifts to Restless. HUD shows a "Microphone off" indicator; Results hides the raw speech numbers.
- **Feature C — Light PPTX (free-form notes)** — instead of uploading slides, the user types short free-form notes (≤ 200 chars each, multiple "notes" per deck) in the Notes panel; the active note renders on the lectern. In-memory only (persists during an app run, cleared on restart). A sample deck is seeded on first launch.

**Removed during this branch (do not re-add without discussion):**

- VR-comfort **brightness** control — removed; phone brightness covers it.
- VR-comfort **vignetting** control — removed; the UI-overlay approach didn't work in stereoscopic Cardboard rendering, and the URP post-process replacement was unreliable on device. See *Future Directions* for the world-space-mesh alternative if motion comfort is revisited.

## Development Stages

- [x] Stage 1 — VR Foundation (Cardboard rig, scenes, first Android build)
- [x] Stage 2 — Classroom Environment (room, 10 avatars in 2×5, decorative blackboard, lectern, gaze targets)
- [x] Stage 3 — Session System (`SessionManager`, countdown timer, screen-space HUD, pause menu)
- [x] Stage 4 — Google Cloud STT Integration (`SpeechRecognizer`, audio chunks, REST API, live transcript)
- [x] Stage 5 — Speech Metrics (`SpeechAnalyzer`, WPM, pauses, filler words; end-of-session recount from full transcript)
- [x] Stage 6 — Rule Engine + Audience AI (`AudienceRuleEngine`, `AudienceController`, `HeadTracker`, `AudienceMember`)
- [x] Stage 7b — Visual Overhaul (dark academic theme; `ResultsUI` scored breakdown, grades, gaze heat map) — pause-menu restyle still pending
- [x] Stage 7c — Developer Panel (debug mode, mock mode, custom duration, audience/gaze/responsiveness overrides)
- [~] Stage 8 — Avatar Animations (state blend trees, per-avatar variant cycling, gaze-reaction override) — multi-clip idle variety optional/pending
- [~] Stage 9 — Notes on the lectern — **light in-app notes shipped (Feature C)**; the PC `convert_pptx.py` slide pipeline is not implemented (see Future Directions)
- [ ] Stage 10 — Integration + Hardening (full device test pass, GC profiling, signed APK)
- [ ] Stage 11 — On-device PPTX parsing *(stretch — see Future Directions)*
- [ ] Stage 12 — User Study Prep (multiple avatar models, study protocol, questionnaire, final build)

## Future Directions

Planned/deferred work, captured so it isn't lost. None of this is required for the current user study.

- **Full PPTX support** — on-device file picker + runtime PPTX → slide-image + speaker-notes parsing (or the PC `convert_pptx.py` pre-processing pipeline). The current light feature is text-only; the lectern's slide-image panel is already in place for this.
- **Microphone permission flow** — explicitly request the mic permission and handle a denial gracefully (currently relies on the platform prompt).
- **STT failure warning** — a HUD indicator when Google STT drops out mid-session (network/quota).
- **Audio cues** — small SFX for session start, slide/note change, warnings, session end (with procedural fallbacks).
- **Recenter / calibration** — a "look forward and tap to start" step so gaze zones align with whichever way the participant is facing.
- **In-VR onboarding tutorial** — a first-run overlay explaining gaze zones, notes navigation, and the pause control.
- **Native volume-button capture** — the current note navigation reads volume buttons by *polling* the media-volume stream (no manifest/activity change), which means the system volume overlay still flashes and media volume is pinned during a session. A native `UnityPlayerActivity` `onKeyDown` override would consume the key cleanly, but the launcher activity/manifest is deliberately fragile for Cardboard (see known fixes) and was left alone.
- **Motion-comfort vignette (redo)** — if reintroduced, use a world-space dome mesh parented to the camera rather than a UI overlay or URP post-process (both failed in stereo Cardboard).
- **Pause-menu restyle** and **multiple avatar models** (visual variety).
- **iOS port** — a follow-up platform target; needs dedicated XR/runtime, input, permissions, plugin-packaging, and file-access work.

## Known Limitations

- **`DebugMode` is intentionally coupled** — the same flag enables custom session duration *and* makes the Results screen substitute random debug values. A short sub-1-minute test session will therefore show randomized results, masking the real metrics. Kept deliberately; split into separate flags if real results are needed from short tests.
- **Final mic chunk is dropped** — `SpeechRecognizer.StopCapture` sends the last ~chunk of audio after `OnSessionEnd` has fired, so the final few seconds don't make it into the recounted end-of-session totals. Acceptable for multi-minute sessions.
- **Volume-button note navigation warts** — on device the system volume overlay flashes on each press, and the in-session media volume is pinned mid-range (restored at session end). Keyboard / Bluetooth-remote keys avoid this.
- **Gaze row discrimination is marginal** — the two audience rows are only ~9° apart vertically while the per-avatar gaze cone is ~15°, so the heat map distinguishes columns better than rows. Accepted and documented.
- **STT accuracy** — Google STT word-error rate is ~5–10% in good conditions, rising toward ~12% with chunk-boundary losses; a 120-word read in ~60 s typically transcribes as 105–115 words.

## Scripts Currently Present

Runtime MonoBehaviours (13):

- `SessionManager.cs` — session lifecycle, timer, pause menu, back/Escape handling (Input System)
- `XRLifecycleManager.cs` — Cardboard XR rig management
- `SpeechRecognizer.cs` — Google Cloud STT, mic input, mock mode, EN/NL language code; silent mode when the mic is disabled (Feature B)
- `SpeechAnalyzer.cs` — WPM, pauses, filler words (per-language lists); end-of-session recount from the full transcript
- `AudienceRuleEngine.cs` — rule table + head modifiers → `AudienceState` (per-language and per-responsiveness thresholds)
- `AudienceController.cs` — state propagation, audio crossfade, per-avatar gaze routing
- `AudienceMember.cs` — per-avatar state, variant cycling, staggered switching, per-avatar gaze-override state machine
- `HeadTracker.cs` — gaze zone classification, per-zone + per-avatar time accumulation for the heat map
- `HUDController.cs` — screen-space countdown + transcript/WPM HUD; "Microphone off" indicator (Feature B)
- `SlideController.cs` — lectern display: renders the active `NoteDeck` note; arrow/remote + Android volume-button navigation
- `MainMenuController.cs` — main-menu panels, developer settings, first-launch consent gate, dev-button visibility, Notes editor
- `ResultsUI.cs` — post-session scored breakdown (speech/gaze/pacing), grade, raw metrics, gaze heat map
- `SettingsMenuController.cs` — Settings panel: Language, Privacy/consent + mic toggle, Audience responsiveness, Advanced (dev-button toggle)

Runtime utility / data (do not count against the script cap):

- `SpeechMetrics.cs` — shared data struct
- `Localization.cs` — static JSON-dictionary localization (EN/NL); source of truth for `CurrentLanguage`
- `NoteDeck.cs` — static in-memory deck of free-form notes (Feature C); persists during an app run, cleared on restart

Editor-only utilities (under `Assets/Scripts/Editor/`, not runtime):

- `ClassroomBuilder.cs` — room shell, desk grid, avatar anchors, audience sanitize
- `AnimatorClipWirer.cs` — wires per-state blend trees into the audience Animator
- `AvatarLayerSetup.cs`, `AvatarMaterialFixer.cs`, `AnimationImportFixer.cs`, `ClipOrganizer.cs`, `VRTrainerSetup.cs` — Mixamo / animation import + setup helpers

## Core Constraints

These come from the active project guidance and should be preserved:

- Rule-based audience AI only — no ML / black-box logic
- Android-first development; test on device at the end of each meaningful stage
- Keep the codebase small, roughly 10–12 runtime scripts (utility/data and editor scripts don't count)
- No paid asset dependency (Unity built-ins, ProBuilder, Mixamo)
- Read input via the **Input System** package (`UnityEngine.InputSystem`), never the legacy `UnityEngine.Input` class — the latter throws at runtime under the active settings
- Avoid `\n` in static TextMeshPro/UI/localization strings; use separate UI elements (the dynamic lectern notes board is the intended exception)

<!-- ## Important Known Working Fixes

These Android/Cardboard fixes are stabilized and should not be casually changed:

- Use `UnityPlayerActivity`, not `GameActivity`
- `androidApplicationEntry: 1` in `ProjectSettings.asset`
- `OpenGLES3` only; Vulkan removed
- Custom `mainTemplate.gradle` is enabled
- `mainTemplate.gradle` includes:
  - `androidx.appcompat:appcompat:1.7.0`
  - `com.google.protobuf:protobuf-javalite:3.25.3`
  - Kotlin stdlib duplicate exclusions
- Cardboard XR Plugin is installed from the Git URL:
  - `https://github.com/googlevr/cardboard-xr-plugin.git`
- Package name:
  - `com.sabin.vrspeakingtrainer`

Files that currently contain stabilized fixes and should not be changed unless necessary:

- `VRSpeakingTrainer/Assets/Plugins/Android/AndroidManifest.xml`
- `VRSpeakingTrainer/Assets/Plugins/Android/mainTemplate.gradle`
- `VRSpeakingTrainer/ProjectSettings/ProjectSettings.asset`
- `VRSpeakingTrainer/Assets/Scripts/SessionManager.cs`
- `VRSpeakingTrainer/Assets/Scripts/XRLifecycleManager.cs` -->

## Google Cloud STT Setup

- API key lives in `Assets/StreamingAssets/config.json`: `{"google_api_key":"<key>"}` — loaded via `UnityWebRequest` (required for Android StreamingAssets, which live inside the `.jar`).
- `config.json` is excluded from version control (contains a live key). If it ever lands in history, rotate the key.
- `INTERNET` permission is declared for Android builds.

## Repository Layout

```text
SpeakAppStudents/
|- README.md
|- SpeakAppStudents.sln
|- Literature research background + interactions.pdf   (design-rationale citations)
|- VRSpeakingTrainer/
|  |- Assets/
|  |- Packages/
|  |- ProjectSettings/
|  |- CLAUDE.md                 (authoritative architecture + script/PlayerPrefs reference)
|  |- USER_TESTING_PLAN.md      (user-study readiness task plan + decision log)
|  |- VISUALS.md                (dark-academic colour palette + typography)
|  |- WIRING_CONVENTIONS.md     (Unity UI primer for the wiring docs)
|  |- WIRING_TASK_2.md, WIRING_TASK_2B.md, WIRING_TASK_2D.md, WIRING_TASK_4.md   (per-task Editor wiring)
|  |- TASK_2D_SUMMARY.md, TASK_4_SUMMARY.md                                       (per-task post-mortems)
|  |- FEATURE_A_SUMMARY.md, FEATURE_B_SUMMARY.md, FEATURE_C_SUMMARY.md            (feature design notes)
|  |- WIRING_FEATURE_A.md, WIRING_FEATURE_B.md, WIRING_FEATURE_C.md               (per-feature Editor wiring)
|- initial-docs-outdated/
|  |- VR_Public_Speaking_Trainer_Plan.docx
|  |- Android_Launch_Debug_Log.docx
|- crashlogs/
|- vosk-unity-asr-master/       (legacy Vosk STT experiment — superseded by Google Cloud STT)
```

## Setup For New Contributors

### Prerequisites

- Windows machine recommended
- Unity 6.4 (worked on with `6000.4.1f1`)
- Android Build Support in Unity Hub (Android SDK & NDK Tools, OpenJDK)
- Git
- Optional: Visual Studio or Rider for C#; ADB for device testing
- Test device used so far: Samsung Galaxy A34 (SM-A346B)

### Clone The Repository

```powershell
git clone https://github.com/Sabin-git/SpeakAppStudents.git
cd SpeakAppStudents
```

### Open The Unity Project

1. Open Unity Hub.
2. Choose `Add project`.
3. Select `SpeakAppStudents/VRSpeakingTrainer`.
4. Open the project in Unity 6.4.

### First Checks After Opening

1. Let Unity finish importing packages and assets.
2. Add `Assets/StreamingAssets/config.json` with your Google STT key (it's gitignored), or use the developer mock mode.
3. Open `Assets/Scenes/Session.unity` and confirm the project compiles without script errors.
4. Check the three scenes exist: `MainMenu`, `Session`, `Results`.

## Android Build Setup

Verify these Unity settings before building:

- Platform: Android
- Scripting Backend: IL2CPP
- Target Architecture: ARM64
- Minimum API Level: 26
- Target API Level: 35
- XR Plug-in Management: Cardboard enabled on Android
- Graphics API: OpenGLES3 only
- Application Entry: Activity / `UnityPlayerActivity`

Also confirm the custom Android files are present:

- `Assets/Plugins/Android/AndroidManifest.xml`
- `Assets/Plugins/Android/mainTemplate.gradle`

## Cardboard / Package Notes

The project depends on a specific stabilized Cardboard configuration. If a fresh clone is missing packages, check `Packages/manifest.json` and confirm the Cardboard plugin is present from the git source above rather than a newer registry package (the latest release dropped the tested setup).

## Working With The Classroom

The `Session` scene contains a saved classroom layout; an editor helper is also available:

- Menu: `VR Trainer -> Build All`
- Menu: `VR Trainer -> Clear All`
- Menu: `VR Trainer -> Sanitize Audience Setup` (strips stray `AudienceMember` components if the audience-size slider misbehaves)

`ClassroomBuilder.cs` builds the room shell, 10-seat layout, lectern, `AudienceTarget`, `LecternTarget`, and placeholder avatar anchors. To regenerate, clear first, then rebuild.

## Recommended Workflow

1. Open the Unity project.
2. Use Play Mode for fast iteration (speech, rule engine, UI, notes all work in-editor; gaze/VR rendering need a device).
3. For features touched on this branch, the matching `FEATURE_*_SUMMARY.md` (what/why) and `WIRING_FEATURE_*.md` (exact Editor steps) are the reference.
4. Test on Android at the end of each meaningful stage.
5. Treat `CLAUDE.md` as the authoritative architecture reference and this README as the high-level overview.
6. Treat `initial-docs-outdated/` as historical context only.

## Legacy Documentation Notes

The initial planning documents in `initial-docs-outdated/` remain useful for the original architecture intent, rule-engine data flow, Vosk integration goals, and onboarding context. The Android launch debug log explains the device used, the launch-issue history, and why the project uses `UnityPlayerActivity` with a stabilized Android configuration.

Outdated items in those older docs include: 20+-seat classroom assumptions, pre-fix Android startup assumptions, Vosk-based STT (superseded by Google Cloud STT), brightness/vignette comfort controls (removed), and any Vulkan / GameActivity suggestions.
