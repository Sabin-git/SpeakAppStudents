# VR Public Speaking Trainer - SpeakAppStudents

Mobile VR public-speaking practice app for Android using Unity and Google Cardboard. The user presents in a virtual classroom, the app listens to speech in real time, computes speaking metrics, tracks gaze zones, and drives a rule-based virtual audience. The long-term goal is an end-of-session feedback report combining speech and gaze performance.

This repository contains the active Unity project in [VRSpeakingTrainer](VRSpeakingTrainer). This README summarizes the current architecture, constraints, and development direction. The files in `initial-docs-outdated/` are useful historical planning notes, but parts of them no longer match the current implementation.

## Project Goals

- Practice public speaking in a virtual classroom on Android with Google Cardboard.
- Use real-time speech recognition (Google Cloud STT) with a mock mode for offline testing.
- Keep audience adaptation deterministic and explainable through a rule-based system.
- Provide end-of-session feedback based on speech and gaze metrics.

## Current Design Summary

- Engine: Unity 6.4
- VR SDK: Google Cardboard XR Plugin
- Speech recognition: Google Cloud Speech-to-Text REST API (with developer mock mode for offline testing)
- Languages: English and Dutch — UI strings, STT language code, filler lists, and WPM thresholds all switch together (default English)
- Target platform: Android, IL2CPP, ARM64, min API 26, target API 35
- UI: TextMeshPro, localized via a JSON dictionary (`StreamingAssets/en.json` / `nl.json`)
- Assets: Unity built-ins, ProBuilder, Mixamo

### Current Scene Structure

- `MainMenu`: start flow and session setup UI
- `Session`: XR rig, classroom, lectern, audience, scripts
- `Results`: post-session feedback scene

### Current Classroom Spec

- Presenter stands at the front of the room facing the audience.
- Blackboard is on the back wall behind the audience and is decorative only.
- Audience count is exactly 10 avatars in 2 rows of 5.
- The lectern is the only interactive surface.
- Slides and speaker notes are both displayed on the lectern surface.
- Gaze targets are `AudienceTarget` and `LecternTarget`.
- `SlidesTarget` and `NotesTarget` do not exist as separate objects.

### Current Session Flow

- User starts from `MainMenu`.
- Session runs in `Session` scene with a timer.
- Android back / Escape during session opens a flat confirmation overlay.
- Cancel resumes XR.
- Confirm exit returns to `MainMenu`.
- Session timeout ends the session and goes to `Results`.

## Current Implementation Status

Stages 1–6 are complete. The project is mid-way through Stages 7b and 7c (visual overhaul and developer tooling). The core session loop is fully functional: session start → speech recognition → speech metrics → rule-based audience AI → head tracking → session end → results screen. A developer panel on the main menu allows testing without the real API.

In parallel with the stage roadmap, the project is working through the user-study readiness tasks in [VRSpeakingTrainer/USER_TESTING_PLAN.md](VRSpeakingTrainer/USER_TESTING_PLAN.md). Landed so far: **Task 1 — Dutch language support** (full bilingual EN/NL: UI, STT language code, filler lists, and WPM thresholds) and **Task 2 — Settings menu + first-launch consent gate** (a user-facing Settings panel with Language, Privacy/consent, Audience responsiveness, and VR-comfort brightness/vignetting controls). Remaining testing-plan work includes gaze control + heat map (Task 4) and PPTX support (Task 3). For the authoritative runtime architecture and the full script / PlayerPrefs reference, see [VRSpeakingTrainer/CLAUDE.md](VRSpeakingTrainer/CLAUDE.md).

## Development Stages

This roadmap reflects the current project plan and intended implementation order.

- [x] Stage 1 - VR Foundation
  Cardboard rig, scenes, first Android build
- [x] Stage 2 - Classroom Environment
  ProBuilder room, 10 avatars in 2×5 rows, decorative blackboard on back wall, angled lectern with slide and notes panels, `AudienceTarget` and `LecternTarget`
- [x] Stage 3 - Session System
  `SessionManager`, user-set countdown timer, screen-space HUD, pause menu with Finish/Exit/Resume, volume key slide control stub
