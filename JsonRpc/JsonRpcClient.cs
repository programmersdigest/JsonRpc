﻿using Newtonsoft.Json.Linq;
using programmersdigest.JsonRpc.Model;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace programmersdigest.JsonRpc
{
    internal class JsonRpcClient
    {
        private readonly Action<object> _sendDataCallback;
        private readonly ConcurrentDictionary<string, Action<JsonRpcResponse>> _responseCallbacks = new ConcurrentDictionary<string, Action<JsonRpcResponse>>();

        public JsonRpcClient(Action<object> sendDataCallback)
        {
            _sendDataCallback = sendDataCallback;
        }

        public void Notify(string method, params object[] parameters)
        {
            var request = new JsonRpcRequest
            {
                method = method,
                @params = parameters.Any() ? parameters : null
            };
            _sendDataCallback(request);
        }

        public object Call(string method, params object[] parameters)
        {
            var resetEvent = new ManualResetEvent(false);
            JsonRpcResponse response = null;

            void responseCallback(JsonRpcResponse r)
            {
                response = r;
                resetEvent.Set();
            }

            var id = Guid.NewGuid().ToString();
            if (!_responseCallbacks.TryAdd(id, responseCallback))
            {
                throw new InvalidOperationException("The generated GUID was already taken. Congrats, you had a chance of about 1 in 10^38.");       // How lucky is that?
            }

            var request = new JsonRpcRequest
            {
                method = method,
                @params = parameters.Any() ? parameters : null,
                id = id
            };
            _sendDataCallback(request);

            if (!resetEvent.WaitOne(1000))
            {
                _responseCallbacks.TryRemove(id, out var junk);
                throw new TimeoutException("The server did not respond within the given timeout.");
            }

            if (response.error != null)
            {
                throw new JsonRpcException(response.error.code, response.error.message);
            }

            return response.result;
        }

        public void ProcessResponse(string id, JObject jObject)
        {
            JsonRpcResponse response = null;

            try
            {
                response = jObject.ToObject<JsonRpcResponse>();
            }
            catch (Exception)
            {
                _responseCallbacks.TryRemove(id, out var junk);
                return;
            }

            if (_responseCallbacks.TryRemove(id, out var callback))
            {
                callback(response);
            }
        }
    }
}
