using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8 Shared Session — Multi-user WebAR via WebSocket relay.
    ///
    /// Enables shared AR experiences where multiple users see the same
    /// placed content in their own space. Uses a simple WebSocket relay
    /// server — no native Cloud Anchors needed.
    ///
    /// Workflow:
    ///   1. User A creates or joins a room
    ///   2. User A places content → position synced to all peers
    ///   3. User B sees the content appear at relative position
    ///
    /// Features:
    ///   - Room-based sessions (auto-generate or set room code)
    ///   - Sync placed objects: position, rotation, scale
    ///   - Peer join/leave notifications
    ///   - Configurable sync rate
    ///   - Host/client roles
    ///
    /// Usage:
    ///   1. Add component to scene
    ///   2. Set relay server URL (WebSocket endpoint)
    ///   3. Call CreateRoom() or JoinRoom(code) from UI
    ///   4. Call SyncObject() when user places/moves content
    ///
    /// Works with: XR8SharedLib.jslib
    /// </summary>
    public class XR8SharedSession : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void WebGLSharedConnect(string goName, string serverUrl);
        [DllImport("__Internal")] private static extern void WebGLSharedDisconnect();
        [DllImport("__Internal")] private static extern void WebGLSharedCreateRoom();
        [DllImport("__Internal")] private static extern void WebGLSharedJoinRoom(string roomCode);
        [DllImport("__Internal")] private static extern void WebGLSharedSendTransform(string objectId, string csv);
        [DllImport("__Internal")] private static extern void WebGLSharedSendMessage(string msgType, string payload);
#endif

        [Serializable]
        public class SyncedObject
        {
            public string objectId;
            public Transform target;
            [HideInInspector] public Vector3 lastSyncedPos;
            [HideInInspector] public Quaternion lastSyncedRot;
        }

        [Header("Server")]
        [Tooltip("WebSocket relay server URL")]
        public string relayServerUrl = "wss://your-relay-server.com/ws";

        [Header("Session")]
        [Tooltip("Auto-connect on Start")]
        public bool autoConnect = false;

        [Tooltip("Sync rate in Hz (updates per second)")]
        [Range(1, 30)]
        public int syncRate = 10;

        [Header("Synced Objects")]
        [Tooltip("Objects to sync transforms for")]
        public List<SyncedObject> syncedObjects = new List<SyncedObject>();

        [Header("Status")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private bool isInRoom = false;
        [SerializeField] private string currentRoomCode = "";
        [SerializeField] private int peerCount = 0;
        [SerializeField] private bool isHost = false;

        // Movement threshold — only sync if moved enough
        private const float POS_THRESHOLD = 0.005f;
        private const float ROT_THRESHOLD = 0.5f;
        private float _syncTimer;

        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnRoomCreated;   // room code
        public event Action<string> OnRoomJoined;    // room code
        public event Action<int> OnPeerJoined;       // new peer count
        public event Action<int> OnPeerLeft;         // new peer count
        public event Action<string, Vector3, Quaternion, Vector3> OnObjectSynced; // id, pos, rot, scale
        public event Action<string, string> OnCustomMessage; // type, payload

        /// <summary>Is connected to relay server?</summary>
        public bool IsConnected => isConnected;
        /// <summary>Is currently in a room?</summary>
        public bool IsInRoom => isInRoom;
        /// <summary>Current room code.</summary>
        public string RoomCode => currentRoomCode;
        /// <summary>Number of peers in room.</summary>
        public int PeerCount => peerCount;
        /// <summary>Is this client the room host?</summary>
        public bool IsHost => isHost;

        private void Start()
        {
            if (autoConnect)
                Connect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void Update()
        {
            if (!isConnected || !isInRoom || syncedObjects.Count == 0) return;

            _syncTimer += Time.deltaTime;
            if (_syncTimer < 1f / syncRate) return;
            _syncTimer = 0f;

            // Sync objects that have moved
            foreach (var obj in syncedObjects)
            {
                if (obj.target == null) continue;

                float posDelta = Vector3.Distance(obj.target.position, obj.lastSyncedPos);
                float rotDelta = Quaternion.Angle(obj.target.rotation, obj.lastSyncedRot);

                if (posDelta > POS_THRESHOLD || rotDelta > ROT_THRESHOLD)
                {
                    SyncObject(obj);
                }
            }
        }

        // =============================================
        // PUBLIC API
        // =============================================

        /// <summary>Connect to relay server.</summary>
        public void Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSharedConnect(gameObject.name, relayServerUrl);
#else
            Debug.Log("[XR8SharedSession] Connect (editor stub)");
            isConnected = true;
            OnConnected?.Invoke();
#endif
        }

        /// <summary>Disconnect from relay server.</summary>
        public void Disconnect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSharedDisconnect();
#endif
            isConnected = false;
            isInRoom = false;
            OnDisconnected?.Invoke();
        }

        /// <summary>Create a new room (become host).</summary>
        public void CreateRoom()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSharedCreateRoom();
