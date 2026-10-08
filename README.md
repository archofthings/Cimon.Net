# Cimon.Net

[![NuGet](https://img.shields.io/nuget/v/Cimon.Net.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/Cimon.Net)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cimon.Net.svg?logo=nuget&label=Downloads)](https://www.nuget.org/packages/Cimon.Net)
[![CI](https://github.com/archofthings/Cimon.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/archofthings/Cimon.Net/actions/workflows/ci.yml)
[![CodeQL](https://github.com/archofthings/Cimon.Net/actions/workflows/codeql.yml/badge.svg)](https://github.com/archofthings/Cimon.Net/actions/workflows/codeql.yml)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A lightweight .NET library for communicating with **CIMON PLCs** over **Ethernet (TCP)** and **Serial (RS232C / RS485)**.
Read and write bits and words in PLC device memory with a simple async API.

```csharp
using var plc = new EthernetConnector(new TcpSocket("192.168.1.10"));
var (code, words) = await plc.ReadWordAsync(MemoryType.D, "000100", 10);
```

## Contents

- [About](#about)
- [Features](#features)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Connection handling](#connection-handling)
- [Addresses and memory types](#addresses-and-memory-types)
- [Response codes](#response-codes)
- [Limits per request](#limits-per-request)
- [Supported PLCs and platforms](#supported-plcs-and-platforms)
- [Build and test](#build-and-test)
- [Contributing](#contributing)
- [License](#license)

## About

CIMON PLCs are industrial controllers based on the IEC 61131 standard. There are many tools and servers to exchange
data with PLCs, but what if you want your own application to talk to them directly? I couldn't find any library for
CIMON PLCs, even though I used them in many projects, so I decided to publish mine.

## Features

- **Ethernet (TCP)** and **Serial (RS232C / RS485)** connectors with the same API
- Read and write **bits** and **words** in any device memory (`X`, `Y`, `M`, `L`, `K`, `F`, `T`, `C`, `S`, `D`)
- Fully **async**, and safe to share one connector between concurrent tasks
- Responses are validated (header, frame number, checksum and length) before data is returned
- Automatic or manual connection management
- Targets **.NET Standard 2.0** and **.NET 8**, so it runs on .NET Framework 4.6.1+ as well as modern .NET
- No third-party dependencies besides `System.IO.Ports`

## Installation

Install from [NuGet](https://www.nuget.org/packages/Cimon.Net) with the .NET CLI:

```shell
dotnet add package Cimon.Net
```

Or with the Package Manager Console in Visual Studio:

```powershell
Install-Package Cimon.Net
```

## Quick start

Create a connector for your connection type: `EthernetConnector` for Ethernet TCP, or `SerialConnector` for RS232C/RS485.

### Ethernet

```csharp
using CimonPlc.Enums;
using CimonPlc.PlcConnectors;
using CimonPlc.Sockets;

using var plc = new EthernetConnector(new TcpSocket("192.168.1.10", 10620));

// Read 10 bits from input memory X, starting at address 000001
var (responseCode, bits) = await plc.ReadBitAsync(MemoryType.X, "000001", 10);

if (responseCode == ResponseCode.Success)
    Console.WriteLine(string.Join(", ", bits));

// Write 3 words to data register D, starting at address 000100
var writeResult = await plc.WriteWordAsync(MemoryType.D, "000100", 1200, 35, 65535);
```

### Serial

```csharp
using var plc = new SerialConnector(new SerialSocket("COM3", 9600));

// Write 5 bits to output memory Y, starting at address 000010
var responseCode = await plc.WriteBitAsync(MemoryType.Y, "000010", 1, 1, 1, 0, 1);

// Read 4 words from data register D
var (code, words) = await plc.ReadWordAsync(MemoryType.D, "000200", 4);
```

### API overview

| Method | Returns | Description |
|--------|---------|-------------|
| `ReadWordAsync(memoryType, address, length)` | `(ResponseCode, int[])` | Reads `length` 16-bit words |
| `ReadBitAsync(memoryType, address, length)` | `(ResponseCode, byte[])` | Reads `length` bits, each value is `0` or `1` |
| `WriteWordAsync(memoryType, address, params int[] data)` | `ResponseCode` | Writes words, each value in `0`–`65535` |
| `WriteBitAsync(memoryType, address, params byte[] data)` | `ResponseCode` | Writes bits, each value `0` or `1` |
| `Connect(readTimeout, writeTimeout, pingTimeout)` | `ConnectionStatus` | Opens the connection (timeouts in ms, 100–10,000) |
| `Disconnect()` | `ConnectionStatus` | Closes the connection |

Invalid arguments (bad address format, out-of-range length or values) throw `ArgumentException`.
Communication problems are reported through the returned `ResponseCode`.

## Connection handling

With `autoConnect` enabled (the default), each read or write opens the connection if needed and closes it afterwards.
That's convenient for occasional requests. For frequent polling it's much faster to keep one connection open:

```csharp
using var plc = new EthernetConnector(new TcpSocket("192.168.1.10"));

var status = await plc.Connect(readTimeout: 1000, writeTimeout: 1000, pingTimeout: 3000);
if (status != ConnectionStatus.Connected)
    return;

while (!cancellationToken.IsCancellationRequested)
{
    var (code, words) = await plc.ReadWordAsync(MemoryType.D, "000100", 10);
    // ...
    await Task.Delay(500);
}

plc.Disconnect();
```

- A connection opened with `Connect()` stays open, even when `autoConnect` is enabled.
- With `autoConnect: false`, requests on a closed connection return `ResponseCode.SystemError`.
- Requests on one connector are sent one at a time, so a single connector can be shared between tasks.
- By default, `TcpSocket` pings the PLC before connecting and returns `ConnectionStatus.NoRouteToDestination` if there's no reply.
  If your network blocks ICMP, turn the ping off:

```csharp
var socket = new TcpSocket("192.168.1.10", 10620, usePing: false);
```

## Addresses and memory types

Addresses are hexadecimal strings of up to 6 characters. Shorter addresses are padded with leading zeros, so `"A0"` becomes `"0000A0"`.

- **Word** functions need a word address, so the last digit must be `0` (for example `"000010"`).
- **Bit** functions accept any bit address (for example `"0000A1"`).

| Memory | Name | Description |
|--------|------|-------------|
| `X` | Input | Inputs received directly from devices |
| `Y` | Output | Outputs written to devices |
| `M` | Internal memory | Internal relays used by the logic |
| `L` | Link | Data link with upper and lower devices |
| `K` | Keep | Like `M`, but retained when power is off |
| `F` | Internal flag | System state, clock contacts and flags |
| `T` | Timer | Timer values |
| `C` | Counter | Counter values |
| `S` | Step control | Step control relays |
| `D` | Data register | Internal data storage |

## Response codes

Every request returns a `ResponseCode`. Anything other than `Success` means the request failed.

| Code | Value | Meaning |
|------|-------|---------|
| `Success` | 0 | Request processed successfully |
| `SystemError` | 1 | System error, no connection or no response from the PLC |
| `InvalidDevicePrefix` | 2 | Invalid device prefix |
| `InvalidDeviceAddress` | 3 | Invalid device address |
| `UdpErrorReadDataSize` | 4 | Error in requested data size |
| `UdpErrorBlockSize` | 5 | More than 16 requested blocks |
| `BufferError` | 6 | Buffer memory reported an error in data or size |
| `OverBufferCapacity` | 7 | Receive buffer capacity exceeded |
| `OverSendingTime` | 8 | Sending time exceeded |
| `UdpInvalidHeader` | 9 | Invalid header |
| `ChecksumError` | 10 | Checksum error in the received data |
| `FrameSizeError` | 11 | Error in frame length |
| `UdpDataSizeError` | 12 | Error in the size of data to write |
| `UnknownBitValue` | 13 | Invalid bit value in write data |
| `UnknownCommand` | 14 | Unknown command |
| `WritingError` | 15 | Writing is disabled, or the response was invalid |
| `CpuError` | 16 | CPU processing error |

## Limits per request

| Function | Ethernet | Serial |
|----------|----------|--------|
| `ReadWordAsync` | 1–512 words | 1–63 words |
| `ReadBitAsync` | 1–1024 bits | 1–126 bits |
| `WriteWordAsync` | 1–64 words | 1–61 words |
| `WriteBitAsync` | 1–256 bits | 1–126 bits |

## Supported PLCs and platforms

- **PLCs:** the CIMON PLC range, including the `PLC-S`, `CP` and `XP` series
- **Frameworks:** .NET Standard 2.0 (.NET Framework 4.6.1+, .NET Core 2.0+, Mono) and .NET 8+

More documentation is in the [Wiki](https://github.com/archofthings/Cimon.Net/wiki).

## Build and test

You need the [.NET SDK](https://dotnet.microsoft.com/download) 10 or newer.

```shell
dotnet build
dotnet test
```

No PLC is needed. `EthernetConnector` tests talk over real TCP sockets to a PLC simulated with the
[Rony.Net](https://github.com/archofthings/Rony.Net) mock server, and `SerialConnector` tests use a fake serial socket.

The Ethernet tests are also a sample of testing a binary protocol client with Rony.Net:

- [`PlcMockServerTest`](https://github.com/archofthings/Cimon.Net/blob/main/tests/CimonPlc.UnitTests/Simulators/PlcMockServerTest.cs) uses `Rony.Net.Xunit` to give every test its own server on a free port, with its log in the test output.
- [`CimonFraming`](https://github.com/archofthings/Cimon.Net/blob/main/tests/CimonPlc.UnitTests/Simulators/CimonFraming.cs) is a custom `IMessageFraming` that splits the TCP stream into Cimon frames.
- [`CimonPlcSimulator`](https://github.com/archofthings/Cimon.Net/blob/main/tests/CimonPlc.UnitTests/Simulators/CimonPlcSimulator.cs) answers like a PLC and keeps its memory, so written values can be read back.
- [`EthernetFailureTests`](https://github.com/archofthings/Cimon.Net/blob/main/tests/CimonPlc.UnitTests/CimonPlc/EthernetFailureTests.cs) simulates slow, chunked, truncated, corrupted and dropped responses.

```csharp
Plc.Attach(Server);                                   // answer like a PLC
using var connector = CreateConnector();              // EthernetConnector for 127.0.0.1:Server.Port

await connector.WriteWordAsync(MemoryType.D, "100", 1, 2, 3);
var (code, words) = await connector.ReadWordAsync(MemoryType.D, "100", 3);   // [1, 2, 3]

Server.Should().HaveAcceptedConnections(Times.Exactly(2))   // autoConnect: one connection per request
    .And.HaveReceivedInOrder(
        r => r.Body[10] == (byte)WriteCommands.WordBlockWrite,
        r => r.Body[10] == (byte)ReadCommand.WordBlockRead);
```

## Contributing

Issues and pull requests are welcome. Please run `dotnet test` before opening a pull request, and add tests for new behavior.
See [CHANGELOG.md](CHANGELOG.md) for the release history.

## License

Cimon.Net is released under the [MIT License](LICENSE).
