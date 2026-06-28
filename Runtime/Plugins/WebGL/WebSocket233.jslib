var WebSocket233Library =
{
    $ws233Manager:
    {
        instances: {},
        lastId: 0,
        onOpen: null,
        onMessage: null,
        onError: null,
        onClose: null,
        support6000: false
    },

    WebSocket233SetSupport6000: function()
    {
        ws233Manager.support6000 = true;
    },

    WebSocket233SetOnOpen: function(callback)
    {
        ws233Manager.onOpen = callback;
    },

    WebSocket233SetOnMessage: function(callback)
    {
        ws233Manager.onMessage = callback;
    },

    WebSocket233SetOnError: function(callback)
    {
        ws233Manager.onError = callback;
    },

    WebSocket233SetOnClose: function(callback)
    {
        ws233Manager.onClose = callback;
    },

    WebSocket233Allocate: function(urlPtr)
    {
        var url = UTF8ToString(urlPtr);
        var id = ++ws233Manager.lastId;
        ws233Manager.instances[id] = {
            url: url,
            ws: null,
            subProtocols: null
        };
        return id;
    },

    WebSocket233AddSubProtocol: function(instanceId, protocolPtr)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;

        var protocol = UTF8ToString(protocolPtr);
        if (instance.subProtocols == null)
        {
            instance.subProtocols = [];
        }

        instance.subProtocols.push(protocol);
        return 0;
    },

    WebSocket233Free: function(instanceId)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return 0;

        if (instance.ws !== null && instance.ws.readyState < 2)
        {
            instance.ws.close();
        }

        delete ws233Manager.instances[instanceId];
        return 0;
    },

    WebSocket233Connect: function(instanceId)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (instance.ws !== null) return -2;

        if (instance.subProtocols != null)
        {
            instance.ws = new WebSocket(instance.url, instance.subProtocols);
        }
        else
        {
            instance.ws = new WebSocket(instance.url);
        }

        instance.ws.binaryType = "arraybuffer";

        instance.ws.onopen = function()
        {
            if (ws233Manager.support6000)
            {
                {{{ makeDynCall('vi', 'ws233Manager.onOpen') }}}(instanceId);
            }
            else
            {
                Module.dynCall_vi(ws233Manager.onOpen, instanceId);
            }
        };

        instance.ws.onmessage = function(ev)
        {
            if (ev.data instanceof ArrayBuffer)
            {
                var array = new Uint8Array(ev.data);
                var buffer = _malloc(array.length);
                writeArrayToMemory(array, buffer);
                try
                {
                    if (ws233Manager.support6000)
                    {
                        {{{ makeDynCall('viii', 'ws233Manager.onMessage') }}}(instanceId, buffer, array.length);
                    }
                    else
                    {
                        Module.dynCall_viii(ws233Manager.onMessage, instanceId, buffer, array.length);
                    }
                }
                finally
                {
                    _free(buffer);
                }
            }
            else if (typeof Blob !== "undefined" && ev.data instanceof Blob)
            {
                var reader = new FileReader();
                reader.onload = function()
                {
                    var array = new Uint8Array(reader.result);
                    var buffer = _malloc(array.length);
                    writeArrayToMemory(array, buffer);
                    try
                    {
                        if (ws233Manager.support6000)
                        {
                            {{{ makeDynCall('viii', 'ws233Manager.onMessage') }}}(instanceId, buffer, array.length);
                        }
                        else
                        {
                            Module.dynCall_viii(ws233Manager.onMessage, instanceId, buffer, array.length);
                        }
                    }
                    finally
                    {
                        reader = null;
                        _free(buffer);
                    }
                };
                reader.readAsArrayBuffer(ev.data);
            }
        };

        instance.ws.onerror = function()
        {
            var msg = "WebSocket error.";
            var length = lengthBytesUTF8(msg) + 1;
            var buffer = _malloc(length);
            stringToUTF8(msg, buffer, length);
            try
            {
                if (ws233Manager.support6000)
                {
                    {{{ makeDynCall('vii', 'ws233Manager.onError') }}}(instanceId, buffer);
                }
                else
                {
                    Module.dynCall_vii(ws233Manager.onError, instanceId, buffer);
                }
            }
            finally
            {
                _free(buffer);
            }
        };

        instance.ws.onclose = function(ev)
        {
            var msg = ev.reason || "";
            var length = lengthBytesUTF8(msg) + 1;
            var buffer = _malloc(length);
            stringToUTF8(msg, buffer, length);
            try
            {
                if (ws233Manager.support6000)
                {
                    {{{ makeDynCall('viii', 'ws233Manager.onClose') }}}(instanceId, ev.code, buffer);
                }
                else
                {
                    Module.dynCall_viii(ws233Manager.onClose, instanceId, ev.code, buffer);
                }
            }
            finally
            {
                _free(buffer);
            }
            instance.ws = null;
        };

        return 0;
    },

    WebSocket233Close: function(instanceId, code, reasonPtr)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (instance.ws === null) return -3;
        if (instance.ws.readyState === 2) return -4;
        if (instance.ws.readyState === 3) return -5;

        var reason = reasonPtr ? UTF8ToString(reasonPtr) : undefined;
        try
        {
            instance.ws.close(code, reason);
        }
        catch (err)
        {
            return -7;
        }

        return 0;
    },

    // Zero-copy send: Uint8Array view over WASM heap instead of buffer.slice().
    WebSocket233Send: function(instanceId, bufferPtr, length)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (instance.ws === null) return -3;
        if (instance.ws.readyState !== 1) return -6;

        if (typeof HEAPU8 !== "undefined")
        {
            instance.ws.send(new Uint8Array(HEAPU8.buffer, bufferPtr, length));
        }
        else if (typeof buffer !== "undefined")
        {
            instance.ws.send(new Uint8Array(buffer, bufferPtr, length));
        }
        else
        {
            return -8;
        }

        return 0;
    },

    WebSocket233SendStr: function(instanceId, stringPtr)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (instance.ws === null) return -3;
        if (instance.ws.readyState !== 1) return -6;

        instance.ws.send(UTF8ToString(stringPtr));
        return 0;
    },

    WebSocket233GetState: function(instanceId)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (instance.ws === null) return 3;
        return instance.ws.readyState;
    }
};

autoAddDeps(WebSocket233Library, '$ws233Manager');
mergeInto(LibraryManager.library, WebSocket233Library);
