usbtemp-csharp
===

C# library for the [usbtemp.com USB thermometer](https://usbtemp.com/) and DS9097E compatible 1-wire adapters with one DS18B20 digital probe attached.
It is the C# counterpart of the [utmp-cli](https://github.com/usbtemp/utmp-cli) command line utility.

### Projects
| Folder | Contents |
|---|---|
| `usbtemp` | Class library `usbtemp.dll` with the `Thermometer` class (namespace `usbtemp`), source in `usbtemp/usbtemp.cs` |
| `usbtemp-demo` | Console application (.NET Framework 4.0) that reads the probe using the library |

The library targets `net40` (.NET Framework 4.0 and newer) and `netstandard2.0` (.NET Framework 4.6.1+, .NET Core, .NET 5 and newer on Windows, Linux and macOS).

### Thermometer class
```
void Open(string port);       // open serial port, e.g. "COM3" or "/dev/ttyUSB0"
void Close();                 // close serial port; safe to call if Open() failed
byte[] Rom();                 // probe serial number (8 bytes)
void Measure();               // start temperature conversion
float Acquire();              // read result of the last conversion, in degrees Celsius
float Temperature();          // Measure(), wait 800 ms, Acquire()
void SetPrecision(int bits);  // set resolution 9..12 bits, saved to probe EEPROM
int GetPrecision();           // current resolution, 9..12 bits
```
`Thermometer.MinPrecision` and `Thermometer.MaxPrecision` hold the allowed resolution range.

Use `Measure()` and `Acquire()` instead of `Temperature()` to do other work during the conversion or to wait less at lower resolution:

| Resolution | Step | Conversion time |
|---|---|---|
| 9 bits | 0.5 °C | 94 ms |
| 10 bits | 0.25 °C | 188 ms |
| 11 bits | 0.125 °C | 375 ms |
| 12 bits | 0.0625 °C | 750 ms |

Errors:

Probe and bus errors throw `ThermometerException`, with the same messages as the Node.js and Python libraries:

| Message | Cause |
|---|---|
| `Device is not connected` | `Open()` was not called or the port is closed |
| `Read timeout` | the adapter does not answer within 500 ms |
| `No device present` | no probe is connected |
| `Short circuit` | the 1-wire bus is shorted |
| `Presence error: 0x..` | unexpected response to the bus reset |
| `Invalid response` | a byte sent to the probe was not echoed back correctly |
| `CRC error` | the data read from the probe is corrupted |
| `Invalid device` | the probe is not a DS18B20 |

`SetPrecision()` throws `ArgumentOutOfRangeException` for a value outside 9..12.
`Open()` and writes throw the exceptions of `System.IO.Ports.SerialPort` (`IOException`, `UnauthorizedAccessException`, `TimeoutException`) when the serial port does not exist, is in use or cannot be accessed.

Example:
```csharp
var thermometer = new usbtemp.Thermometer();
try
{
    thermometer.Open("COM3");
    Console.WriteLine(BitConverter.ToString(thermometer.Rom()));
    Console.WriteLine($"{thermometer.Temperature():0.00} C");
}
finally
{
    thermometer.Close();
}
```

### Building
Open `usbtemp-sharp.sln` in Visual Studio 2017 or newer, or use the [.NET SDK](https://dotnet.microsoft.com/download) (Windows, Linux or macOS):
```
dotnet build                     # library and demo
dotnet pack usbtemp -c Release   # NuGet package of the library
```
The code is limited to C# 7.0, the default language version of Visual Studio 2017.

The demo is a .NET Framework 4.0 application: it runs on Windows, and on Linux or macOS with [Mono](https://www.mono-project.com/) (`mono usbtemp-demo.exe /dev/ttyUSB0`).

### Demo
```
usbtemp-demo <serial port>
```
Example output:
```
Serial: 28FF6A2B71160398
Precision: 12 bits
Temperature: 22.62 C
```
Exit code is 0 on success, 1 on a port or probe error and 2 on invalid arguments.
When started by double-click on Windows, it waits for a key press before closing the window.

On Linux the user needs read/write access to the serial port, usually by being a member of the `dialout` group.

### License
MIT, see [`LICENSE.md`](LICENSE.md).

This software is in alpha stage, contributions are welcome.
