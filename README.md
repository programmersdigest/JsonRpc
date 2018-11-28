[![Build status](https://ci.appveyor.com/api/projects/status/github/programmersdigest/JsonRpc?branch=master&svg=true)](https://ci.appveyor.com/api/projects/status/github/programmersdigest/JsonRpc?branch=master&svg=true)
# JsonRpc
An implementation of the JSON-RPC 2.0 protocol.

## Description
This project implements the *JSON-RPC 2.0 protocol* in C#. The implementation is independent of the underlying transport which allows its use over HTTP, TCP, WebSockets, etc.

From the JSON-RPC 2.0 specification:
> JSON-RPC is a stateless, light-weight remote procedure call (RPC) protocol. Primarily this specification defines several data structures and the rules around their processing. It is transport agnostic in that the concepts can be used within the same process, over sockets, over http, or in many various message passing environments. It uses JSON (RFC 4627) as data format.
> 
> It is designed to be simple!

**Do note**: This project is a hobby project for my own personal use. It may therefore be incomplete and/or faulty. If you have corrections or suggestions, please let me know.

## Features
- Full implementation of the JSON-RPC 2.0 protocol
- Independent of the underlying transport protocol
- Compatible with any JSON-RPC library which adheres to the JSON-RPC 2.0 specification

## Usage
Grab the latest version from NuGet https://www.nuget.org/packages/programmersdigest.JsonRpc
All relevant classes are contained in the namespace _programmersdigest.JsonRpc_.
Available for _.NET Standard 2.0_ and _.NET Framework 4.5_.

```
// Instantiate JsonRpc with callbacks for sending and receiving data.
// Note: The receive callback is executed on a separate thread in a loop. JsonRpc expects to receive a single
//       message (request/response) per call to the callback.
var jsonRpc = new JsonRpc(sendCallback, receiveCallback);

// Register callables which other clients can call remotly.
jsonRpc.Register("MyCallable", MyCallableMethod);

// Any callable can be unregistered at any point in time.
jsonRpc.Unregister("MyCallable");

// Send notifications to the remote endpoint (a notification does not expect to receive a response, therefore
// the call returns without result and without error checking - fire and forget).
jsonRpc.Notify("RemoteCallable");

// Perform an RPC call expecting a result.
// Call() waits for a response from the server. If the server responds with an error, an appropriate exception
// is thrown. The server is supposed to respond withing 1 sec, otherwise a timeout exception is thrown.
jsonRpc.Call("RemoteCallable");
```

## Todos
- More unit tests
- Add comments on public members

## Relevant Materials
The official JSON-RPC 2.0 specification: https://www.jsonrpc.org/specification