- [x] Stage 4 - Google Cloud STT Integration
  `SpeechRecognizer.cs`, 5s audio chunks, REST API, live transcript on HUD
- [x] Stage 5 - Speech Metrics
  `SpeechAnalyzer`, WPM rolling window, pause detection, filler-word tracking, HUD display
- [x] Stage 6 - Rule Engine + Audience AI
  `AudienceRuleEngine` rule table + head modifiers, `AudienceController` state propagation + audio crossfade, `HeadTracker` zone classification + per-avatar gaze
- [ ] Stage 7b - Visual Overhaul *(in progress)*
  Dark academic theme on all scenes; `ResultsUI.cs` with scored breakdown; metrics persisted via PlayerPrefs; Results scene UI build in Unity Editor pending
- [ ] Stage 7c - Developer Panel *(in progress)*
  `MainMenuController.cs` rewrite; developer overlay with debug mode, mock mode, custom duration, audience/gaze overrides; Unity Editor wiring mostly complete — Audience and Gaze sections pending
- [ ] Stage 8 - Avatar Animations
  AnimatorController with `State` int parameter; per-avatar randomised switch delay; individual gaze reactions; idle animations per state
- [ ] Stage 9 - Slide System (PC Pipeline)
  `convert_pptx.py` tool; `SlideController.cs` loading PNGs + `notes.json`; volume-key navigation; fallback to blank if no slides
- [ ] Stage 10 - Integration + Hardening
  End-to-end test pass on device, GC profiling, signed APK
- [ ] Stage 11 - On-device PPTX Parsing *(stretch)*
  Android file picker on MainMenu, runtime PPTX → PNG + notes pipeline
- [ ] Stage 12 - User Study Prep
  Multiple avatar models, study protocol, questionnaire, final polished build

## Big TODO

- [ ] Port the app to iOS as a follow-up platform target.
  This is not part of the current Android-first stabilized setup and will require dedicated platform work for XR/runtime behavior, input, permissions, plugin packaging, and file access.

### Scripts Currently Present

Runtime MonoBehaviours (13):

- `SessionManager.cs` — session lifecycle, timer, pause menu
- `XRLifecycleManager.cs` — Cardboard XR rig management
- `SpeechRecognizer.cs` — Google Cloud STT, mic input, mock mode (EN/NL language code)
- `SpeechAnalyzer.cs` — WPM, pauses, filler words (per-language filler lists)
- `AudienceRuleEngine.cs` — rule table + head modifiers → AudienceState (per-language + per-responsiveness thresholds)
- `AudienceController.cs` — state propagation, audio crossfade
- `AudienceMember.cs` — per-avatar state, randomised switching
- `HeadTracker.cs` — gaze zone classification, time accumulation
- `HUDController.cs` — screen-space countdown + transcript HUD
- `SlideController.cs` — slide index, texture swap, notes text
- `MainMenuController.cs` — main menu panels, developer settings, first-launch consent gate
- `ResultsUI.cs` — post-session scored breakdown
- `SettingsMenuController.cs` — user-facing Settings panel (language, privacy/consent, audience responsiveness, VR comfort)

Runtime utility / data (do not count against the script cap):

- `SpeechMetrics.cs` — shared data struct
- `Localization.cs` — static JSON-dictionary localization (EN/NL); source of truth for `CurrentLanguage`

Editor-only utilities (under `Assets/Scripts/Editor/`, not runtime):

- `ClassroomBuilder.cs` — room shell, desk grid, avatar anchors, audience sanitize
- `AnimatorClipWirer.cs` — wires per-state blend trees into the audience Animator
- `AvatarLayerSetup.cs`, `AvatarMaterialFixer.cs`, `AnimationImportFixer.cs`, `ClipOrganizer.cs`, `VRTrainerSetup.cs` — Mixamo / animation import + setup helpers

## Core Constraints

These come from the active project guidance and should be preserved:

