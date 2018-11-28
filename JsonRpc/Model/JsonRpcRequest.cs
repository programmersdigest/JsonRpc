using Newtonsoft.Json;

namespace programmersdigest.JsonRpc.Model
{
    internal class JsonRpcRequest
    {
#pragma warning disable IDE1006 // Naming Styles
        public string jsonrpc { get; } = "2.0";

        public string method { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object[] @params { get; set; } = null;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object id { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}
