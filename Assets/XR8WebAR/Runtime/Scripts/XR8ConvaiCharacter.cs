using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8ConvaiCharacter — Drop this on any character GameObject to give it
    /// an AI brain powered by Convai. Works in Unity WebGL builds via the
    /// Convai Web SDK (loaded as JavaScript, bridged through ConvaiBridge.jslib).
    ///
    /// Setup:
    ///   1. Get a Character ID + API key from convai.com
    ///   2. Paste them into this component's inspector
    ///   3. Assign your character's SkinnedMeshRenderer (for lip-sync)
    ///   4. Assign idle/talking Animation clips or Animator
    ///   5. Build WebGL → test on phone → talk to your character!
    ///
    /// In the Unity Editor (desktop preview), the character will show up and
    /// play its idle animation, but AI conversation requires a WebGL build.
    /// </summary>
    public class XR8ConvaiCharacter : MonoBehaviour
    {
        // ====================================================================
        // Inspector Fields
        // ====================================================================

        [Header("Convai Settings")]
        [Tooltip("Your Convai API key from convai.com")]
        [SerializeField] private string apiKey = "edac99820870aef8ab5f54cfdd98f18e";

        [Tooltip("The Character ID from convai.com")]
        [SerializeField] private string characterId = "fe1454e8-803a-11ee-a21d-42010a40000e";

        [Tooltip("Connect automatically when the scene starts")]
        [SerializeField] private bool autoConnect = true;

        [Header("Lip Sync")]
        [Tooltip("Enable real-time lip-sync blendshapes (60fps from Convai)")]
        [SerializeField] private bool enableLipsync = true;

        [Tooltip("ARKit = 52 blendshapes (Apple standard). MHA = 251 (MetaHuman).")]
        [SerializeField] private BlendshapeFormat blendshapeFormat = BlendshapeFormat.ARKit;

        [Tooltip("The SkinnedMeshRenderer with blendshapes (usually the face mesh)")]
        [SerializeField] private SkinnedMeshRenderer faceMesh;

        [Tooltip("Optional: Map Convai blendshape indices to your model's blendshape indices")]
        [SerializeField] private BlendshapeMapping[] customBlendshapeMappings;

        [Header("Animation")]
        [Tooltip("Animator on the character (for idle/talk state switching)")]
        [SerializeField] private Animator characterAnimator;

        [Tooltip("Animator parameter name for talking state (Bool)")]
        [SerializeField] private string talkingAnimParam = "IsTalking";

        [Tooltip("Animator trigger name for emotion reactions")]
        [SerializeField] private string emotionAnimParam = "Emotion";

        [Header("UI (Optional)")]
        [Tooltip("Show Convai's built-in chat widget in the browser")]
        [SerializeField] private bool showChatWidget = false;

        [Tooltip("Show a 'Tap to Talk' button in the AR view")]
        [SerializeField] private bool showTalkButton = true;

        [Header("Events")]
        public UnityEvent OnConnected;
        public UnityEvent OnDisconnected;
        public UnityEvent OnBotReady;
        public UnityEvent OnStartedTalking;
        public UnityEvent OnStoppedTalking;
        public UnityEvent<string> OnBotTextReceived;
        public UnityEvent<string> OnUserTextReceived;
        public UnityEvent<string> OnEmotionReceived;
        public UnityEvent<string> OnActionReceived;
        public UnityEvent<string> OnError;

        // ====================================================================
        // Enums
        // ====================================================================

        public enum BlendshapeFormat
        {
            ARKit,  // 52 blendshapes (Apple ARKit standard)
            MHA     // 251 blendshapes (MetaHuman standard)
        }

        [Serializable]
        public struct BlendshapeMapping
        {
            [Tooltip("Index in the Convai blendshape array")]
            public int convaiIndex;
            [Tooltip("Index on your SkinnedMeshRenderer")]
            public int meshIndex;
            [Tooltip("Optional: multiplier for this blendshape (default 1.0)")]
            public float weight;
        }

        // ====================================================================
        // State
        // ====================================================================

        private bool isConnected;
        private bool isTalking;
        private string currentEmotion = "neutral";
        private string lastBotText = "";
        private float[] currentBlendshapes;

        // ====================================================================
        // JS Bridge (WebGL only)
        // ====================================================================

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void Convai_Connect(string configJson);
        [DllImport("__Internal")] private static extern void Convai_Disconnect();
        [DllImport("__Internal")] private static extern void Convai_SendText(string text);
        [DllImport("__Internal")] private static extern void Convai_ToggleMic();
        [DllImport("__Internal")] private static extern void Convai_StartMic();
        [DllImport("__Internal")] private static extern void Convai_StopMic();
        [DllImport("__Internal")] private static extern void Convai_SendTrigger(string triggerName, string payload);
        [DllImport("__Internal")] private static extern bool Convai_IsConnected();
        [DllImport("__Internal")] private static extern bool Convai_IsTalking();
#else
        // Editor stubs — Convai doesn't run in editor
        private static void Convai_Connect(string configJson) { Debug.Log("[Convai] Would connect: " + configJson); }
        private static void Convai_Disconnect() { Debug.Log("[Convai] Would disconnect"); }
        private static void Convai_SendText(string text) { Debug.Log("[Convai] Would send: " + text); }
        private static void Convai_ToggleMic() { Debug.Log("[Convai] Would toggle mic"); }
        private static void Convai_StartMic() { Debug.Log("[Convai] Would start mic"); }
        private static void Convai_StopMic() { Debug.Log("[Convai] Would stop mic"); }
        private static void Convai_SendTrigger(string t, string p) { Debug.Log("[Convai] Would trigger: " + t); }
        private static bool Convai_IsConnected() { return false; }
        private static bool Convai_IsTalking() { return false; }
#endif

        // ====================================================================
        // Unity Lifecycle
        // ====================================================================

        private void Start()
        {
            // Auto-find animator if not assigned
            if (characterAnimator == null)
                characterAnimator = GetComponent<Animator>();

            // Auto-find face mesh if not assigned
            if (faceMesh == null)
                faceMesh = GetComponentInChildren<SkinnedMeshRenderer>();

            if (autoConnect)
                Connect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        // ====================================================================
        // Public API (call these from other scripts or UI buttons)
        // ====================================================================

        /// <summary>Connect to Convai and start the AI character.</summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(characterId))
            {
                Debug.LogWarning("[XR8ConvaiCharacter] API Key and Character ID are required. Get them from convai.com");
                return;
            }

            var config = new ConvaiConfig
            {
                apiKey = apiKey,
                characterId = characterId,
                unityObjectName = gameObject.name,
                enableLipsync = enableLipsync,
                blendshapeFormat = blendshapeFormat == BlendshapeFormat.ARKit ? "arkit" : "mha"
            };

            string json = JsonUtility.ToJson(config);
            Debug.Log("[XR8ConvaiCharacter] Connecting to Convai...");
            Convai_Connect(json);
        }

        /// <summary>Disconnect from Convai.</summary>
        public void Disconnect()
        {
            Convai_Disconnect();
        }

        /// <summary>Send a text message to the AI character.</summary>
        public void SendTextMessage(string text)
        {
            Convai_SendText(text);
        }

        /// <summary>Toggle microphone on/off for voice chat.</summary>
        public void ToggleMicrophone()
        {
            Convai_ToggleMic();
        }

        /// <summary>Start microphone for push-to-talk.</summary>
        public void StartMicrophone()
        {
            Convai_StartMic();
        }

        /// <summary>Stop microphone.</summary>
        public void StopMicrophone()
        {
            Convai_StopMic();
        }

        /// <summary>Send a narrative trigger to the character.</summary>
        public void SendTrigger(string triggerName, string payload = "")
        {
            Convai_SendTrigger(triggerName, payload);
        }

        /// <summary>Is the character currently connected?</summary>
        public bool IsConnected => isConnected;

        /// <summary>Is the character currently talking?</summary>
        public bool IsTalking => isTalking;

        /// <summary>The last emotion received from Convai.</summary>
        public string CurrentEmotion => currentEmotion;

        /// <summary>The last bot response text.</summary>
        public string LastBotText => lastBotText;

        // ====================================================================
        // SendMessage callbacks from ConvaiBridge.js (via .jslib)
        // ====================================================================

        // Called when connection is established
        private void OnConvaiConnected(string _)
        {
            isConnected = true;
            Debug.Log("[XR8ConvaiCharacter] Connected to Convai!");
            OnConnected?.Invoke();
        }

        // Called when disconnected
        private void OnConvaiDisconnected(string _)
        {
            isConnected = false;
            isTalking = false;
            Debug.Log("[XR8ConvaiCharacter] Disconnected from Convai");
            OnDisconnected?.Invoke();
        }

        // Called when bot is ready to receive messages
        private void OnConvaiBotReady(string _)
        {
            Debug.Log("[XR8ConvaiCharacter] Bot ready!");
            OnBotReady?.Invoke();
        }

        // Called when character starts speaking
        private void OnConvaiStartedTalking(string _)
        {
            isTalking = true;

            // Switch animation to talking
            if (characterAnimator != null && !string.IsNullOrEmpty(talkingAnimParam))
                characterAnimator.SetBool(talkingAnimParam, true);

            OnStartedTalking?.Invoke();
        }

        // Called when character stops speaking
        private void OnConvaiStoppedTalking(string _)
        {
            isTalking = false;

            // Switch animation to idle
            if (characterAnimator != null && !string.IsNullOrEmpty(talkingAnimParam))
                characterAnimator.SetBool(talkingAnimParam, false);

            // Reset blendshapes to neutral
            ResetBlendshapes();

            OnStoppedTalking?.Invoke();
        }

        // Called with agent state: "idle", "listening", "thinking", "speaking"
        private void OnConvaiStateChanged(string state)
        {
            // Could drive more granular UI/animation states
        }

        // Called with bot response text
        private void OnConvaiBotText(string text)
        {
            lastBotText = text;
            OnBotTextReceived?.Invoke(text);
        }

        // Called with user's transcribed speech
        private void OnConvaiUserText(string text)
        {
            OnUserTextReceived?.Invoke(text);
        }

        // Called with real-time user transcription (partial)
        private void OnConvaiUserTranscription(string text)
        {
            // Could show live subtitles
        }

        // Called with emotion signal
        private void OnConvaiEmotion(string emotion)
        {
            currentEmotion = emotion;

            // Trigger emotion animation if animator supports it
            if (characterAnimator != null && !string.IsNullOrEmpty(emotionAnimParam))
            {
                characterAnimator.SetTrigger(emotionAnimParam);
            }

            OnEmotionReceived?.Invoke(emotion);
        }

        // Called with character action
        private void OnConvaiAction(string action)
        {
            OnActionReceived?.Invoke(action);
        }

        // Called with mic state changes
        private void OnConvaiMicStateChanged(string state)
        {
            // "1" = mic on, "0" = mic off
        }

        // Called with error messages
        private void OnConvaiError(string error)
        {
            Debug.LogWarning("[XR8ConvaiCharacter] Error: " + error);
            OnError?.Invoke(error);
        }

        // ====================================================================
        // Lip-sync blendshape handling
        // ====================================================================

        /// <summary>
        /// Receives blendshape data from Convai at 60fps.
        /// CSV format: "0.1234,0.5678,0.0000,..." (52 or 251 values)
        /// </summary>
        private void OnConvaiBlendshapes(string csv)
        {
            if (faceMesh == null) return;

            string[] parts = csv.Split(',');
            if (currentBlendshapes == null || currentBlendshapes.Length != parts.Length)
                currentBlendshapes = new float[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float val))
                {
                    currentBlendshapes[i] = val;
                }
            }

            ApplyBlendshapes();
        }

        private void ApplyBlendshapes()
        {
            if (faceMesh == null || currentBlendshapes == null) return;

            if (customBlendshapeMappings != null && customBlendshapeMappings.Length > 0)
            {
                // Use custom mapping
                for (int i = 0; i < customBlendshapeMappings.Length; i++)
                {
                    var mapping = customBlendshapeMappings[i];
                    if (mapping.convaiIndex < currentBlendshapes.Length &&
                        mapping.meshIndex < faceMesh.sharedMesh.blendShapeCount)
                    {
                        float weight = mapping.weight > 0 ? mapping.weight : 1f;
                        faceMesh.SetBlendShapeWeight(mapping.meshIndex,
                            currentBlendshapes[mapping.convaiIndex] * 100f * weight);
                    }
                }
            }
            else
            {
                // Direct 1:1 mapping (works if model has ARKit-ordered blendshapes)
                int count = Mathf.Min(currentBlendshapes.Length, faceMesh.sharedMesh.blendShapeCount);
                for (int i = 0; i < count; i++)
                {
                    faceMesh.SetBlendShapeWeight(i, currentBlendshapes[i] * 100f);
                }
            }
        }

        private void ResetBlendshapes()
        {
            if (faceMesh == null) return;

            for (int i = 0; i < faceMesh.sharedMesh.blendShapeCount; i++)
            {
                faceMesh.SetBlendShapeWeight(i, 0f);
            }
        }

        // ====================================================================
        // Config struct for JSON serialization
        // ====================================================================

        [Serializable]
        private struct ConvaiConfig
        {
            public string apiKey;
            public string characterId;
            public string unityObjectName;
            public bool enableLipsync;
            public string blendshapeFormat;
        }

        // ====================================================================
        // Desktop Preview (Editor only)
        // ====================================================================

