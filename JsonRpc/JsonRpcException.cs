using System;
using System.Runtime.Serialization;

namespace programmersdigest.JsonRpc
{
    [Serializable]
    internal class JsonRpcException : Exception
    {
        public int Code { get; }

        public JsonRpcException(int code, string message) : base(message)
        {
            Code = code;
        }

        protected JsonRpcException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}