/**
 * convai-bridge.js — Bridge between Convai Web SDK and Unity WebGL
 *
 * Handles:
 *   - ConvaiClient lifecycle (connect, disconnect, text/voice input)
 *   - Bot response forwarding to Unity via SendMessage()
 *   - Real-time lip-sync blendshape streaming to Unity
 *   - Emotion and action event forwarding
 *   - Audio playback via AudioRenderer
 *
 * Follows the same pattern as xr8-bridge.js:
 *   JS bridge class ←→ .jslib (DllImport) ←→ C# component
 *
 * License: MIT
 * Author: hoodtronik (https://github.com/hoodtronik)
 */

// ============================================================================
// ConvaiBridge — Convai Web SDK ↔ Unity bridge
// ============================================================================
class ConvaiBridge {
    constructor() {
        this.client = null;
        this.audioRenderer = null;
        this.lipsyncPlayer = null;
        this.unityObjectName = null;  // Unity GameObject to receive SendMessage
        this.isConnected = false;
        this.isTalking = false;
        this.lipsyncStartTime = 0;
        this.lipsyncAnimFrameId = null;

        console.log('[ConvaiBridge] Bridge created');
    }

    /**
     * Initialize and connect Convai client.
     * Called from ConvaiBridge.jslib → XR8ConvaiCharacter.cs
     *
     * @param {string} configJson - JSON string with:
     *   { apiKey, characterId, unityObjectName, enableLipsync, blendshapeFormat }
     */
    async connect(configJson) {
        var config;
        try {
            config = JSON.parse(configJson);
        } catch (e) {
            console.error('[ConvaiBridge] Invalid config JSON:', e);
            return;
        }

        this.unityObjectName = config.unityObjectName || 'XR8ConvaiCharacter';

        console.log('[ConvaiBridge] Connecting...', {
            characterId: config.characterId,
            unityObject: this.unityObjectName,
            enableLipsync: config.enableLipsync
        });

        try {
            // Get the ConvaiClient class from the SDK
            // The SDK is loaded via CDN and exposes ConvaiSDK global
            var ConvaiClient;
            if (window.ConvaiSDK && window.ConvaiSDK.ConvaiClient) {
                ConvaiClient = window.ConvaiSDK.ConvaiClient;
            } else if (window.ConvaiClient) {
                ConvaiClient = window.ConvaiClient;
            } else {
                console.error('[ConvaiBridge] Convai SDK not loaded! Make sure the script tag is in index.html');
                this._sendToUnity('OnConvaiError', 'SDK not loaded');
                return;
            }

            // Create client
            this.client = new ConvaiClient();

            // Set up event listeners BEFORE connecting
            this._setupEventListeners();

            // Connect with config
            var connectConfig = {
                apiKey: config.apiKey,
                characterId: config.characterId,
                enableLipsync: config.enableLipsync || false,
                ttsEnabled: true,
                startWithAudioOn: false  // User taps to talk
            };

            if (config.enableLipsync) {
                connectConfig.blendshapeConfig = {
                    format: config.blendshapeFormat || 'arkit'
                };
            }

            if (config.actionConfig) {
                connectConfig.actionConfig = config.actionConfig;
            }

            await this.client.connect(connectConfig);

            // Set up audio renderer for bot voice playback
            this._setupAudioRenderer();

            // Set up lipsync if enabled
            if (config.enableLipsync) {
                this._setupLipsync();
            }

            this.isConnected = true;
            console.log('[ConvaiBridge] Connected successfully');
            this._sendToUnity('OnConvaiConnected', '');

        } catch (e) {
            console.error('[ConvaiBridge] Connection failed:', e);
            this._sendToUnity('OnConvaiError', 'Connection failed: ' + e.message);
        }
    }

