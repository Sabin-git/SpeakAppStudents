# Wiring — Task 2b: Audience Animation Fix

**No scene changes required for this task.** All changes are in `AudienceMember.cs` and `CLAUDE.md`. No prefabs, scenes, assets, or animator controllers need to be re-wired or re-saved.

## What changed

- DistractionReact one-shot trigger logic removed from `AudienceMember.cs` (was the source of the "fall through chair" bug).
- Added per-avatar variant cycling: each avatar now picks a new clip variant for its current state every 8–15 seconds, randomised per avatar with a 0–8s initial offset.
- Widened the state-transition delay window from 0–5s to 0–8s for a more visibly staggered transition across the audience.

## Test checklist

Run these in the Unity Editor (Play Mode) after the changes are on disk. No Inspector wiring required.

1. **Variant cycling within a single state.**
   Enter Play Mode. Pick one avatar (any of the 10 around the room) and watch it for **30+ seconds** during a single audience state (e.g. force `Engaged` via the dev panel "Force Audience State" if needed). Confirm it cycles through **2 or more different clip variants** during that window — the visual should change without the avatar entering a new audience state. Note: if a state only has 1 clip variant wired (i.e. `_variantCounts[state] == 1`), the cycle will still fire but the visual won't change — this is expected.

2. **No fall-through-chair in Restless.**
   Trigger Restless. Easiest path: open the dev panel and use "Force Audience State" → Restless. If the dev panel doesn't expose force-state at runtime, speak a lot of filler words ("um", "uh", "like") into the mic for 30+ seconds to drive the rule engine into Restless. Confirm:
   - Restless avatars play their base clip (same as Neutral) and cycle variants normally.
   - **No one-shot reaction triggers fire.** No avatar suddenly leans left/right "telling a secret".
   - **No avatar's hips clip through the chair seat at any point.** Watch the seated pose of every visible Restless avatar for at least 30 seconds.

3. **5-minute mock-mode soak test.**
   Set Mock Mode ON in the dev panel and run a 5-minute session. Visually scan all 10 avatars at intervals (1 min, 2.5 min, 5 min). Confirm **no avatar falls through its chair at any point**, regardless of which audience state is active.

4. **Staggered global transition.**
   When the rule engine triggers a global state change (e.g. WPM drops into the Distracted band), observe the audience: avatars should transition over a **noticeable 0–8 second window**, not all on the same frame. Some flip immediately, others trail behind by up to 8s. A useful way to force a global transition: in the dev panel, toggle "Force Audience State" off and on (or change the forced state), then watch the audience react.

## Animator controller note

The `DistractionReacting` state in `Assets/Animations/AudienceAnimator.controller` is now **dead code** — `AudienceMember` no longer fires the `DistractionReact` trigger that transitions into it. It can be cleaned up via the Animator window if desired (open the controller, select the `DistractionReacting` state on the Base Layer, delete it), but it is **harmless to leave** — Unity will never enter it at runtime because no script sets the trigger.

The wiring code in `Assets/Scripts/Editor/AnimatorClipWirer.cs` that creates this state has been intentionally left in place, in case reactions are reintroduced later (likely as a masked upper-body Animator layer — see the original "fall through chair" analysis for options).
