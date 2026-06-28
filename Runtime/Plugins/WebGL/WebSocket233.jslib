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
        deliverScratch: null,
        stringScratch: null,

        cleanupWebSocket: function(ws)
        {
            if (ws == null)
            {
                return;
            }

            ws.onopen = null;
            ws.onmessage = null;
            ws.onerror = null;
            ws.onclose = null;
        },

        safeClose: function(ws)
        {
            if (ws == null)
            {
                return;
            }

            try
            {
                if (ws.readyState != null && ws.readyState < 2)
                {
                    ws.close();
                }
            }
            catch (e)
            {
            }

            ws233Manager.cleanupWebSocket(ws);
        },

        ensureDeliverScratch: function(minSize)
        {
            if (!ws233Manager.deliverScratch || ws233Manager.deliverScratch.capacity < minSize)
            {
                if (ws233Manager.deliverScratch)
                {
                    _free(ws233Manager.deliverScratch.ptr);
                }

                ws233Manager.deliverScratch = {
                    ptr: _malloc(minSize),
                    capacity: minSize
                };
            }

            return ws233Manager.deliverScratch.ptr;
        },

        ensureStringScratch: function(minSize)
        {
            if (!ws233Manager.stringScratch || ws233Manager.stringScratch.capacity < minSize)
            {
                if (ws233Manager.stringScratch)
                {
                    _free(ws233Manager.stringScratch.ptr);
                }

                ws233Manager.stringScratch = {
                    ptr: _malloc(minSize),
                    capacity: minSize
                };
            }

            return ws233Manager.stringScratch.ptr;
        },

        tryDeliverRing: function(instanceId, array, invokeRing)
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
                invokeRing(instanceId, slot, array.length);
                return true;
            }

            return false;
        },

        deliverPooled: function(instanceId, array, invokeMessage)
        {
            var buffer = ws233Manager.ensureDeliverScratch(array.length);
            writeArrayToMemory(array, buffer);
            invokeMessage(instanceId, buffer, array.length);
        },

        destroyInstance: function(instance)
        {
            if (!instance)
            {
                return;
            }

            ws233Manager.safeClose(instance.ws);
            instance.ws = null;
            instance.ring = null;
            instance.subProtocols = null;
            instance.url = null;
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

        ws233Manager.destroyInstance(instance);
        delete ws233Manager.instances[instanceId];
        return 0;
    },

    WebSocket233Connect: function(instanceId)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (instance.ws != null) return -2;

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
            if (!(ev.data instanceof ArrayBuffer))
            {
                return;
            }

            var array = new Uint8Array(ev.data);

            var invokeRing = function(id, slot, len)
            {
                if (ws233Manager.support6000)
                {
                    {{{ makeDynCall('viii', 'ws233Manager.onMessageRing') }}}(id, slot, len);
                }
                else
                {
                    Module.dynCall_viii(ws233Manager.onMessageRing, id, slot, len);
                }
            };

            var invokeMessage = function(id, ptr, len)
            {
                if (ws233Manager.support6000)
                {
                    {{{ makeDynCall('viii', 'ws233Manager.onMessage') }}}(id, ptr, len);
                }
                else
                {
                    Module.dynCall_viii(ws233Manager.onMessage, id, ptr, len);
                }
            };

            if (!ws233Manager.tryDeliverRing(instanceId, array, invokeRing))
            {
                ws233Manager.deliverPooled(instanceId, array, invokeMessage);
            }
        };

        instance.ws.onerror = function()
        {
            var length = lengthBytesUTF8("WebSocket error.") + 1;
            var buffer = ws233Manager.ensureStringScratch(length);
            stringToUTF8("WebSocket error.", buffer, length);
            if (ws233Manager.support6000)
            {
                {{{ makeDynCall('vii', 'ws233Manager.onError') }}}(instanceId, buffer);
            }
            else
            {
                Module.dynCall_vii(ws233Manager.onError, instanceId, buffer);
            }
        };

        instance.ws.onclose = function(ev)
        {
            var reason = ev.reason || "";
            var length = lengthBytesUTF8(reason) + 1;
            var buffer = ws233Manager.ensureStringScratch(length);
            stringToUTF8(reason, buffer, length);
            if (ws233Manager.support6000)
            {
                {{{ makeDynCall('viii', 'ws233Manager.onClose') }}}(instanceId, ev.code, buffer);
            }
            else
            {
                Module.dynCall_viii(ws233Manager.onClose, instanceId, ev.code, buffer);
            }

            ws233Manager.cleanupWebSocket(instance.ws);
            instance.ws = null;
        };

        return 0;
    },

    WebSocket233Close: function(instanceId, code, reasonPtr)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (instance.ws == null) return -3;

        var state = instance.ws.readyState;
        if (state === 2) return -4;
        if (state === 3) return -5;

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
        if (instance.ws == null) return -3;
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
        if (instance.ws == null) return -3;
        if (instance.ws.readyState !== 1) return -6;

        instance.ws.send(UTF8ToString(stringPtr));
        return 0;
    },

    WebSocket233GetState: function(instanceId)
    {
        var instance = ws233Manager.instances[instanceId];
        if (!instance) return -1;
        if (instance.ws == null) return 3;
        return instance.ws.readyState;
    }
};

autoAddDeps(WebSocket233Library, '$ws233Manager');
mergeInto(LibraryManager.library, WebSocket233Library);