#if UNITY_EDITOR
        [Header("Desktop Preview")]
        [Tooltip("Simulate talking animation in editor (for positioning/testing)")]
        [SerializeField] private bool simulateTalking = false;

        private void Update()
        {
            // In editor, let the user toggle talking simulation
            if (simulateTalking != isTalking)
            {
                isTalking = simulateTalking;
                if (characterAnimator != null && !string.IsNullOrEmpty(talkingAnimParam))
                    characterAnimator.SetBool(talkingAnimParam, isTalking);
            }
        }
#endif
    }

    // ========================================================================
    // Custom Editor — pretty inspector with help boxes
    // ========================================================================

#if UNITY_EDITOR
    [CustomEditor(typeof(XR8ConvaiCharacter))]
    public class XR8ConvaiCharacterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var character = (XR8ConvaiCharacter)target;

            EditorGUILayout.HelpBox(
                "🤖 Convai AI Character\n\n" +
                "This character will talk, respond, and emote using Convai's AI.\n" +
                "Get your API Key and Character ID from convai.com",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ AI conversation only works in WebGL builds.\n" +
                    "In the editor, use 'Simulate Talking' to test animations.\n\n" +
                    "Build: XR8 WebAR > Build WebGL\n" +
                    "Test: npx serve Build/ → open on phone",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("🌐 Open Convai Dashboard"))
            {
                Application.OpenURL("https://convai.com/pipeline/dashboard");
            }
        }
    }
#endif
}
