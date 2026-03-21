using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Batch Scene Generator — creates 19 AR scene templates as .unity files.
    /// Access via: XR8 WebAR > Generate All Scene Templates
    /// </summary>
    public class XR8SceneGenerator : EditorWindow
    {
        private const string ScenesRoot = "Assets/Scenes/Templates";
        private bool[] selected = new bool[19];
        private Vector2 scroll;
        private static readonly string[] SceneNames = {
            "AR Wall Gallery",
            "AR Museum Tour",
            "AR Hidden Layer",
            "AR Museum Resurrections",
            "AR Outdoor Gallery",
            "AR Luxury Portal",
            "AR Time Travel Portal",
            "AR Cosmic Portal",
            "AR Product Showroom Portal",
            "AR Concert Stage",
            "AR Storytelling",
            "AR Scavenger Hunt",
            "AR Magic Mirror",
            "AR Creature Encounter",
            "AR Product Configurator",
            "AR Product Placement",
            "AR Live Launch",
            "AR Holiday Theme",
            "AR Photo Op"
        };

        [MenuItem("XR8 WebAR/Generate All Scene Templates", false, 2)]
        public static void ShowWindow()
        {
            var win = GetWindow<XR8SceneGenerator>("Scene Generator");
            win.minSize = new Vector2(440, 560);
            for (int i = 0; i < win.selected.Length; i++) win.selected[i] = true;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("🎨 AR Scene Template Generator", title);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Generate pre-configured AR scene templates.\n" +
                "Each scene includes XR8Manager, Camera, Lighting, and use-case specific content.\n" +
                "Scenes are saved to: " + ScenesRoot, MessageType.Info);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All")) for (int i = 0; i < selected.Length; i++) selected[i] = true;
            if (GUILayout.Button("Deselect All")) for (int i = 0; i < selected.Length; i++) selected[i] = false;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            string[] cats = { "🖼 GALLERY & ART", "", "", "", "",
                              "🚪 PORTALS", "", "", "",
                              "🎭 INTERACTIVE", "", "", "", "",
                              "📦 PRODUCT VIZ", "", "",
                              "🎉 EVENTS", "" };
            string lastCat = "";
            for (int i = 0; i < SceneNames.Length; i++)
            {
                if (cats[i] != "" && cats[i] != lastCat) {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(cats[i], EditorStyles.boldLabel);
                    lastCat = cats[i];
                }
                selected[i] = EditorGUILayout.ToggleLeft("  " + SceneNames[i], selected[i]);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("🚀 Generate Selected Scenes", GUILayout.Height(32)))
            {
                int count = 0;
                for (int i = 0; i < selected.Length; i++) if (selected[i]) count++;
                if (count == 0) { EditorUtility.DisplayDialog("Nothing Selected", "Select at least one scene.", "OK"); return; }
                if (EditorUtility.DisplayDialog("Generate Scenes",
                    $"Create {count} scene(s) in {ScenesRoot}?\nExisting scenes with matching names will be overwritten.", "Generate", "Cancel"))
                {
                    GenerateScenes();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void GenerateScenes()
        {
            EnsureFolder(ScenesRoot);
            int created = 0;
            for (int i = 0; i < SceneNames.Length; i++)
            {
                if (!selected[i]) continue;
                EditorUtility.DisplayProgressBar("Generating Scenes", SceneNames[i], (float)i / SceneNames.Length);
                CreateScene(i);
                created++;
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Done! 🎉", $"Created {created} scene template(s) in\n{ScenesRoot}", "OK");
            Debug.Log($"[XR8 Generator] ✅ Created {created} scene templates in {ScenesRoot}");
        }

        // =============================================================
        // SCENE CREATION
        // =============================================================

        private void CreateScene(int index)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Shared setup: camera, light, manager
            var camObj = CreateCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();
            CreateLight();

            switch (index)
            {
                case 0: Build_WallGallery(cam, xr8Cam); break;
                case 1: Build_MuseumTour(cam, xr8Cam); break;
                case 2: Build_HiddenLayer(cam, xr8Cam); break;
                case 3: Build_MuseumResurrections(cam, xr8Cam); break;
                case 4: Build_OutdoorGallery(cam, xr8Cam); break;
                case 5: Build_LuxuryPortal(cam, xr8Cam); break;
                case 6: Build_TimeTravelPortal(cam, xr8Cam); break;
                case 7: Build_CosmicPortal(cam, xr8Cam); break;
                case 8: Build_ProductShowroomPortal(cam, xr8Cam); break;
                case 9: Build_ConcertStage(cam, xr8Cam); break;
                case 10: Build_Storytelling(cam, xr8Cam); break;
                case 11: Build_ScavengerHunt(cam, xr8Cam); break;
                case 12: Build_MagicMirror(cam, xr8Cam); break;
                case 13: Build_CreatureEncounter(cam, xr8Cam); break;
                case 14: Build_ProductConfigurator(cam, xr8Cam); break;
                case 15: Build_ProductPlacement(cam, xr8Cam); break;
                case 16: Build_LiveLaunch(cam, xr8Cam); break;
                case 17: Build_HolidayTheme(cam, xr8Cam); break;
                case 18: Build_PhotoOp(cam, xr8Cam); break;
            }

            string path = $"{ScenesRoot}/{SceneNames[index]}.unity";
            EditorSceneManager.SaveScene(scene, path);
        }

        // =============================================================
        // SHARED BUILDERS
        // =============================================================

        private GameObject CreateCamera()
        {
            var obj = new GameObject("Main Camera");
            obj.tag = "MainCamera";
            var cam = obj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            obj.AddComponent<AudioListener>();
            obj.AddComponent<XR8Camera>();
            return obj;
        }

        private GameObject CreateLight()
        {
            var obj = new GameObject("Directional Light");
            var light = obj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.84f);
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            obj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            obj.transform.position = new Vector3(0, 3, 0);
            return obj;
        }

        private GameObject CreateManagerObj(Camera cam, XR8Camera xr8Cam,
            bool image = false, bool world = false, bool face = false)
        {
            var obj = new GameObject("XR8Manager");
            var mgr = obj.AddComponent<XR8Manager>();
            var so = new SerializedObject(mgr);
            so.FindProperty("arCamera").objectReferenceValue = cam;
            so.FindProperty("xr8CameraComponent").objectReferenceValue = xr8Cam;
            so.FindProperty("enableImageTracking").boolValue = image;
            so.FindProperty("enableWorldTracking").boolValue = world;
            so.FindProperty("enableFaceTracking").boolValue = face;
            so.FindProperty("enableDesktopPreview").boolValue = true;
            so.FindProperty("enableSurfaceDetection").boolValue = world;
            so.ApplyModifiedProperties();
            return obj;
        }

        private GameObject CreateImageTrackerObj(XR8Camera xr8Cam, string targetName, out Transform contentRoot)
        {
            var obj = new GameObject("XR8ImageTracker");
            var tracker = obj.AddComponent<XR8ImageTracker>();
            var content = new GameObject(targetName + "_Content");
            content.transform.SetParent(obj.transform);
            content.transform.localPosition = Vector3.zero;
            contentRoot = content.transform;

            var so = new SerializedObject(tracker);
            so.FindProperty("trackerCam").objectReferenceValue = xr8Cam;
            var targets = so.FindProperty("imageTargets");
            targets.ClearArray();
            targets.InsertArrayElementAtIndex(0);
            var elem = targets.GetArrayElementAtIndex(0);
            elem.FindPropertyRelative("id").stringValue = targetName;
            elem.FindPropertyRelative("transform").objectReferenceValue = contentRoot;
            so.ApplyModifiedProperties();
            return obj;
        }

        private GameObject CreateWorldTrackerObj(Camera cam = null)
        {
            var obj = new GameObject("XR8WorldTracker");
            var wt = obj.AddComponent<XR8WorldTracker>();
            if (cam != null)
            {
                var so = new SerializedObject(wt);
                so.FindProperty("trackerCam").objectReferenceValue = cam;
                so.ApplyModifiedProperties();
            }
            return obj;
        }

        private void WireManager(GameObject mgrObj, XR8ImageTracker imgTracker = null,
            XR8WorldTracker worldTracker = null, XR8FaceTracker faceTracker = null)
        {
            var so = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            if (imgTracker != null) so.FindProperty("imageTracker").objectReferenceValue = imgTracker;
            if (worldTracker != null) so.FindProperty("worldTracker").objectReferenceValue = worldTracker;
            if (faceTracker != null) so.FindProperty("faceTracker").objectReferenceValue = faceTracker;
            so.ApplyModifiedProperties();
        }

        private GameObject MakePrimitive(string name, PrimitiveType type, Transform parent,
            Vector3 pos, Vector3 scale, Color color, Quaternion? rot = null)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localPosition = pos;
            obj.transform.localScale = scale;
            if (rot.HasValue) obj.transform.localRotation = rot.Value;
            var col = obj.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
            var r = obj.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            r.material.color = color;
            return obj;
        }

        private void AddTweenFX(GameObject obj, bool autoFloat = false)
        {
            var fx = obj.AddComponent<XR8TweenFX>();
            fx.autoFloat = autoFloat;
        }

        // =============================================================
        // GALLERY & ART TEMPLATES (0-4)
        // =============================================================

        // 0: AR Wall Gallery
        private void Build_WallGallery(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, image: true);
            var tracker = CreateImageTrackerObj(xr8Cam, "gallery-wall", out Transform content);
            WireManager(mgr, imgTracker: tracker.GetComponent<XR8ImageTracker>());

            // Art frames grid (3 frames)
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.35f;
                var frame = MakePrimitive($"ArtFrame_{i+1}", PrimitiveType.Cube, content,
                    new Vector3(x, 0, 0), new Vector3(0.28f, 0.005f, 0.2f),
                    new Color(0.15f, 0.12f, 0.1f));
                var canvas = MakePrimitive($"ArtCanvas_{i+1}", PrimitiveType.Quad, frame.transform,
                    new Vector3(0, 0.01f, 0), new Vector3(0.85f, 1f, 0.85f),
                    new Color(0.9f, 0.85f, 0.8f), Quaternion.Euler(90f, 0, 0));
            }
            AddTweenFX(content.gameObject);
        }

        // 1: AR Museum Tour
        private void Build_MuseumTour(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, image: true, world: true);
            var tracker = CreateImageTrackerObj(xr8Cam, "exhibit-marker", out Transform content);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, imgTracker: tracker.GetComponent<XR8ImageTracker>(),
                worldTracker: world.GetComponent<XR8WorldTracker>());

            // Guide character placeholder
            var guide = MakePrimitive("GuideCharacter", PrimitiveType.Capsule, content,
                new Vector3(0.3f, 0.15f, 0), new Vector3(0.08f, 0.15f, 0.08f),
                new Color(0.3f, 0.6f, 0.9f));
            AddTweenFX(guide, autoFloat: true);

            // Info panel
            var panel = MakePrimitive("InfoPanel", PrimitiveType.Cube, content,
                new Vector3(-0.15f, 0.1f, 0), new Vector3(0.25f, 0.005f, 0.15f),
                new Color(0.95f, 0.95f, 0.9f));
        }

        // 2: AR Hidden Layer (Double Exposure)
        private void Build_HiddenLayer(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, image: true);
            var tracker = CreateImageTrackerObj(xr8Cam, "painting-target", out Transform content);
            WireManager(mgr, imgTracker: tracker.GetComponent<XR8ImageTracker>());

            // Visible layer
            var visible = MakePrimitive("VisibleArtwork", PrimitiveType.Quad, content,
                Vector3.zero, new Vector3(0.3f, 0.25f, 1f),
                new Color(0.8f, 0.7f, 0.5f), Quaternion.Euler(90f, 0, 0));

            // Hidden layer (revealed via AR)
            var hidden = MakePrimitive("HiddenArtwork", PrimitiveType.Quad, content,
                new Vector3(0, 0.001f, 0), new Vector3(0.3f, 0.25f, 1f),
                new Color(0.3f, 0.5f, 0.9f, 0.8f), Quaternion.Euler(90f, 0, 0));

            AddTweenFX(content.gameObject);
        }

        // 3: AR Museum Resurrections
        private void Build_MuseumResurrections(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, image: true, world: true);
            var tracker = CreateImageTrackerObj(xr8Cam, "skeleton-exhibit", out Transform content);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, imgTracker: tracker.GetComponent<XR8ImageTracker>(),
                worldTracker: world.GetComponent<XR8WorldTracker>());

            // Creature placeholder
            var creature = MakePrimitive("CreaturePlaceholder", PrimitiveType.Capsule, content,
                new Vector3(0, 0.2f, 0), new Vector3(0.15f, 0.2f, 0.15f),
                new Color(0.85f, 0.75f, 0.6f));
            AddTweenFX(creature, autoFloat: true);

            // Particle indicator
            var sparkle = new GameObject("SparkleEffect");
            sparkle.transform.SetParent(content);
            sparkle.transform.localPosition = new Vector3(0, 0.3f, 0);
        }

        // 4: AR Outdoor Gallery
        private void Build_OutdoorGallery(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, world: true);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, worldTracker: world.GetComponent<XR8WorldTracker>());

            var gallery = new GameObject("OutdoorGalleryContent");
            gallery.transform.position = new Vector3(0, 0, 3f);

            // Floating art pieces at different positions
            string[] names = { "FloatingArt_A", "FloatingArt_B", "FloatingArt_C" };
            Color[] colors = { new Color(0.9f,0.3f,0.3f), new Color(0.3f,0.9f,0.4f), new Color(0.3f,0.4f,0.9f) };
            Vector3[] positions = { new Vector3(-1,1.5f,0), new Vector3(0,2f,1), new Vector3(1,1.2f,-0.5f) };
            for (int i = 0; i < 3; i++)
            {
                var art = MakePrimitive(names[i], PrimitiveType.Cube, gallery.transform,
                    positions[i], new Vector3(0.6f, 0.8f, 0.02f), colors[i]);
                AddTweenFX(art, autoFloat: true);
            }
        }

        // =============================================================
        // PORTAL TEMPLATES (5-8)
        // =============================================================

        private void BuildPortalBase(Camera cam, XR8Camera xr8Cam, string portalName,
            Color frameColor, Color roomColor, out GameObject portal, out Transform interior)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, world: true);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, worldTracker: world.GetComponent<XR8WorldTracker>());

            portal = new GameObject(portalName);
            portal.transform.position = new Vector3(0, 0, 2f);

            // Frame
            MakePrimitive("PortalFrame", PrimitiveType.Cube, portal.transform,
                Vector3.zero, new Vector3(1.2f, 2.2f, 0.05f), frameColor);

            // Mask
            var mask = MakePrimitive("PortalMask", PrimitiveType.Quad, portal.transform,
                new Vector3(0, 0, 0.01f), new Vector3(1f, 2f, 1f), Color.clear);
            var maskShader = Shader.Find("XR8WebAR/PortalMask");
            if (maskShader != null) mask.GetComponent<Renderer>().material = new Material(maskShader);

            // Interior
            var intObj = new GameObject("PortalInterior");
            intObj.transform.SetParent(portal.transform);
            intObj.transform.localPosition = new Vector3(0, 0, -2f);
            interior = intObj.transform;

            // Room walls
            MakePrimitive("Floor", PrimitiveType.Quad, interior,
                new Vector3(0,-1,0), new Vector3(3,4,1), roomColor, Quaternion.Euler(90,0,0));
            MakePrimitive("BackWall", PrimitiveType.Quad, interior,
                new Vector3(0,0,-2), new Vector3(3,2.5f,1), roomColor * 1.1f);
            MakePrimitive("LeftWall", PrimitiveType.Quad, interior,
                new Vector3(-1.5f,0,-1), new Vector3(4,2.5f,1), roomColor, Quaternion.Euler(0,90,0));
            MakePrimitive("RightWall", PrimitiveType.Quad, interior,
                new Vector3(1.5f,0,-1), new Vector3(4,2.5f,1), roomColor, Quaternion.Euler(0,-90,0));
            MakePrimitive("Ceiling", PrimitiveType.Quad, interior,
                new Vector3(0,1.2f,-1), new Vector3(3,4,1), roomColor * 0.7f, Quaternion.Euler(-90,0,0));

            // Portal light
            var lightObj = new GameObject("PortalLight");
            lightObj.transform.SetParent(interior);
            lightObj.transform.localPosition = new Vector3(0, 1, -1);
            var pl = lightObj.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 5f;
            pl.intensity = 1.5f;
            pl.color = roomColor * 2f;
        }

        // 5: Luxury Interior Portal
        private void Build_LuxuryPortal(Camera cam, XR8Camera xr8Cam)
        {
            BuildPortalBase(cam, xr8Cam, "LuxuryPortal",
                new Color(0.7f, 0.55f, 0.3f), new Color(0.15f, 0.12f, 0.1f),
                out var portal, out var interior);

            // Furniture placeholders
            MakePrimitive("Sofa", PrimitiveType.Cube, interior,
                new Vector3(0, -0.6f, -1.5f), new Vector3(1.2f, 0.4f, 0.5f),
                new Color(0.6f, 0.3f, 0.2f));
            MakePrimitive("Table", PrimitiveType.Cylinder, interior,
                new Vector3(0.8f, -0.7f, -0.8f), new Vector3(0.3f, 0.15f, 0.3f),
                new Color(0.4f, 0.25f, 0.15f));
        }

        // 6: Time Travel Portal
        private void Build_TimeTravelPortal(Camera cam, XR8Camera xr8Cam)
        {
            BuildPortalBase(cam, xr8Cam, "TimeTravelPortal",
                new Color(0.5f, 0.4f, 0.3f), new Color(0.2f, 0.18f, 0.15f),
                out var portal, out var interior);

            // Ancient column placeholders
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1f : 1f;
                MakePrimitive($"Column_{i+1}", PrimitiveType.Cylinder, interior,
                    new Vector3(x, -0.2f, -1.5f), new Vector3(0.15f, 0.8f, 0.15f),
                    new Color(0.7f, 0.65f, 0.55f));
            }
        }

        // 7: Cosmic Portal
        private void Build_CosmicPortal(Camera cam, XR8Camera xr8Cam)
        {
            BuildPortalBase(cam, xr8Cam, "CosmicPortal",
                new Color(0.2f, 0.1f, 0.4f), new Color(0.05f, 0.02f, 0.15f),
                out var portal, out var interior);

            // Floating planets
            MakePrimitive("Planet_1", PrimitiveType.Sphere, interior,
                new Vector3(-0.5f, 0.3f, -1f), new Vector3(0.3f, 0.3f, 0.3f),
                new Color(0.4f, 0.6f, 0.9f));
            var p2 = MakePrimitive("Planet_2", PrimitiveType.Sphere, interior,
                new Vector3(0.7f, 0f, -1.5f), new Vector3(0.5f, 0.5f, 0.5f),
                new Color(0.9f, 0.4f, 0.2f));
            AddTweenFX(p2, autoFloat: true);
        }

        // 8: Product Showroom Portal
        private void Build_ProductShowroomPortal(Camera cam, XR8Camera xr8Cam)
        {
            BuildPortalBase(cam, xr8Cam, "ProductShowroomPortal",
                new Color(0.9f, 0.9f, 0.95f), new Color(0.95f, 0.95f, 0.97f),
                out var portal, out var interior);

            // Product pedestals
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.8f;
                MakePrimitive($"Pedestal_{i+1}", PrimitiveType.Cylinder, interior,
                    new Vector3(x, -0.7f, -1.2f), new Vector3(0.3f, 0.15f, 0.3f),
                    new Color(0.85f, 0.85f, 0.9f));
                var product = MakePrimitive($"Product_{i+1}", PrimitiveType.Cube, interior,
                    new Vector3(x, -0.4f, -1.2f), new Vector3(0.15f, 0.15f, 0.15f),
                    new Color(0.3f, 0.7f, 0.9f));
                AddTweenFX(product, autoFloat: true);
            }
        }

        // =============================================================
        // INTERACTIVE TEMPLATES (9-13)
        // =============================================================

        // 9: AR Concert Stage
        private void Build_ConcertStage(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, world: true);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, worldTracker: world.GetComponent<XR8WorldTracker>());

            var stage = new GameObject("ConcertStage");
            stage.transform.position = new Vector3(0, 0, 2f);

            // Stage platform
            MakePrimitive("StagePlatform", PrimitiveType.Cube, stage.transform,
                Vector3.zero, new Vector3(2f, 0.1f, 1.5f), new Color(0.2f, 0.2f, 0.25f));

            // Performer placeholder
            var performer = MakePrimitive("Performer", PrimitiveType.Capsule, stage.transform,
                new Vector3(0, 0.55f, 0), new Vector3(0.2f, 0.45f, 0.2f),
                new Color(0.9f, 0.2f, 0.3f));

            // Speaker stacks
            MakePrimitive("Speaker_L", PrimitiveType.Cube, stage.transform,
                new Vector3(-0.8f, 0.3f, -0.5f), new Vector3(0.3f, 0.5f, 0.25f),
                new Color(0.15f, 0.15f, 0.15f));
            MakePrimitive("Speaker_R", PrimitiveType.Cube, stage.transform,
                new Vector3(0.8f, 0.3f, -0.5f), new Vector3(0.3f, 0.5f, 0.25f),
                new Color(0.15f, 0.15f, 0.15f));

            // Stage lights
            var spotL = new GameObject("StageLight_L");
            spotL.transform.SetParent(stage.transform);
            spotL.transform.localPosition = new Vector3(-0.7f, 1.5f, 0.3f);
            spotL.transform.localRotation = Quaternion.Euler(45, 30, 0);
            var sl = spotL.AddComponent<Light>();
            sl.type = LightType.Spot; sl.range = 5f; sl.spotAngle = 40f;
            sl.color = new Color(1f, 0.3f, 0.5f); sl.intensity = 2f;
        }

        // 10: AR Storytelling
        private void Build_Storytelling(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, image: true);
            var tracker = CreateImageTrackerObj(xr8Cam, "story-trigger", out Transform content);
            WireManager(mgr, imgTracker: tracker.GetComponent<XR8ImageTracker>());

            // Narrator character
            var narrator = MakePrimitive("NarratorCharacter", PrimitiveType.Capsule, content,
                new Vector3(0.2f, 0.12f, 0), new Vector3(0.06f, 0.12f, 0.06f),
                new Color(0.4f, 0.3f, 0.6f));

            // Speech bubble
            var bubble = MakePrimitive("SpeechBubble", PrimitiveType.Sphere, content,
                new Vector3(0.2f, 0.3f, 0), new Vector3(0.12f, 0.08f, 0.01f),
                new Color(1f, 1f, 1f, 0.9f));

            // Scene backdrop
            var backdrop = MakePrimitive("StoryBackdrop", PrimitiveType.Quad, content,
                new Vector3(-0.1f, 0.1f, -0.05f), new Vector3(0.3f, 0.2f, 1f),
                new Color(0.2f, 0.15f, 0.3f), Quaternion.Euler(90f, 0, 0));

            AddTweenFX(content.gameObject);
        }

        // 11: AR Scavenger Hunt
        private void Build_ScavengerHunt(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, image: true, world: true);
            var tracker = CreateImageTrackerObj(xr8Cam, "hunt-clue-1", out Transform content);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, imgTracker: tracker.GetComponent<XR8ImageTracker>(),
                worldTracker: world.GetComponent<XR8WorldTracker>());

            // Collectible item
            var collectible = MakePrimitive("Collectible_Star", PrimitiveType.Sphere, content,
                new Vector3(0, 0.15f, 0), new Vector3(0.1f, 0.1f, 0.1f),
                new Color(1f, 0.85f, 0.2f));
            AddTweenFX(collectible, autoFloat: true);

            // Clue indicator
            var clue = MakePrimitive("ClueIndicator", PrimitiveType.Cube, content,
                new Vector3(0, 0.05f, 0), new Vector3(0.15f, 0.005f, 0.1f),
                new Color(0.9f, 0.9f, 0.85f));
        }

        // 12: AR Magic Mirror
        private void Build_MagicMirror(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, face: true);
            var faceObj = new GameObject("XR8FaceTracker");
            var faceTracker = faceObj.AddComponent<XR8FaceTracker>();
            WireManager(mgr, faceTracker: faceTracker);

            // Crown attachment
            var crown = MakePrimitive("CrownAttachment", PrimitiveType.Cube, faceObj.transform,
                new Vector3(0, 0.14f, 0), new Vector3(0.12f, 0.04f, 0.12f),
                new Color(0.9f, 0.75f, 0.2f));

            // Mask attachment
            var maskObj = MakePrimitive("MaskAttachment", PrimitiveType.Sphere, faceObj.transform,
                new Vector3(0, 0.02f, 0.08f), new Vector3(0.14f, 0.1f, 0.04f),
                new Color(0.6f, 0.2f, 0.8f));

            // Glasses attachment
            var glasses = MakePrimitive("GlassesAttachment", PrimitiveType.Cube, faceObj.transform,
                new Vector3(0, 0.03f, 0.09f), new Vector3(0.12f, 0.025f, 0.01f),
                new Color(0.15f, 0.15f, 0.15f));
        }

        // 13: AR Creature Encounter
        private void Build_CreatureEncounter(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, world: true);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, worldTracker: world.GetComponent<XR8WorldTracker>());

            var encounter = new GameObject("CreatureEncounter");
            encounter.transform.position = new Vector3(0, 0, 2f);

            // Creature placeholder
            var creature = MakePrimitive("Creature", PrimitiveType.Capsule, encounter.transform,
                new Vector3(0, 0.4f, 0), new Vector3(0.3f, 0.4f, 0.3f),
                new Color(0.2f, 0.7f, 0.3f));
            AddTweenFX(creature, autoFloat: true);

            // Shadow disc
            MakePrimitive("ShadowDisc", PrimitiveType.Cylinder, encounter.transform,
                Vector3.zero, new Vector3(0.5f, 0.005f, 0.5f),
                new Color(0, 0, 0, 0.3f));

            // Footprint trail
            for (int i = 0; i < 3; i++)
            {
                MakePrimitive($"Footprint_{i+1}", PrimitiveType.Cube, encounter.transform,
                    new Vector3(0, 0.001f, 0.5f + i * 0.4f), new Vector3(0.06f, 0.002f, 0.08f),
                    new Color(0.3f, 0.3f, 0.3f, 0.4f));
            }
        }

        // =============================================================
        // PRODUCT VIZ TEMPLATES (14-16)
        // =============================================================

        // 14: AR Product Configurator
        private void Build_ProductConfigurator(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, world: true);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, worldTracker: world.GetComponent<XR8WorldTracker>());

            var configurator = new GameObject("ProductConfigurator");
            configurator.transform.position = new Vector3(0, 0, 2f);

            // Product on pedestal
            MakePrimitive("Pedestal", PrimitiveType.Cylinder, configurator.transform,
                Vector3.zero, new Vector3(0.4f, 0.05f, 0.4f),
                new Color(0.9f, 0.9f, 0.92f));

            var product = MakePrimitive("ConfigurableProduct", PrimitiveType.Cube, configurator.transform,
                new Vector3(0, 0.2f, 0), new Vector3(0.25f, 0.25f, 0.25f),
                new Color(0.3f, 0.6f, 0.9f));

            // Color option indicators
            Color[] opts = { Color.red, Color.blue, new Color(0.2f, 0.2f, 0.2f), Color.white };
            for (int i = 0; i < opts.Length; i++)
            {
                MakePrimitive($"ColorOption_{i+1}", PrimitiveType.Sphere, configurator.transform,
                    new Vector3(-0.3f + i * 0.2f, -0.1f, 0.3f), new Vector3(0.05f, 0.05f, 0.05f), opts[i]);
            }
            AddTweenFX(product);
        }

        // 15: AR Product Placement
        private void Build_ProductPlacement(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, world: true);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, worldTracker: world.GetComponent<XR8WorldTracker>());

            // Placement preview
            var preview = MakePrimitive("PlacementPreview", PrimitiveType.Cube, null,
                Vector3.zero, new Vector3(0.4f, 0.4f, 0.4f),
                new Color(0.5f, 0.8f, 0.5f, 0.5f));

            // Reticle
            var reticle = MakePrimitive("PlacementReticle", PrimitiveType.Cylinder, null,
                Vector3.zero, new Vector3(0.3f, 0.002f, 0.3f),
                new Color(0.3f, 0.8f, 1f, 0.5f));

            AddTweenFX(preview);
        }

        // 16: AR Live Launch
        private void Build_LiveLaunch(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, image: true, world: true);
            var tracker = CreateImageTrackerObj(xr8Cam, "launch-qr", out Transform content);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, imgTracker: tracker.GetComponent<XR8ImageTracker>(),
                worldTracker: world.GetComponent<XR8WorldTracker>());

            // Product reveal stage
            var stage = MakePrimitive("RevealStage", PrimitiveType.Cylinder, content,
                Vector3.zero, new Vector3(0.3f, 0.01f, 0.3f),
                new Color(0.1f, 0.1f, 0.15f));

            var product = MakePrimitive("LaunchProduct", PrimitiveType.Cube, content,
                new Vector3(0, 0.15f, 0), new Vector3(0.08f, 0.15f, 0.04f),
                new Color(0.2f, 0.2f, 0.25f));
            AddTweenFX(product, autoFloat: true);
        }

        // =============================================================
        // EVENT TEMPLATES (17-18)
        // =============================================================

        // 17: AR Holiday Theme
        private void Build_HolidayTheme(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, image: true, world: true);
            var tracker = CreateImageTrackerObj(xr8Cam, "holiday-trigger", out Transform content);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, imgTracker: tracker.GetComponent<XR8ImageTracker>(),
                worldTracker: world.GetComponent<XR8WorldTracker>());

            // Themed character
            var character = MakePrimitive("HolidayCharacter", PrimitiveType.Capsule, content,
                new Vector3(0, 0.15f, 0), new Vector3(0.1f, 0.15f, 0.1f),
                new Color(0.9f, 0.2f, 0.2f));

            // Decorations
            var ornament = MakePrimitive("Ornament", PrimitiveType.Sphere, content,
                new Vector3(0.15f, 0.25f, 0), new Vector3(0.05f, 0.05f, 0.05f),
                new Color(1f, 0.85f, 0.1f));
            AddTweenFX(ornament, autoFloat: true);

            // Gift box
            MakePrimitive("GiftBox", PrimitiveType.Cube, content,
                new Vector3(-0.12f, 0.04f, 0.1f), new Vector3(0.08f, 0.08f, 0.08f),
                new Color(0.2f, 0.6f, 0.3f));
        }

        // 18: AR Photo Op
        private void Build_PhotoOp(Camera cam, XR8Camera xr8Cam)
        {
            var mgr = CreateManagerObj(cam, xr8Cam, world: true);
            var world = CreateWorldTrackerObj(cam);
            WireManager(mgr, worldTracker: world.GetComponent<XR8WorldTracker>());

            var photoOp = new GameObject("PhotoOpSetup");
            photoOp.transform.position = new Vector3(0, 0, 2f);

            // Character placeholder
            var character = MakePrimitive("PhotoCharacter", PrimitiveType.Capsule, photoOp.transform,
                new Vector3(0, 0.5f, 0), new Vector3(0.25f, 0.5f, 0.25f),
                new Color(0.3f, 0.5f, 0.9f));

            // Photo frame overlay
            MakePrimitive("PhotoFrame", PrimitiveType.Cube, photoOp.transform,
                new Vector3(0, 0.5f, -0.5f), new Vector3(1.2f, 0.9f, 0.02f),
                new Color(0.9f, 0.8f, 0.3f));

            // Props
            MakePrimitive("PropHat", PrimitiveType.Cylinder, photoOp.transform,
                new Vector3(-0.4f, 0.3f, 0.3f), new Vector3(0.12f, 0.04f, 0.12f),
                new Color(0.15f, 0.15f, 0.15f));
            MakePrimitive("PropStar", PrimitiveType.Sphere, photoOp.transform,
                new Vector3(0.5f, 0.7f, 0.2f), new Vector3(0.08f, 0.08f, 0.08f),
                new Color(1f, 0.9f, 0.2f));

            AddTweenFX(character);
        }

        // =============================================================
        // UTILITIES
        // =============================================================

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
