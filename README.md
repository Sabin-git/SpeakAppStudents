# VR Public Speaking Trainer - SpeakAppStudents

Mobile VR public-speaking practice app for Android using Unity and Google Cardboard. The user presents in a virtual classroom, the app listens to speech in real time, computes speaking metrics, tracks gaze zones, and drives a rule-based virtual audience. The long-term goal is an end-of-session feedback report combining speech and gaze performance.

This repository contains the active Unity project in [VRSpeakingTrainer](VRSpeakingTrainer). This README summarizes the current architecture, constraints, and development direction. The files in `initial-docs-outdated/` are useful historical planning notes, but parts of them no longer match the current implementation.

## Project Goals

- Practice public speaking in a virtual classroom on Android with Google Cardboard.
- Run fully offline at runtime.
- Use on-device speech recognition with Vosk.
- Keep audience adaptation deterministic and explainable through a rule-based system.
- Provide end-of-session feedback based on speech and gaze metrics.

## Current Design Summary

- Engine: Unity 6.4
- VR SDK: Google Cardboard XR Plugin
- Speech recognition: Vosk offline model
- Target platform: Android, IL2CPP, ARM64, min API 26, target API 35
- UI: TextMeshPro
- Assets: Unity built-ins, ProBuilder, Mixamo, Vosk

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

The project is still under staged development. Broadly:

- Stage 1 `VR Foundation`: complete
- Stage 2 `Classroom Environment`: in progress, with the classroom, lectern, gaze targets, and 10-seat layout now established
- Stage 3+ systems exist only partially as stubs or early implementations

## Development Stages

This roadmap reflects the current project plan and intended implementation order.

- [x] Stage 1 - VR Foundation
  Cardboard rig, scenes, first Android build
- [x] Stage 2 - Classroom Environment
  ProBuilder room, 10 avatars in 2x5 rows, decorative blackboard on back wall, angled lectern with slide and notes panels, `AudienceTarget` and `LecternTarget`
- [x] Stage 3 - Session System
  `SessionManager`, user-set countdown timer, screen-space HUD, pause menu, volume key slide control stub
- [x] Stage 4 - Vosk Integration
  Microphone input, transcription, live transcript on HUD
- [x] Stage 5 - Speech Metrics
  `SpeechAnalyzer`, WPM, pauses, filler-word tracking, HUD display
- [x] Stage 6 - Rule Engine + Audience AI
  `AudienceRuleEngine`, `AudienceController`, audio behavior, plus `HeadTracker` gaze classification and timing
- [x] Stage 7 - Avatar Animations
  Animator controller, per-avatar randomisation, individual gaze reactions, polish
- [ ] Stage 8 - Slide System - PC Pre-processing
  `convert_pptx.py`, `SlideController`, PNG slide loading, `notes.json`, volume-key navigation
- [ ] Stage 9 - Integration + Hardening
  End-to-end testing, GC profiling, signed APK
- [ ] Stage 10 - Results UI
  Speech stats, gaze zone breakdown, percentages and raw seconds, tip generation
- [ ] Stage 11 - On-device PPTX Parsing
  Android file picker and runtime PPTX-to-slides-and-notes pipeline
- [ ] Stage 12 - User Study Prep
  Protocol, questionnaire, multiple avatar models, final build

## Big TODO

- [ ] Port the app to iOS as a follow-up platform target.
  This is not part of the current Android-first stabilized setup and will require dedicated platform work for XR/runtime behavior, input, permissions, plugin packaging, and file access.

### Scripts Currently Present

- `SessionManager.cs`
- `XRLifecycleManager.cs`
- `SpeechMetrics.cs`
- `HeadTracker.cs`
- `SlideController.cs`
- `HUDController.cs`
- `Editor/ClassroomBuilder.cs`

Several later-stage systems described in the design docs are still planned but not implemented yet, including full Vosk transcription, speech analysis, audience rule evaluation, avatar behavior, and results reporting.

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
|- VRSpeakingTrainer/
|  |- Assets/
|  |- Packages/
|  |- ProjectSettings/
|- initial-docs-outdated/
|  |- VR_Public_Speaking_Trainer_Plan.docx
|  |- Android_Launch_Debug_Log.docx
|- crashlogs/
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