    /**
     * Disconnect from Convai.
     */
    async disconnect() {
        if (this.client) {
            try {
                this._stopLipsync();
                await this.client.disconnect();
                this.isConnected = false;
                console.log('[ConvaiBridge] Disconnected');
                this._sendToUnity('OnConvaiDisconnected', '');
            } catch (e) {
                console.warn('[ConvaiBridge] Disconnect error:', e);
            }
        }
    }

    /**
     * Send a text message to the AI character.
     * Called from ConvaiBridge.jslib when user types a message.
     */
    sendText(text) {
        if (!this.client || !this.isConnected) {
            console.warn('[ConvaiBridge] Not connected, cannot send text');
            return;
        }
        console.log('[ConvaiBridge] Sending text:', text);
        this.client.sendUserTextMessage(text);
    }

    /**
     * Toggle microphone for voice input.
     * Called from ConvaiBridge.jslib when user presses talk button.
     */
    async toggleMic() {
        if (!this.client || !this.isConnected) {
            console.warn('[ConvaiBridge] Not connected, cannot toggle mic');
            return;
        }
        try {
            await this.client.audioControls.toggleAudio();
            console.log('[ConvaiBridge] Mic toggled');
        } catch (e) {
            console.warn('[ConvaiBridge] Mic toggle error:', e);
            this._sendToUnity('OnConvaiError', 'Mic error: ' + e.message);
        }
    }

    /**
     * Start microphone for voice input.
     */
    async startMic() {
        if (!this.client || !this.isConnected) return;
        try {
            await this.client.audioControls.unmuteAudio();
            console.log('[ConvaiBridge] Mic started');
            this._sendToUnity('OnConvaiMicStateChanged', '1');
        } catch (e) {
            console.warn('[ConvaiBridge] Mic start error:', e);
        }
    }

    /**
     * Stop microphone for voice input.
     */
    async stopMic() {
        if (!this.client || !this.isConnected) return;
        try {
            await this.client.audioControls.muteAudio();
            console.log('[ConvaiBridge] Mic stopped');
            this._sendToUnity('OnConvaiMicStateChanged', '0');
        } catch (e) {
            console.warn('[ConvaiBridge] Mic stop error:', e);
        }
    }

    /**
     * Send a trigger message (for narrative design / pre-scripted events).
     */
    sendTrigger(triggerName, payload) {
        if (!this.client || !this.isConnected) return;
        this.client.sendTriggerMessage(triggerName, payload || '');
    }

    // ========================================================================
    // Event Listeners
    // ========================================================================

    _setupEventListeners() {
        var self = this;

        // Bot ready
        this.client.on('botReady', function () {
            console.log('[ConvaiBridge] Bot ready');
            self._sendToUnity('OnConvaiBotReady', '');
        });

        // State changes (listening, thinking, speaking, idle)
        this.client.on('stateChange', function (state) {
            var agentState = state.agentState || 'unknown';
            self._sendToUnity('OnConvaiStateChanged', agentState);

            // Track talking state for animation
            if (agentState === 'speaking' && !self.isTalking) {
                self.isTalking = true;
                self._sendToUnity('OnConvaiStartedTalking', '');
            } else if (agentState !== 'speaking' && self.isTalking) {
                self.isTalking = false;
                self._sendToUnity('OnConvaiStoppedTalking', '');
            }
        });

        // New messages (bot responses, emotions, actions)
        this.client.on('message', function (message) {
            if (!message) return;

            var type = message.type || '';
            var content = message.content || '';

            switch (type) {
                case 'bot-llm-text':
                    // AI response text
                    self._sendToUnity('OnConvaiBotText', content);
                    break;

                case 'bot-emotion':
                    // Emotion signal (e.g. "happy", "sad", "angry")
                    self._sendToUnity('OnConvaiEmotion', content);
                    break;

                case 'action':
                    // Character action (e.g. "wave", "nod", "point")
                    self._sendToUnity('OnConvaiAction', content);
                    break;

                case 'user-transcription':
                    // User's speech transcription
                    self._sendToUnity('OnConvaiUserText', content);
                    break;
            }
        });

        // Real-time user transcription (partial, while speaking)
        this.client.on('userTranscriptionChange', function (text) {
            self._sendToUnity('OnConvaiUserTranscription', text || '');
        });

        // Error handling
        this.client.on('error', function (err) {
            console.error('[ConvaiBridge] Error:', err);
            self._sendToUnity('OnConvaiError', String(err));
        });

        // Connection lifecycle
        this.client.on('connect', function () {
            console.log('[ConvaiBridge] WebRTC connected');
        });

        this.client.on('disconnect', function () {
            console.log('[ConvaiBridge] WebRTC disconnected');
            self.isConnected = false;
        });
    }

