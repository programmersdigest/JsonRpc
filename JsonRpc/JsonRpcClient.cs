using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using programmersdigest.JsonRpc.Model;
using programmersdigest.Util.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace programmersdigest.JsonRpc
{
    public class JsonRpcClient
    {
        private readonly Action<byte[]> _sendCallback;
        private readonly Func<byte[]> _receiveCallback;

        private readonly WorkerThread _receiveThread;

        public JsonRpcClient(Action<byte[]> sendCallback, Func<byte[]> receiveCallback)
        {
            _sendCallback = sendCallback ?? throw new ArgumentNullException(nameof(sendCallback));
            _receiveCallback = receiveCallback ?? throw new ArgumentNullException(nameof(receiveCallback));

            _receiveThread = new WorkerThread(ReceiveThreadMethod);
        }

        private void SendData(object message)
        {
            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            _sendCallback(bytes);
        }

        private void ReceiveThreadMethod(CancellationToken cancellationToken)
        {
            try
            {
                var data = _receiveCallback();
                if (data.Length <= 0)
                {
                    return;     // We received nothing.
                }

                ProcessReceivedData(data);
            }
            catch (Exception)
            {
                // TODO: What to do with exceptions caught here?
            }
        }

        private void ProcessReceivedData(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var jsonObject = JsonConvert.DeserializeObject(json);
            
            switch (jsonObject)
            {
                case JObject jObject:
                    var response = ProcessReceivedJObject(jObject);
                    if (response != null)
                    {
                        SendData(response);
                    }

                    break;
                case JArray jArray:
                    if (jArray.Count <= 0)
                    {
                        SendData(new JsonRpcResponse(null, JsonRpcError.CODE_INVALID_REQUEST, "Request array must not be empty."));
                        break;
                    }

                    var responseArray = ProcessReceivedJArray(jArray);
                    SendData(responseArray);

                    break;
            }
        }

        private List<JsonRpcResponse> ProcessReceivedJArray(JArray jArray)
        {
            var result = new List<JsonRpcResponse>();

            foreach (var jToken in jArray)
            {
                if (!(jToken is JObject))
                {
                    result.Add(new JsonRpcResponse(null, JsonRpcError.CODE_INVALID_REQUEST, "The message is not a valid request object."));
                    continue;
                }

                var response = ProcessReceivedJObject((JObject)jToken);
                if (response != null)
                {
                    result.Add(response);
                }
            }

            return result;
        }

        private JsonRpcResponse ProcessReceivedJObject(JObject jObject)
        {
            var id = jObject.GetValue(nameof(JsonRpcRequest.id));
            var isRequest = jObject.GetValue(nameof(JsonRpcRequest.method)) != null;
            var isResponse = jObject.GetValue(nameof(JsonRpcResponse.result)) != null ||
                             jObject.GetValue(nameof(JsonRpcResponse.error)) != null;

            if (isRequest && isResponse)
            {
                return new JsonRpcResponse(id, JsonRpcError.CODE_INVALID_REQUEST, "A JSON RPC message must not be a message and response at the same time.");
            }
            else if (isRequest)
            {
                return ProcessRequest(id, jObject);
            }
            else if (isResponse)
            {
                ProcessResponse((string)id, jObject);   // We always send id as string, so we expect a string back.
                return null;
            }
            else
            {
                return new JsonRpcResponse(id, JsonRpcError.CODE_INVALID_REQUEST, "Not a valid Json RPC request or response.");
            }
        }

        #region Server - Receives requests and executes RPC calls.

        private ConcurrentDictionary<string, Delegate> _callables = new ConcurrentDictionary<string, Delegate>();

        public void Register(string method, Delegate callable)
        {
            if (!_callables.TryAdd(method, callable))
            {
                throw new ArgumentException("A callable with the given method name has already been registered.");
            }
        }

        public void Unregister(string method)
        {
            _callables.TryRemove(method, out var callable);
        }

        private JsonRpcResponse ProcessRequest(object id, JObject jObject)
        {
            JsonRpcRequest request;

            try
            {
                request = jObject.ToObject<JsonRpcRequest>();
            }
            catch (Exception)
            {
                return new JsonRpcResponse(id, JsonRpcError.CODE_PARSE_ERROR, null);
            }

            if (!_callables.TryGetValue(request.method, out var callable))
            {
                return new JsonRpcResponse(id, JsonRpcError.CODE_METHOD_NOT_FOUND, $"Method {request.method} could not be found.");
            }

            object result = null;

            try
            {
                result = callable.DynamicInvoke(request.@params);
            }
            catch(ArgumentException)
            {
                // TODO: Are there other exception pointing to invalid parameters?
                return new JsonRpcResponse(id, JsonRpcError.CODE_INVALID_PARAMS, "The provided parameters do not match the registered method.");
            }
            catch (TargetParameterCountException)
            {
                return new JsonRpcResponse(id, JsonRpcError.CODE_INVALID_PARAMS, "The method has a different parameter count.");
            }
            catch (Exception)
            {
                return new JsonRpcResponse(id, JsonRpcError.CODE_INTERNAL_ERROR, null);
            }

            return new JsonRpcResponse(id, result);
        }

        #endregion

        #region Client - Sends requests and receives responses.

        private ConcurrentDictionary<string, Action<JsonRpcResponse>> _responseCallbacks = new ConcurrentDictionary<string, Action<JsonRpcResponse>>();

        public void Notify(string method, params object[] parameters)
        {
            var request = new JsonRpcRequest
            {
                method = method,
                @params = parameters.Any() ? parameters : null
            };
            SendData(request);
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
            SendData(request);

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

        private void ProcessResponse(string id, JObject jObject)
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

        #endregion
    }
}
