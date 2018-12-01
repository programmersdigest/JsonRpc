using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using programmersdigest.JsonRpc.Model;
using programmersdigest.Util.Threading;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace programmersdigest.JsonRpc
{
    public delegate void JsonRpcSendCallback(byte[] data, object state);
    public delegate (byte[] data, object state) JsonRpcReceiveCallback();
    
    public class JsonRpc
    {
        private readonly JsonRpcSendCallback _sendCallback;
        private readonly JsonRpcReceiveCallback _receiveCallback;

        private readonly JsonRpcServer _jsonRpcServer;
        private readonly JsonRpcClient _jsonRpcClient;
        private readonly WorkerThread _receiveThread;

        public JsonRpc(JsonRpcSendCallback sendCallback, JsonRpcReceiveCallback receiveCallback)
        {
            _sendCallback = sendCallback ?? throw new ArgumentNullException(nameof(sendCallback));
            _receiveCallback = receiveCallback ?? throw new ArgumentNullException(nameof(receiveCallback));

            _jsonRpcServer = new JsonRpcServer();
            _jsonRpcClient = new JsonRpcClient(SendData);

            _receiveThread = new WorkerThread(ReceiveThreadMethod);
        }

        public void Register(string method, Delegate callable)
        {
            _jsonRpcServer.Register(method, callable);
        }

        public void Unregister(string method)
        {
            _jsonRpcServer.Unregister(method);
        }

        public void Notify(string method, object state, params object[] parameters)
        {
            _jsonRpcClient.Notify(method, state, parameters);
        }

        public object Call(string method, object state, params object[] parameters)
        {
            return _jsonRpcClient.Call(method, state, parameters);
        }

        private void SendData(object message, object state)
        {
            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            _sendCallback(bytes, state);
        }

        private void ReceiveThreadMethod(CancellationToken cancellationToken)
        {
            try
            {
                var (data, state) = _receiveCallback();
                if (data.Length <= 0)
                {
                    return;     // We received nothing.
                }

                ProcessReceivedData(data, state);
            }
            catch (Exception)
            {
                // Transport error. TODO: Maybe add a logging callback?
            }
        }

        private void ProcessReceivedData(byte[] data, object state)
        {
            JToken jToken;
            try
            {
                var json = Encoding.UTF8.GetString(data);
                jToken = JsonConvert.DeserializeObject<JToken>(json);
            }
            catch (Exception)
            {
                SendData(new JsonRpcResponse(null, JsonRpcError.CODE_PARSE_ERROR, "Unable to parse request message."), state);
                return;
            }

            switch (jToken)
            {
                case JObject jObject:
                    ProcessMessageAndRespond(jObject, state);
                    break;
                case JArray jArray:
                    ProcessBatchAndRespond(jArray, state);
                    break;
            }
        }

        private void ProcessBatchAndRespond(JArray jArray, object state)
        {
            if (jArray.Count <= 0)
            {
                var response = new JsonRpcResponse(null, JsonRpcError.CODE_INVALID_REQUEST, "Request array must not be empty.");
                SendData(response, state);

                return;
            }

            var responseArray = ProcessBatch(jArray);
            SendData(responseArray, state);
        }

        private List<JsonRpcResponse> ProcessBatch(JArray jArray)
        {
            var result = new List<JsonRpcResponse>();

            foreach (var jToken in jArray)
            {
                if (!(jToken is JObject))
                {
                    result.Add(new JsonRpcResponse(null, JsonRpcError.CODE_INVALID_REQUEST, "The message is not a valid request object."));
                    continue;
                }

                var response = ProcessMessage((JObject)jToken);
                if (response != null)
                {
                    result.Add(response);
                }
            }

            return result;
        }

        private void ProcessMessageAndRespond(JObject jObject, object state)
        {
            var response = ProcessMessage(jObject);
            if (response != null)
            {
                SendData(response, state);
            }
        }

        private JsonRpcResponse ProcessMessage(JObject jObject)
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
                return _jsonRpcServer.ProcessRequest(id, jObject);
            }
            else if (isResponse)
            {
                _jsonRpcClient.ProcessResponse((string)id, jObject);   // We always send id as string, so we expect a string back.
                return null;
            }
            else
            {
                return new JsonRpcResponse(id, JsonRpcError.CODE_INVALID_REQUEST, "Not a valid Json RPC request or response.");
            }
        }
    }
}
