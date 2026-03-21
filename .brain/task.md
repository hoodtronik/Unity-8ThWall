# Unity-8ThWall XR8WebAR — Master Task Tracker
**Last Updated:** 2026-03-20 23:15 EDT

## ⚠️ IMPORTANT: Dual Repo Setup
- **PUBLIC** (`origin`): `github.com/hoodtronik/Unity-8ThWall` — free, no plugins  
- **PRIVATE** (`pro`): `github.com/hoodtronik/Unity-8ThWall-Pro` — with premium plugins  
- **All active development pushes to `pro` remote**
- Only push to `origin` for open-source releases (plugins are gitignored)
- **Unity Version:** 6000.3.9f1

## Completed ✅
- [x] NotebookLM MCP + notebook (25+ sources, 4 notes)
- [x] WebGLBuilder.cs editor script
- [x] Desktop preview rewrite (interactive controls: T/Tab/R/Esc/mouse/scroll)
- [x] Multi-target tracking (gallery-target + info-target)
- [x] Face tracking polish (rotation, offsets, smoothing, desktop preview, expressions 1-5)
- [x] Deprecated FindObjectOfType → FindFirstObjectByType (×4)
- [x] Gaussian Splat pipeline (shader + loader + renderer, WebGL-compatible, no compute)
- [x] Gaussian Splat Importer (one-click .ply/.splat → prefab)
- [x] XR8 Setup Wizard (3-tab: Setup/Validate/Build)
- [x] Image Trackability Analyzer (score images 0-100)
- [x] Tracking Quality Presets (Performance/Balanced/Quality)
- [x] Combined Image+World Tracker (gallery/museum AR)
- [x] XR8Manager: worldTracker + combinedTracker auto-find
- [x] Scene Templates (5 original: Image+Video, Gallery, Portal, Face, World)
- [x] Portal Shaders (PortalMask + PortalInterior stencil shaders)
- [x] XR8 Optimize Scene tool (analysis + optimization pass)
- [x] Dual repo setup (public + private Pro with plugins)
- [x] Plugin compatibility fixes (Amplify, MeshAnimator, GPUInstancer for Unity 6)
- [x] XR8MeshOptimizer.cs — runtime GPU instancing + static batching + scene stats
- [x] XR8ARCrowd.cs — animated crowd spawner (Mesh Animator + GPU Instancer + pooling)
- [x] XR8TweenFX.cs — DOTween-powered AR effects (reveal, float, pulse, rotate, billboard)
- [x] 19 Scene Template Generator (XR8SceneGenerator.cs) — all pushed to pro repo
- [x] AR research: 83 deep research sources + web crawl of 50+ WebAR examples
- [x] NotebookLM notes: Plugin capabilities, Scene template brainstorm, Additional assets needed

## Installed Plugins (Pro Version)
- Mesh Baker ($65) — mesh combining + texture atlasing
- Mantis LOD Editor Pro ($50) — auto-LOD generation
- Mesh Animator ($50) — VAT baking (skeletal → texture)
- GPU Instancer Crowd ($80) — GPU instancing + animated crowds
- Amplify Shader Editor ($80) — visual shader graph
- DOTween Pro ($15) — tweening library
- Animation Converter ($25) — animation retargeting
- NaughtyAttributes (free) — inspector UX

## Custom XR8 Components Built
| Script | Purpose |
|---|---|
| XR8Manager.cs | Singleton hub, all tracking modes, desktop preview |
| XR8Camera.cs | Camera feed → Unity texture, background rendering |
| XR8ImageTracker.cs | Image tracking with smoothing + multi-target |
| XR8WorldTracker.cs | SLAM surface detection, hit testing, tap-to-place |
| XR8FaceTracker.cs | Face tracking, 15 attachment points, expressions |
| XR8CombinedTracker.cs | Simultaneous image + world tracking |
| XR8VideoController.cs | Video playback on tracked targets |
| XR8TweenFX.cs | DOTween AR effects (reveal/float/pulse/rotate) |
| XR8ARCrowd.cs | Animated crowd spawner with pooling + GPU instancing |
| XR8MeshOptimizer.cs | Runtime mesh optimization + GPU instancing |
| XR8OptimizeScene.cs | Editor tool: scene analysis + optimization pass |
| XR8SceneTemplates.cs | 5 original scene templates (editor window) |
| XR8SceneGenerator.cs | 19 advanced scene templates (batch generator) |
| GaussianSplat pipeline | Shader + Loader + Renderer (WebGL-compatible) |

