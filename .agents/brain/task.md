# XR8WebAR Unity Addon — Task List

## Completed
- [x] Base addon: XR8Camera, XR8ImageTracker, XR8VideoController, XR8EngineStatus, XR8ScreenCapture
- [x] Git repo initialized, main branch
- [x] feature/xr8-manager: Unified controller, world tracking, desktop preview mode → merged
- [x] Fix InputSystem compile errors
- [x] Custom inspector: auto-discovery, thumbnails, drag-and-drop, debug buttons
- [x] Fix desktop preview race condition (waits for IsReady)
- [x] feature/face-tracking: XR8FaceTracker, expressions, attachment points, landmarks → merged
- [x] feature/editor-inspector: XR8ManagerEditor with colored toggles, contextual config → merged
- [x] feature/prefab-library: Menu items (AR Camera Rig, Manager, Trackers, full scene setup) → merged

## Pinned (Later)
- [ ] Desktop preview mode — video doesn't play visually; needs deeper investigation
- [x] Visible image target preview in editor scene view (always-on gizmos)

## Roadmap
- [ ] Gaussian Splat support — integrate [mobile-gs](https://github.com/xiaobiaodu/mobile-gs) splat renderer, create XR8GaussianSplatTarget component, test WebGL + mobile
- [ ] Multi-target tracking test
- [ ] Face tracking attachment point polish
- [ ] WebGL build + phone test
