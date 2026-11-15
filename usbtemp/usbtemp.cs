using System;
using System.IO.Ports;

namespace usbtemp
{
    public class ThermometerException : Exception
    {
        public ThermometerException(string message) : base(message)
        {
        }
    }

    public class Thermometer
    {
        const int ok = 0;
        const int fail = -1;

        const byte generator = 0x8c;
        const int rom_size = 8;
        const int sp_size = 9;
        const int sp_th = 2;
        const int sp_tl = 3;
        const int sp_config = 4;

        // DS18B20 commands.
        const byte cmd_read_rom = 0x33;
        const byte cmd_skip_rom = 0xcc;
        const byte cmd_convert_t = 0x44;
        const byte cmd_read_scratchpad = 0xbe;
        const byte cmd_write_scratchpad = 0x4e;
        const byte cmd_copy_scratchpad = 0x48;

        // Configuration register: bits 6:5 hold the resolution R1:R0, the other bits are fixed.
        const byte config_mask = 0x9f;
        const byte config_fixed = 0x1f;

        // 1-wire over UART: the reset pulse is sent at a low speed, data bits at a high one.
        const int reset_baud = 9600;
        const int reset_data_bits = 8;
        const int data_baud = 115200;
        const int data_data_bits = 6;

        public const int MinPrecision = 9;
        public const int MaxPrecision = 12;

        private SerialPort mySerialPort;

        protected byte LsbCrc8(byte[] buf, int buflen, byte generator)
        {
            int i, bit_counter;
            byte crc = 0;

            for (i = 0; i < buflen; i++)
            {
                crc ^= buf[i];
                bit_counter = 8;
                do
                {
                    crc = (crc & 0x01) == 0x01 ? (byte)(((crc >> 1) & 0x7f) ^ generator) : (byte)((crc >> 1) & 0x7f);
                    bit_counter--;
                } while (bit_counter > 0);
            }
            return crc;
        }

        void SetSpeed(int baud, int dataBits)
        {
            mySerialPort.BaudRate = baud;
            mySerialPort.DataBits = dataBits;
        }

        static byte PrecisionToConfig(int precision)
        {
            return (byte)(config_fixed | ((precision - MinPrecision) << 5));
        }

        static int ConfigToPrecision(byte config)
        {
            return ((config >> 5) & 0x03) + MinPrecision;
        }

        protected int OwReset()
        {
            int rv;
            byte wbuff;
            byte rbuff;
            byte[] buf;

            SetSpeed(reset_baud, reset_data_bits);

            buf = new byte[1];

            try
            {
                wbuff = 0xf0;
                buf[0] = wbuff;
                mySerialPort.Write(buf, 0, 1);

                // No echo means nothing answered the reset pulse.
                if (mySerialPort.Read(buf, 0, 1) != 1)
                {
                    rv = fail;
                }
                else
                {
                    rbuff = buf[0];
                    rv = (rbuff == 0x00 || rbuff == 0xf0) ? fail : ok;
                }
            }
            catch (TimeoutException)
            {
                rv = fail;
            }
            finally
            {
                // Always return to data speed, even if the reset failed.
                SetSpeed(data_baud, data_data_bits);
            }

            return rv;
        }

        protected byte OwWriteByte(byte wbuff)
        {
            int rbytes, remaining, i;
            byte[] buf;
            byte rbuff;

            buf = new byte[8];
            for (i = 0; i < 8; i++)
            {
                buf[i] = Convert.ToBoolean(wbuff & (1 << (i & 0x7))) ? (byte)0xff : (byte)0x00;
            }
            mySerialPort.Write(buf, 0, 8);

            rbuff = 0;
            remaining = 8;
            while (remaining > 0)
            {
                rbytes = mySerialPort.Read(buf, 0, remaining);
                for (i = 0; i < rbytes; i++)
                {
                    rbuff >>= 1;
                    rbuff |= (buf[i] & 0x01) == 0x01 ? (byte)0x80 : (byte)0x00;
                    remaining--;
                }
            }
            return rbuff;
        }

