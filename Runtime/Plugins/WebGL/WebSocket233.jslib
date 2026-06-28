var WebSocket233Library =
{
    $ws233Manager:
    {
        instances: {},
        lastId: 0,
        onOpen: null,
        onMessage: null,
        onMessageRing: null,
        onError: null,
        onClose: null,
        support6000: false,

        tryDeliverRing: function(instanceId, array)
        {
            var instance = ws233Manager.instances[instanceId];
            if (!instance || !instance.ring || !ws233Manager.onMessageRing)
            {
                return false;
            }

            var ring = instance.ring;
            if (array.length > ring.slotSize)
            {
                return false;
            }

            for (var attempt = 0; attempt < ring.slotCount; attempt++)
            {
                var slot = (instance.ringCursor + attempt) % ring.slotCount;
                var flagIndex = ring.flagsOffset + slot;
                if (HEAPU8[flagIndex] !== 0)
                {
                    continue;
                }

                HEAPU8[flagIndex] = 1;
                HEAPU8.set(array, ring.base + slot * ring.slotSize);
                instance.ringCursor = (slot + 1) % ring.slotCount;

                if (ws233Manager.support6000)
                {
                    {{{ makeDynCall('viii', 'ws233Manager.onMessageRing') }}}(instanceId, slot, array.length);
                }
                else
                {
                    Module.dynCall_viii(ws233Manager.onMessageRing, instanceId, slot, array.length);
                }

                return true;
            }

            return false;
        },

        deliverPooled: function(instanceId, array)
        {
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

    WebSocket233SetOnMessageRing: function(callback)
    {
        ws233Manager.onMessageRing = callback;
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
            subProtocols: null,
            ring: null,
            ringCursor: 0
        };
        return id;
    },

    WebSocket233BindReceiveRing: function(instanceId, bufferPtr, slotSize, slotCount, flagsOffset)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (slotSize < 1 || slotCount < 1) return -2;

        instance.ring = {
            base: bufferPtr,
            slotSize: slotSize,
            slotCount: slotCount,
            flagsOffset: flagsOffset
        };
        instance.ringCursor = 0;
        return 0;
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
                if (!ws233Manager.tryDeliverRing(instanceId, array))
                {
                    ws233Manager.deliverPooled(instanceId, array);
                }
            }
            else if (typeof Blob !== "undefined" && ev.data instanceof Blob)
            {
                var reader = new FileReader();
                reader.onload = function()
                {
                    var array = new Uint8Array(reader.result);
                    if (!ws233Manager.tryDeliverRing(instanceId, array))
                    {
                        ws233Manager.deliverPooled(instanceId, array);
                    }
                    reader = null;
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