    // ========================================================================
    // Audio Renderer — plays bot voice through speakers
    // ========================================================================

    _setupAudioRenderer() {
        if (!this.client || !this.client.room) return;

        try {
            // Create a hidden audio element for bot audio playback
            var audioEl = document.createElement('audio');
            audioEl.id = 'convai-audio';
            audioEl.autoplay = true;
            audioEl.style.display = 'none';
            document.body.appendChild(audioEl);

            // Listen for audio tracks from the WebRTC room
            var room = this.client.room;
            if (room) {
                room.on('trackSubscribed', function (track) {
                    if (track.kind === 'audio') {
                        track.attach(audioEl);
                        console.log('[ConvaiBridge] Audio track attached');
                    }
                });
            }

            console.log('[ConvaiBridge] Audio renderer set up');
        } catch (e) {
            console.warn('[ConvaiBridge] Audio renderer setup failed:', e);
        }
    }

    // ========================================================================
    // Lip-sync — streams blendshape data to Unity at 60fps
    // ========================================================================

    _setupLipsync() {
        if (!this.client) return;

        var self = this;

        // Track when bot starts speaking to sync timing
        this.client.on('stateChange', function (state) {
            if (state.agentState === 'speaking') {
                self.lipsyncStartTime = performance.now();
            }
        });

        // Start the animation loop
        this._startLipsyncLoop();
        console.log('[ConvaiBridge] Lipsync enabled');
    }

    _startLipsyncLoop() {
        var self = this;

        function animate() {
            if (!self.client || !self.client.blendshapeQueue) {
                self.lipsyncAnimFrameId = requestAnimationFrame(animate);
                return;
            }

            var queue = self.client.blendshapeQueue;

            if (queue.hasFrames() && queue.isConversationActive()) {
                var elapsedTime = (performance.now() - self.lipsyncStartTime) / 1000;
                var result = queue.getFrameAtTime(elapsedTime);

                if (result && result.frame) {
                    // Convert Float32Array to CSV string for SendMessage
                    // Send only the first 52 blendshape values (ARKit standard)
                    var values = [];
                    var count = Math.min(result.frame.length, 52);
                    for (var i = 0; i < count; i++) {
                        values.push(result.frame[i].toFixed(4));
                    }
                    self._sendToUnity('OnConvaiBlendshapes', values.join(','));
                }
            }

            self.lipsyncAnimFrameId = requestAnimationFrame(animate);
        }

        this.lipsyncAnimFrameId = requestAnimationFrame(animate);
    }

    _stopLipsync() {
        if (this.lipsyncAnimFrameId !== null) {
            cancelAnimationFrame(this.lipsyncAnimFrameId);
            this.lipsyncAnimFrameId = null;
        }
    }

    // ========================================================================
    // Unity SendMessage helper
    // ========================================================================

    _sendToUnity(methodName, data) {
        if (window.unityInstance && this.unityObjectName) {
            window.unityInstance.SendMessage(this.unityObjectName, methodName, data);
        }
    }
}


// ============================================================================
// Export to global scope (same pattern as xr8-bridge.js)
// ============================================================================
window.ConvaiBridge = ConvaiBridge;
