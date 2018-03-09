using System;

namespace Demo
{
    class Program
    {

        static void Main(string[] args)
        {
            byte[] rom;
            float temperature;
            usbtemp.Thermometer thermometer;
            string hex;

            if (args.Length == 0)
            {
                Console.WriteLine("Please specify serial port!");
                goto exit;
            }

            thermometer = new usbtemp.Thermometer();

            try
            {
                thermometer.Open(args[0]);
            }
            catch (System.IO.IOException e)
            {
                Console.WriteLine(e.Message);
                goto exit;
            }

            try
            {
                rom = thermometer.Rom();
                temperature = thermometer.Temperature();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                goto exit;
            }
            finally
            {
                thermometer.Close();
            }

            hex = BitConverter.ToString(rom).Replace("-", string.Empty);
            Console.WriteLine($"Serial: {hex}\nTemperature: {temperature:0.00}");

            exit:
            Console.ReadKey();
        }
    }
}
