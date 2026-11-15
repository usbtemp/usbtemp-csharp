using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Demo
{
    class Program
    {
        // Exit codes, as in utmp-cli.
        const int RC_OK = 0;
        const int RC_ERROR = 1;
        const int RC_USAGE = 2;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        static int Main(string[] args)
        {
            int rv = Run(args);

            // Started by double-click in Windows Explorer: keep the window open so the output can be read.
            if (OwnsConsoleWindow())
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }

            return rv;
        }

        static int UsageError(string message)
        {
            Console.Error.WriteLine(message);
            Console.WriteLine("Usage: usbtemp-demo <serial port>");
            return RC_USAGE;
        }

        static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                return UsageError("Please specify serial port!");
            }
            if (args.Length > 1)
            {
                return UsageError("Too many arguments!");
            }

            return Read(args[0]);
        }

        static int Read(string port)
        {
            usbtemp.Thermometer thermometer = new usbtemp.Thermometer();
            byte[] rom;
            float temperature;
            int precision;

            try
            {
                thermometer.Open(port);
                rom = thermometer.Rom();
                precision = thermometer.GetPrecision();
                temperature = thermometer.Temperature();
            }
            catch (Exception e)
            {
                // Port problems come as SerialPort's own exceptions, sensor problems as ThermometerException.
                Console.Error.WriteLine(e.Message);
                return RC_ERROR;
            }
            finally
            {
                thermometer.Close();
            }

            Console.WriteLine($"Serial: {BitConverter.ToString(rom).Replace("-", string.Empty)}");
            Console.WriteLine($"Precision: {precision} bits");
            Console.WriteLine($"Temperature: {temperature:0.00} C");

            return RC_OK;
        }

        // True when this program is the only process using its console window, which happens when it
        // is started from Explorer: Windows then closes the window as soon as the program exits.
        // From cmd or PowerShell the shell shares the window, so the count is 2 or more.
        static bool OwnsConsoleWindow()
        {
            if (Path.DirectorySeparatorChar != '\\')
            {
                return false;
            }
            try
            {
                uint[] list = new uint[2];
                return GetConsoleProcessList(list, (uint)list.Length) == 1;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
