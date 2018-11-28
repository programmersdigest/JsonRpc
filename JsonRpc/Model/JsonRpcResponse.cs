using Newtonsoft.Json;

namespace programmersdigest.JsonRpc.Model
{
    internal class JsonRpcResponse
    {
#pragma warning disable IDE1006 // Naming Styles
        public string jsonrpc { get; } = "2.0";

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object result { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JsonRpcError error { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object id { get; set; }
#pragma warning restore IDE1006 // Naming Styles


        public JsonRpcResponse()
        {
        }

        public JsonRpcResponse(object id, object result)
        {
            this.id = id;
            this.result = result;
        }

        public JsonRpcResponse(object id, int code, string message, object data = null)
        {
            this.id = id;
            error = new JsonRpcError
            {
                code = code,
                message = message,
                data = data
            };
        }
    }
}
