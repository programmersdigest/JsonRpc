using Newtonsoft.Json.Linq;
using programmersdigest.JsonRpc.Model;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace programmersdigest.JsonRpc
{
    internal class JsonRpcServer
    {
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

        public JsonRpcResponse ProcessRequest(object id, JObject jObject)
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
            catch (ArgumentException)
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
    }
}