- Offline only at runtime
- Rule-based AI only, no ML / black-box audience logic
- Android-first development
- Keep the codebase small, roughly 10-12 scripts total
- No paid asset dependency
- Avoid `\n` in TextMeshPro/UI strings; use separate UI elements instead

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
|  |- USER_TESTING_PLAN.md      (user-study readiness task plan)
|  |- VISUALS.md                (dark-academic colour palette + typography)
|  |- WIRING_CONVENTIONS.md     (Unity UI primer for the WIRING_TASK_N docs)
|  |- WIRING_TASK_2.md, WIRING_TASK_2B.md   (per-task Unity Editor wiring steps)
|- initial-docs-outdated/
|  |- VR_Public_Speaking_Trainer_Plan.docx
|  |- Android_Launch_Debug_Log.docx
|- crashlogs/
|- vosk-unity-asr-master/       (legacy Vosk STT experiment — superseded by Google Cloud STT)
```

## Setup For New Contributors

### Prerequisites

- Windows machine recommended
- Unity 6.4
  - The project has been worked on with `6000.4.1f1`
- Android Build Support installed in Unity Hub
  - Android SDK & NDK Tools
  - OpenJDK
- Git
- Optional but recommended:
  - Visual Studio or Rider for C# editing
  - ADB access for device testing
- Test device used so far:
  - Samsung Galaxy A34 (SM-A346B)

### Clone The Repository

```powershell
git clone https://github.com/Sabin-git/SpeakAppStudents.git
cd SpeakAppStudents
```

### Open The Unity Project

1. Open Unity Hub.
2. Choose `Add project`.
3. Select:
   - `SpeakAppStudents/VRSpeakingTrainer`
4. Open the project in Unity 6.4.

### First Checks After Opening

1. Let Unity finish importing packages and assets.
2. Open `Assets/Scenes/Session.unity`.
3. Confirm the project opens without script compile errors.
4. Check that the three scenes exist:
   - `MainMenu`
   - `Session`
   - `Results`

## Android Build Setup

After cloning, verify these Unity settings before building:

- Platform: Android
- Scripting Backend: IL2CPP
- Target Architecture: ARM64
- Minimum API Level: 26
- Target API Level: 35
- XR Plug-in Management:
  - Cardboard enabled on Android
- Graphics API:
  - OpenGLES3 only
- Application Entry:
  - Activity / `UnityPlayerActivity`

Also verify the custom Android files are present:

- `Assets/Plugins/Android/AndroidManifest.xml`
- `Assets/Plugins/Android/mainTemplate.gradle`

## Cardboard / Package Notes

The initial planning docs assumed a simpler first-pass Android setup, but the project now depends on a specific stabilized Cardboard configuration. If a new clone is missing packages, start by checking `Packages/manifest.json` and confirming the Cardboard plugin is present from the git source above rather than substituting a newer registry package.

## Working With The Classroom

The `Session` scene currently contains a saved classroom layout, but the editor helper is also available:

- Menu: `VR Trainer -> Build All`
- Menu: `VR Trainer -> Clear All`

`ClassroomBuilder.cs` is the editor utility responsible for:

- Room shell
- 10-seat classroom layout
- Lectern geometry
- `AudienceTarget`
- `LecternTarget`
- Placeholder avatar anchors

If the classroom needs to be regenerated, clear first and then rebuild.

## Recommended Workflow

Based on the active project workflow:

1. Open the Unity project.
2. Work stage-by-stage, maintaining the stage order.
3. Use Play Mode for fast iteration where possible.
4. Test on Android at the end of each meaningful stage.
5. Treat this README and the current project state as the main reference for ongoing development.
6. Treat `initial-docs-outdated/` as historical context only.

## Legacy Documentation Notes

The initial planning documents `initial-docs-outdated/` are still useful for:

- original system architecture
- intended rule-engine data flow
- Vosk integration goals
- development staging approach
- contributor onboarding context

The Android launch debug log is useful for:

- the device used during debugging
- the launch issue history
- why the project now explicitly uses `UnityPlayerActivity`
- why the stabilized Android configuration should be preserved

Outdated items in those older docs include:

- old classroom layout assumptions such as 20+ seats
- earlier Android assumptions that predate the final startup fix
- earlier recommendations that mentioned Vulkan or GameActivity-related possibilities
