[![NuGet version](https://badge.fury.io/nu/Cimon.Net.svg)](https://badge.fury.io/nu/Cimon.Net)

# Cimon.Net
A .NET Library for Cimon PLCs Connectivity

<!-- ABOUT THE PROJECT -->
## About The Project
<p>CIMON-PLC is an industrial control device based on international standards of IEC61131. There are many tools and servers to work with PLCs and send/recieve data with them, but what if we want to use our application to communicate? I Couldn't find any library to work with Cimon PLCs, and actually I used them in many projects. So I decided to publish my library.</p>

## Getting Started
### 1. Installing Cimon.Net
You can install Cimon.Net with [NuGet Package Manager Console](https://www.nuget.org/packages/Cimon.Net):

    Install-Package Cimon.Net
    
Or via the .NET Core command-line interface:

    dotnet add package Cimon.Net
    
Either commands, from Package Manager Console or .NET Core CLI, will download and install **Cimon.Net** and all required dependencies.

### 2. Defining your Connector
Create a Connector based on your connection type. You can choose between `EthernetConnector` to support Ethernet TCP connection or `SerialConnector` to support RS232C/RS485 serial interfaces.

`EthernetConnector` usage sample for reading 10 bits from Input device memory `X` address 000001:

```csharp
using var plc = new EthernetConnector(new TcpSocket("192.168.1.10", 10620));
var (responseCode, data) = await plc.ReadBitAsync(MemoryType.X, "000001", 10);
```

`SerialConnector` usage sample for writing 5 bits to Output device memory `Y` address 000010:

```csharp
using var plc = new SerialConnector(new SerialSocket("COM3", 9600));
var responseCode = await plc.WriteBitAsync(MemoryType.Y, "000010", 1, 1, 1, 0, 1);
```

Always check the returned `ResponseCode`: `ResponseCode.Success` means the PLC accepted the request.

### 3. Connection handling
With `autoConnect` enabled (the default), every read/write opens the connection if it is not open and closes it again when done.
For frequent polling it is much cheaper to keep one connection open:

```csharp
using var plc = new EthernetConnector(new TcpSocket("192.168.1.10"));
await plc.Connect(readTimeout: 1000, writeTimeout: 1000, pingTimeout: 3000);

while (running)
{
    var (code, words) = await plc.ReadWordAsync(MemoryType.D, "000100", 10);
    // ...
}

plc.Disconnect();
```

Connections opened explicitly with `Connect()` are kept open, even when `autoConnect` is enabled.
Requests sent through one connector are serialized, so a connector can safely be shared between tasks.

By default `TcpSocket` pings the PLC before connecting. If your network blocks ICMP, disable it:

```csharp
var socket = new TcpSocket("192.168.1.10", 10620, usePing: false);
```

### Limits per request

| Function        | Ethernet     | Serial       |
|-----------------|--------------|--------------|
| `ReadWordAsync` | 1-512 words  | 1-63 words   |
| `ReadBitAsync`  | 1-1024 bits  | 1-126 bits   |
| `WriteWordAsync`| 1-64 words   | 1-61 words   |
| `WriteBitAsync` | 1-256 bits   | 1-126 bits   |

## Documentation
Check the Wiki and feel free to edit it: https://github.com/MojtabaKiani/Cimon.Net/wiki

## Supported PLCs
Complete range of Cimon PLC products including `PLC-S`, `CP`, `XP` series

## Supported platforms
The package targets `netstandard2.0` and `net8.0`, so it works on .NET Framework 4.6.1+, .NET Core 2.0+ and all modern .NET versions.

## Build and test
You need the [.NET SDK](https://dotnet.microsoft.com/download) 10 or newer.

    dotnet build
    dotnet test

I used my library [Rony.Net](https://github.com/MojtabaKiani/Rony.Net) in unit tests for `EthernetConnector`. But for `SerialConnector`
I used no device or library and it only works with a fake socket.

## Changes
See [CHANGELOG.md](CHANGELOG.md).
