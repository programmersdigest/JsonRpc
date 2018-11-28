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
TODO

## Todos
- More unit tests
- Add comments on public members

## Relevant Materials
The official JSON-RPC 2.0 specification: https://www.jsonrpc.org/specification