#else
            isHost = true;
            currentRoomCode = UnityEngine.Random.Range(1000, 9999).ToString();
            isInRoom = true;
            OnRoomCreated?.Invoke(currentRoomCode);
            Debug.Log($"[XR8SharedSession] Room created: {currentRoomCode} (editor stub)");
#endif
        }

        /// <summary>Join an existing room by code.</summary>
        public void JoinRoom(string roomCode)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSharedJoinRoom(roomCode);
#else
            isHost = false;
            currentRoomCode = roomCode;
            isInRoom = true;
            OnRoomJoined?.Invoke(roomCode);
            Debug.Log($"[XR8SharedSession] Joined room: {roomCode} (editor stub)");
#endif
        }

        /// <summary>Manually sync a specific registered object.</summary>
        public void SyncObject(SyncedObject obj)
        {
            if (obj.target == null) return;

            var pos = obj.target.position;
            var rot = obj.target.rotation;
            var scale = obj.target.localScale;

            string csv = $"{pos.x:F4},{pos.y:F4},{pos.z:F4},{rot.x:F4},{rot.y:F4},{rot.z:F4},{rot.w:F4},{scale.x:F3},{scale.y:F3},{scale.z:F3}";

            obj.lastSyncedPos = pos;
            obj.lastSyncedRot = rot;

#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSharedSendTransform(obj.objectId, csv);
#endif
        }

        /// <summary>Send a custom message to all peers.</summary>
        public void SendCustomMessage(string messageType, string payload)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSharedSendMessage(messageType, payload);
#endif
        }

        // =============================================
        // SendMessage callbacks from JS bridge
        // =============================================

        public void OnSharedConnected(string msg)
        {
            isConnected = true;
            OnConnected?.Invoke();
            Debug.Log("[XR8SharedSession] Connected to relay server");
        }

        public void OnSharedDisconnected(string msg)
        {
            isConnected = false;
            isInRoom = false;
            OnDisconnected?.Invoke();
        }

        public void OnSharedRoomCreated(string roomCode)
        {
            currentRoomCode = roomCode;
            isInRoom = true;
            isHost = true;
            OnRoomCreated?.Invoke(roomCode);
            Debug.Log($"[XR8SharedSession] Room created: {roomCode}");
        }

        public void OnSharedRoomJoined(string roomCode)
        {
            currentRoomCode = roomCode;
            isInRoom = true;
            isHost = false;
            OnRoomJoined?.Invoke(roomCode);
            Debug.Log($"[XR8SharedSession] Joined room: {roomCode}");
        }

        public void OnSharedPeerJoined(string countStr)
        {
            if (int.TryParse(countStr, out int count))
            {
                peerCount = count;
                OnPeerJoined?.Invoke(count);
            }
        }

        public void OnSharedPeerLeft(string countStr)
        {
            if (int.TryParse(countStr, out int count))
            {
                peerCount = count;
                OnPeerLeft?.Invoke(count);
            }
        }

        /// <summary>
        /// Receives transform sync: "objectId,posX,posY,posZ,rotX,rotY,rotZ,rotW,scaleX,scaleY,scaleZ"
        /// </summary>
        public void OnSharedTransform(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 11) return;

            string objectId = parts[0];

            if (!float.TryParse(parts[1], out float px)) return;
            if (!float.TryParse(parts[2], out float py)) return;
            if (!float.TryParse(parts[3], out float pz)) return;
            if (!float.TryParse(parts[4], out float rx)) return;
            if (!float.TryParse(parts[5], out float ry)) return;
            if (!float.TryParse(parts[6], out float rz)) return;
            if (!float.TryParse(parts[7], out float rw)) return;
            if (!float.TryParse(parts[8], out float sx)) return;
            if (!float.TryParse(parts[9], out float sy)) return;
            if (!float.TryParse(parts[10], out float sz)) return;

            var pos = new Vector3(px, py, pz);
            var rot = new Quaternion(rx, ry, rz, rw);
            var scale = new Vector3(sx, sy, sz);

            // Apply to matching synced object
            foreach (var obj in syncedObjects)
            {
                if (obj.objectId == objectId && obj.target != null)
                {
                    obj.target.position = pos;
                    obj.target.rotation = rot;
                    obj.target.localScale = scale;
                    obj.lastSyncedPos = pos;
                    obj.lastSyncedRot = rot;
                    break;
                }
            }

            OnObjectSynced?.Invoke(objectId, pos, rot, scale);
        }

        public void OnSharedCustomMessage(string data)
        {
            int sep = data.IndexOf('|');
            if (sep < 0) return;
            string msgType = data.Substring(0, sep);
            string payload = data.Substring(sep + 1);
            OnCustomMessage?.Invoke(msgType, payload);
        }
    }
}