        protected byte OwRead()
        {
            return OwWriteByte(0xff);
        }

        protected int OwWrite(byte wbuff)
        {
            if (OwWriteByte(wbuff) != wbuff)
            {
                return fail;
            }
            return ok;
        }

        // Reset the bus and send the command bytes; throw with the given message if any step fails.
        protected void Command(string error, params byte[] bytes)
        {
            if (OwReset() == fail)
            {
                throw new ThermometerException(error);
            }
            foreach (byte b in bytes)
            {
                if (OwWrite(b) == fail)
                {
                    throw new ThermometerException(error);
                }
            }
        }

        protected byte[] ReadBytes(int count)
        {
            byte[] buf = new byte[count];
            int i;

            for (i = 0; i < count; i++)
            {
                buf[i] = OwRead();
            }
            return buf;
        }

        // The last byte of ROM and scratchpad is the CRC of the bytes before it.
        protected void CheckCrc(byte[] buf)
        {
            if (buf[buf.Length - 1] != LsbCrc8(buf, buf.Length - 1, generator))
            {
                throw new ThermometerException("CRC Error!");
            }
        }

        public void Open(string name)
        {
            mySerialPort = new SerialPort(name)
            {
                BaudRate = data_baud,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DataBits = data_data_bits,
                Handshake = Handshake.None,
                RtsEnable = false,

                ReadTimeout = 500,
                WriteTimeout = 500
            };

            mySerialPort.Open();
        }

        public void Close()
        {
            // Safe to call when Open() failed or was never called.
            if (mySerialPort != null)
            {
                mySerialPort.Close();
                mySerialPort = null;
            }
        }

        public byte[] Rom()
        {
            byte[] rom;

            Command("Cannot read ROM!", cmd_read_rom);

            rom = ReadBytes(rom_size);
            CheckCrc(rom);

            return rom;
        }

        public void Measure()
        {
            Command("Cannot start the conversion process!", cmd_skip_rom, cmd_convert_t);
        }

        public float Acquire()
        {
            byte[] sp_sensor;
            short T;

            sp_sensor = ReadScratchpad("Cannot read the temperature!");

            T = BitConverter.ToInt16(sp_sensor, 0);
            return (float)T / 16;
        }

        public float Temperature()
        {
            Measure();
            System.Threading.Thread.Sleep(800);
            return Acquire();
        }

        public void SetPrecision(int precision)
        {
            byte cfg_new;
            byte[] sp_sensor;

            if (precision < MinPrecision || precision > MaxPrecision)
            {
                throw new ArgumentOutOfRangeException(nameof(precision), precision,
                    $"Precision must be between {MinPrecision} and {MaxPrecision} bits.");
            }

            cfg_new = PrecisionToConfig(precision);

            sp_sensor = ReadScratchpad("Cannot read the configuration!");
            if (sp_sensor[sp_config] == cfg_new)
            {
                return;
            }

            // Write Scratchpad: TH, TL and config (TH/TL kept as they are).
            Command("Cannot write the configuration!", cmd_skip_rom, cmd_write_scratchpad,
                sp_sensor[sp_th], sp_sensor[sp_tl], cfg_new);

            // Copy Scratchpad to EEPROM so the setting survives power loss.
            Command("Cannot save the configuration!", cmd_skip_rom, cmd_copy_scratchpad);
        }

        public int GetPrecision()
        {
            byte[] sp_sensor;

            sp_sensor = ReadScratchpad("Cannot read the configuration!");

            return ConfigToPrecision(sp_sensor[sp_config]);
        }

        protected byte[] ReadScratchpad(string error)
        {
            byte[] sp_sensor;

            Command(error, cmd_skip_rom, cmd_read_scratchpad);

            sp_sensor = ReadBytes(sp_size);

            if ((sp_sensor[sp_config] & config_mask) != config_fixed)
            {
                throw new ThermometerException("Invalid device!");
            }
            CheckCrc(sp_sensor);

            return sp_sensor;
        }
    }
}
