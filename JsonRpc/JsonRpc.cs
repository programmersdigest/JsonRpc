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
    public class JsonRpc
    {
        private readonly Action<byte[]> _sendCallback;
        private readonly Func<byte[]> _receiveCallback;

        private readonly JsonRpcServer _jsonRpcServer;
        private readonly JsonRpcClient _jsonRpcClient;
        private readonly WorkerThread _receiveThread;

        public JsonRpc(Action<byte[]> sendCallback, Func<byte[]> receiveCallback)
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

        public void Notify(string method, params object[] parameters)
        {
            _jsonRpcClient.Notify(method, parameters);
        }

        public object Call(string method, params object[] parameters)
        {
            return _jsonRpcClient.Call(method, parameters);
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
                // Transport error. TODO: Maybe add a logging callback?
            }
        }

        private void ProcessReceivedData(byte[] data)
        {
            JToken jToken;
            try
            {
                var json = Encoding.UTF8.GetString(data);
                jToken = JsonConvert.DeserializeObject<JToken>(json);
            }
            catch (Exception)
            {
                SendData(new JsonRpcResponse(null, JsonRpcError.CODE_PARSE_ERROR, "Unable to parse request message."));
                return;
            }

            switch (jToken)
            {
                case JObject jObject:
                    ProcessMessageAndRespond(jObject);
                    break;
                case JArray jArray:
                    ProcessBatchAndRespond(jArray);
                    break;
            }
        }

        private void ProcessBatchAndRespond(JArray jArray)
        {
            if (jArray.Count <= 0)
            {
                var response = new JsonRpcResponse(null, JsonRpcError.CODE_INVALID_REQUEST, "Request array must not be empty.");
                SendData(response);

                return;
            }

            var responseArray = ProcessBatch(jArray);
            SendData(responseArray);
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

        private void ProcessMessageAndRespond(JObject jObject)
        {
            var response = ProcessMessage(jObject);
            if (response != null)
            {
                SendData(response);
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