## 19 Scene Templates (XR8SceneGenerator.cs)
### 🟢 Ready Now (no extra assets needed)
| # | Scene | Tracking |
|---|---|---|
| 0 | AR Wall Gallery | Image |
| 2 | AR Hidden Layer | Image |
| 4 | AR Outdoor Gallery | World |
| 5 | AR Luxury Portal | World |
| 6 | AR Time Travel Portal | World |
| 7 | AR Cosmic Portal | World |
| 8 | AR Product Showroom Portal | World |
| 11 | AR Scavenger Hunt | Image + World |
| 14 | AR Product Configurator | World |
| 15 | AR Product Placement | World |
| 17 | AR Holiday Theme | Image + World |

### 🟡 Need Extra 3D Assets (user must provide)
| # | Scene | What's Needed | Est. Cost |
|---|---|---|---|
| 1 | Museum Tour | Animated character model — Mixamo (free) | Free |
| 3 | Museum Resurrections | Animal/creature 3D models | $10-30 |
| 9 | Concert Stage | Animated performer + audio reactive shader | $15-40 |
| 10 | AR Storytelling | Character models + TextMeshPro | Free |
| 12 | Magic Mirror | Face accessories (hats, glasses 3D models) | $10-20 |
| 13 | Creature Encounter | Animated creature model → bake w/ Mesh Animator | $10-30 |
| 16 | AR Live Launch | Product 3D model (user provides own) | User content |
| 18 | AR Photo Op | Character model + 3D props | $10-20 |

## USER Testing Required 🧪
- [ ] Open Unity → XR8 WebAR > Generate All Scene Templates → generate all 19
- [ ] Test each generated scene loads correctly
- [ ] WebGL build output → phone test
- [ ] Desktop preview (T/Tab/R/Esc/mouse/scroll)
- [ ] AR Portal → verify stencil effect in WebGL build
- [ ] Gallery Mode (combined tracking) → verify floor detection
- [ ] Optimize Scene tool → test analysis on real scene

## TO-DO: What User Needs to Provide 📋
- [ ] 3D character model for Museum Tour guide (or grab from Mixamo.com)
- [ ] 3D creature/animal models for Museum Resurrections & Creature Encounter
- [ ] 3D accessories (hats, glasses, masks) for Magic Mirror face filter
- [ ] Product 3D model for AR Live Launch template
- [ ] Performer model + audio clip for Concert Stage
- [ ] Props (hats, frames, stars) for Photo Op
- [ ] Head OBJ for face preview mesh (mentioned in earlier session)
- [ ] Reference image targets for each scene template

## Backlog 📋
- [ ] Character animation pipeline: iClone → Unity → Mesh Animator VAT baking
- [ ] Mesh Animator VAT integration for animated crowds in portals
- [ ] Face mesh rendering component
- [ ] Gaussian Splat: test with real .ply file
- [ ] WebGL performance profiling on mobile
- [ ] Audio reactive shader for Concert Stage (Amplify Shader Editor)
- [ ] Dissolve/reveal shader for Hidden Layer (Amplify Shader Editor)
- [ ] GPS/geofencing for Outdoor Gallery (may need additional plugin)
- [ ] Screenshot/share feature for Photo Op template
- [ ] Multi-clue system for Scavenger Hunt (state machine)

## NotebookLM Notes
- 🔌 Plugin Capabilities & XR8 Power Components Reference
- 🎨 AR Art & Interactive Experience Ideas — Scene Template Brainstorm
- 🛒 Scene Templates — Additional Assets Needed
- Session progress notes (this file mirrors to notebook)
