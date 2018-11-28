using Newtonsoft.Json;

namespace programmersdigest.JsonRpc.Model
{
    internal class JsonRpcError
    {
        public const int CODE_PARSE_ERROR = -32700;
        public const int CODE_INVALID_REQUEST = -32600;
        public const int CODE_METHOD_NOT_FOUND = -32601;
        public const int CODE_INVALID_PARAMS = -32602;
        public const int CODE_INTERNAL_ERROR = -32063;

#pragma warning disable IDE1006 // Naming Styles
        public int code { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string message { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object data { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}