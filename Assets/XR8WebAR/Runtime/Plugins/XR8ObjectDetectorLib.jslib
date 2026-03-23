var XR8ObjectDetectorLib = {

    $WGL_OD_State: {
        gameObjectName: null,
        active: false,
        model: null,
        confidenceThreshold: 0.5,
        intervalMs: 500,
        intervalId: null,
        video: null,
        loading: false
    },

    WebGLStartObjectDetection: function(goNamePtr, confidenceThreshold, intervalMs) {
        var goName = UTF8ToString(goNamePtr);
        WGL_OD_State.gameObjectName = goName;
        WGL_OD_State.confidenceThreshold = confidenceThreshold;
        WGL_OD_State.intervalMs = intervalMs;
        WGL_OD_State.active = true;

        // Load TensorFlow.js + COCO-SSD from CDN
        if (WGL_OD_State.loading) return;
        WGL_OD_State.loading = true;

        _loadScript('https://cdn.jsdelivr.net/npm/@tensorflow/tfjs@4.17.0/dist/tf.min.js', function() {
            _loadScript('https://cdn.jsdelivr.net/npm/@tensorflow-models/coco-ssd@2.2.3/dist/coco-ssd.min.js', function() {
                console.log('[XR8ObjectDetector] Loading COCO-SSD model...');
                cocoSsd.load().then(function(model) {
                    WGL_OD_State.model = model;
                    WGL_OD_State.loading = false;
                    console.log('[XR8ObjectDetector] Model loaded, starting detection');
                    _startDetectionLoop();
                }).catch(function(err) {
                    console.error('[XR8ObjectDetector] Model load failed:', err);
                    WGL_OD_State.loading = false;
                });
            });
        });
    },

    WebGLStopObjectDetection: function() {
        WGL_OD_State.active = false;
        if (WGL_OD_State.intervalId) {
            clearInterval(WGL_OD_State.intervalId);
            WGL_OD_State.intervalId = null;
        }
    },

    WebGLSetDetectionThreshold: function(threshold) {
        WGL_OD_State.confidenceThreshold = threshold;
    }
};

function _loadScript(url, callback) {
    // Check if already loaded
    var scripts = document.getElementsByTagName('script');
    for (var i = 0; i < scripts.length; i++) {
        if (scripts[i].src === url) {
            callback();
            return;
        }
    }

    var script = document.createElement('script');
    script.src = url;
    script.onload = callback;
    script.onerror = function() {
        console.error('[XR8ObjectDetector] Failed to load: ' + url);
    };
    document.head.appendChild(script);
}

function _startDetectionLoop() {
    if (!WGL_OD_State.active || !WGL_OD_State.model) return;

    // Find camera video element (8th Wall renders to a <video> or <canvas>)
    var video = document.querySelector('video');
    if (!video) {
        var canvas = document.querySelector('#camerafeed') || document.querySelector('canvas');
        WGL_OD_State.video = canvas;
    } else {
        WGL_OD_State.video = video;
    }

    if (!WGL_OD_State.video) {
        console.warn('[XR8ObjectDetector] No video/canvas element found, retrying in 1s...');
        setTimeout(_startDetectionLoop, 1000);
        return;
    }

    WGL_OD_State.intervalId = setInterval(function() {
        if (!WGL_OD_State.active || !WGL_OD_State.model || !WGL_OD_State.video) return;

        WGL_OD_State.model.detect(WGL_OD_State.video).then(function(predictions) {
            if (!WGL_OD_State.active) return;

            // Filter by confidence
            var filtered = predictions.filter(function(p) {
                return p.score >= WGL_OD_State.confidenceThreshold;
            });

            if (filtered.length === 0) {
                if (typeof SendMessage !== 'undefined') {
                    SendMessage(WGL_OD_State.gameObjectName, 'OnDetectionResults', 'none');
                }
                return;
            }

            // Build CSV: "label,conf,x,y,w,h|label,conf,x,y,w,h|..."
            var videoW = WGL_OD_State.video.videoWidth || WGL_OD_State.video.width || 640;
            var videoH = WGL_OD_State.video.videoHeight || WGL_OD_State.video.height || 480;

            var csv = filtered.map(function(p) {
                var bbox = p.bbox; // [x, y, width, height] in pixels
                return p.class + ',' +
                       p.score.toFixed(3) + ',' +
                       (bbox[0] / videoW).toFixed(4) + ',' +
                       (bbox[1] / videoH).toFixed(4) + ',' +
                       (bbox[2] / videoW).toFixed(4) + ',' +
                       (bbox[3] / videoH).toFixed(4);
            }).join('|');

            if (typeof SendMessage !== 'undefined') {
                SendMessage(WGL_OD_State.gameObjectName, 'OnDetectionResults', csv);
            }
        }).catch(function(err) {
            // Silently ignore detection errors (e.g. video not ready)
        });

    }, WGL_OD_State.intervalMs);
}

autoAddDeps(XR8ObjectDetectorLib, '$WGL_OD_State');
mergeInto(LibraryManager.library, XR8ObjectDetectorLib);
