# Hard Rules for XR8WebAR Project

## 1. Brain Location
**ALWAYS** read `.agents/brain/walkthrough.md` before starting any work on this project. It contains the full architecture, file map, current state, and known issues.

## 2. No 8th Wall App Keys
The 8th Wall engine is **self-hosted**. There are NO app keys, NO cloud project IDs, NO 8th Wall console accounts. Never ask for or try to configure an 8th Wall app key. The engine binary is at `Assets/WebGLTemplates/8thWallTracker/xr8.js`.

## 3. Branch Workflow
Create feature branches for new work. Build, test, and merge to `main` when stable:
```
git checkout -b feature/my-feature
# ... work ...
git checkout main && git merge feature/my-feature
```

## 4. Unity Version
Use **Unity 6 LTS** (6003.1.9f1). Do not upgrade without user approval.

## 5. Assembly Definitions
- Runtime: `Assets/XR8WebAR/Runtime/XR8WebAR.Runtime.asmdef`
- Editor: `Assets/XR8WebAR/Editor/XR8WebAR.Editor.asmdef`
Editor scripts MUST go in the Editor folder. Runtime scripts MUST NOT reference UnityEditor.

## 6. JS Bridge Pattern
All C#↔JS communication uses:
- `.jslib` files with `DllImport("__Internal")` on the C# side
- `window.XR8*Bridge` classes in `xr8-bridge.js`
- `SendMessage()` from JS to Unity
- CSV-formatted strings for data transfer

## 7. Image Targets
Image target files go in `Assets/image-targets/`. The editor auto-discovers JSON files there. Never hardcode target IDs — always read from the JSON.

## 8. Update the Brain
When finishing a session, **always update** `.agents/brain/walkthrough.md` with what was done and what's next.
