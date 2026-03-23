var XR8SharedLib = {

    $WGL_SS_State: {
        gameObjectName: null,
        socket: null,
        roomCode: null,
        reconnectAttempts: 0,
        maxReconnectAttempts: 5
    },

    WebGLSharedConnect: function(goNamePtr, serverUrlPtr) {
        var goName = UTF8ToString(goNamePtr);
        var serverUrl = UTF8ToString(serverUrlPtr);
        WGL_SS_State.gameObjectName = goName;
        WGL_SS_State.reconnectAttempts = 0;

        _connectWebSocket(serverUrl);
    },

    WebGLSharedDisconnect: function() {
        if (WGL_SS_State.socket) {
            WGL_SS_State.socket.close();
            WGL_SS_State.socket = null;
        }
    },

    WebGLSharedCreateRoom: function() {
        if (!WGL_SS_State.socket) return;
        WGL_SS_State.socket.send(JSON.stringify({
            type: 'create_room'
        }));
    },

    WebGLSharedJoinRoom: function(roomCodePtr) {
        if (!WGL_SS_State.socket) return;
        var roomCode = UTF8ToString(roomCodePtr);
        WGL_SS_State.socket.send(JSON.stringify({
            type: 'join_room',
            room: roomCode
        }));
    },

    WebGLSharedSendTransform: function(objectIdPtr, csvPtr) {
        if (!WGL_SS_State.socket || WGL_SS_State.socket.readyState !== WebSocket.OPEN) return;
        var objectId = UTF8ToString(objectIdPtr);
        var csv = UTF8ToString(csvPtr);
        WGL_SS_State.socket.send(JSON.stringify({
            type: 'transform',
            id: objectId,
            data: csv
        }));
    },

    WebGLSharedSendMessage: function(msgTypePtr, payloadPtr) {
        if (!WGL_SS_State.socket || WGL_SS_State.socket.readyState !== WebSocket.OPEN) return;
        var msgType = UTF8ToString(msgTypePtr);
        var payload = UTF8ToString(payloadPtr);
        WGL_SS_State.socket.send(JSON.stringify({
            type: 'custom',
            msgType: msgType,
            payload: payload
        }));
    }
};

function _connectWebSocket(url) {
    try {
        WGL_SS_State.socket = new WebSocket(url);
    } catch (e) {
        console.error('[XR8SharedSession] WebSocket creation failed:', e);
        return;
    }

    WGL_SS_State.socket.onopen = function() {
        console.log('[XR8SharedSession] Connected to relay');
        WGL_SS_State.reconnectAttempts = 0;
        if (typeof SendMessage !== 'undefined') {
            SendMessage(WGL_SS_State.gameObjectName, 'OnSharedConnected', '');
        }
    };

    WGL_SS_State.socket.onclose = function() {
        console.log('[XR8SharedSession] Disconnected');
        if (typeof SendMessage !== 'undefined') {
            SendMessage(WGL_SS_State.gameObjectName, 'OnSharedDisconnected', '');
        }

        // Auto-reconnect
        if (WGL_SS_State.reconnectAttempts < WGL_SS_State.maxReconnectAttempts) {
            WGL_SS_State.reconnectAttempts++;
            var delay = Math.min(1000 * Math.pow(2, WGL_SS_State.reconnectAttempts), 30000);
            console.log('[XR8SharedSession] Reconnecting in ' + delay + 'ms...');
            setTimeout(function() { _connectWebSocket(url); }, delay);
        }
    };

    WGL_SS_State.socket.onerror = function(err) {
        console.error('[XR8SharedSession] WebSocket error:', err);
    };

    WGL_SS_State.socket.onmessage = function(event) {
        var msg;
        try {
            msg = JSON.parse(event.data);
        } catch (e) {
            return;
        }

        if (typeof SendMessage === 'undefined') return;
        var goName = WGL_SS_State.gameObjectName;

        switch (msg.type) {
            case 'room_created':
                WGL_SS_State.roomCode = msg.room;
                SendMessage(goName, 'OnSharedRoomCreated', msg.room);
                break;

            case 'room_joined':
                WGL_SS_State.roomCode = msg.room;
                SendMessage(goName, 'OnSharedRoomJoined', msg.room);
                break;

            case 'peer_joined':
                SendMessage(goName, 'OnSharedPeerJoined', String(msg.count || 0));
                break;

            case 'peer_left':
                SendMessage(goName, 'OnSharedPeerLeft', String(msg.count || 0));
                break;

            case 'transform':
                // Relay transform from peer: prepend objectId
                SendMessage(goName, 'OnSharedTransform', msg.id + ',' + msg.data);
                break;

            case 'custom':
                SendMessage(goName, 'OnSharedCustomMessage', msg.msgType + '|' + msg.payload);
                break;
        }
    };
}

autoAddDeps(XR8SharedLib, '$WGL_SS_State');
mergeInto(LibraryManager.library, XR8SharedLib);
