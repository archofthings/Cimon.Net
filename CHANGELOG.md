# Changelog

## 2.0.0

### Fixed
- **Ethernet read requests were malformed**: the `Length` field of `ReadWordAsync`/`ReadBitAsync` frames was written as 1 byte instead of 2,
  shifting the whole data block by one byte. Read frames now use the same `ID | FrameNo | Cmd | Res | Length(2) | Data | CheckSum` layout as write frames.
- `TcpSocket` could not reconnect after being disconnected (a closed `Socket` was reused), so with `autoConnect` every request after the first one failed
  with `ObjectDisposedException`. A new socket is now created for each connection.
- Frame numbers did not wrap correctly: after 128 requests the frame number stayed `0` for the next 128 requests. It now cycles from 0 to 127.
- `SerialSocket.ReceiveData` returned a `null` task when a read was incomplete, which caused a `NullReferenceException` when awaited.
- Short, truncated or corrupted responses caused `IndexOutOfRangeException`; they now return `ResponseCode.WritingError`.
- Serial `WriteWordAsync` accepted up to 63 words although the 2-char length field can only describe 61; larger requests silently produced a wrong length.
- Opening an already open `SerialSocket` threw an exception.

### Changed
- Responses are returned as soon as a complete frame arrives, instead of always waiting for the full read timeout (1 s by default) on every request.
- Connections opened explicitly with `Connect()` are no longer closed after each request when `autoConnect` is enabled; only connections opened automatically are closed.
- Requests on one connector are serialized, so a connector can be shared between concurrent tasks.
- `TcpSocket` has a new optional `usePing` parameter to skip the ICMP ping before connecting.
- `PlcConnector`, `TcpSocket` and `SerialSocket` implement `IDisposable`.
- Addresses are sent in upper case.
- Targets `netstandard2.0` and `net8.0` (was `netcoreapp3.1`, which is out of support), so the package also works on .NET Framework.
- Removed the `Ardalis.GuardClauses` dependency.
- **Breaking:** the helper classes `Tools` and the guard extensions are now internal.
- **Breaking:** `PlcConnector` constructor is now `protected PlcConnector(IPlcSocket socket, bool autoConnect)` and the protected `_timeout` field was removed.
- Fixed `dotnet pack`, which referenced an icon on a local disk path.
- Replaced duplicate CodeQL workflows with a single updated one, and added a CI workflow that builds, tests and packs the library.

## 1.1.0
- Downgrade project to .NET Core 3.1.
