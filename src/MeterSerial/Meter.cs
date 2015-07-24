using System;
using System.IO.Ports;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace MeterSerial
{
    public class Meter
    {
        #region Nested Classes
        public class Response
        {
            public String Header = null;
            public String Payload = null;
            public bool ACK = false;

            public Response()
            {

            }
        }
        #endregion

        #region Constants
        private const String Password = "(00000000)"; 
        public class Commands
        {
            public const String DeviceID = "/?!";
            public const String ModeRead = "0:0";
            public const String ModeProgram = "0:1";
        }

        public enum Registers : uint
        {
            Voltage = 0,
            Current = 1,
            Frequency = 2,
            ActivePower = 3,
            ReactivePower = 4,
            TotalEnergy = 0x10,
            Temperature = 0x32,
            MeterID = 0x36,
            PassWord = 0x37
        }

        public class Header
        {
            public const String Password = "P1";    // header for password
            public const String Exit = "B0";
            public const String RequiresPassword = "P0";
            public const String ReadRegister = "R1";
        }

        public const byte SOH = 0x01; // start of header
        public const byte STX = 0x02; // StartofText 
        public const byte ETX = 0x03; // EndofText 
        public const byte ACK = 0x06; // Acknowledge 
        public static readonly byte[] CRLF = new byte[] {0x0d, 0x0a};

        #endregion

        #region Local fields

        String serialPort;
        SerialPort connection;

        String meterID = "unknown";



        #endregion

        #region Constructors

     
        public Meter(String serialPort)
        {
            this.serialPort = serialPort;

            Connect();
            InitMeter();
        }
        #endregion

        #region Properties
        public String MeterID
        {
            get
            {
                return this.meterID;
            }
        }
        #endregion

        #region Register Interpretation
        [MethodImpl(MethodImplOptions.Synchronized)]
        public float GetVoltage()
        {
            return ParseRegister((int)Registers.Voltage, 1);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public float GetCurrent()
        {
            return ParseRegister((int)Registers.Current, 1);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public float GetFrequency()
        {
            return ParseRegister((int)Registers.Frequency, 1);
        }

        /// <summary>
        /// Returns Power in Watts
        /// </summary>
        /// <returns>The power.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public float GetPower()
        {
            return ParseRegister((int)Registers.ActivePower, 2) * 1000;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public float GetVA()
        {
            return ParseRegister((int)Registers.ReactivePower, 2) * 1000;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public float GetConsumption()
        {
            return ParseRegister((int)Registers.TotalEnergy, 0);
        }


        protected float ParseRegister(UInt32 regNum, int decimalPlaces)
        {
            float result = float.Parse(ReadRegister(regNum));

            if (decimalPlaces > 0)
            {
                return result / (float)Math.Pow(10.0, (double)decimalPlaces);
            }
            else
            {
                return result;
            }
        }

        #endregion

        #region High level access
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Login()
        {
            SendCommand(Header.Password, Password);
            Response res = GetResponse();

            return res.ACK;
        }

        /// <summary>
        /// Tells the smart meter to deactivate the password. New login
        /// required.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Logout()
        {
            SendCommand(Header.Exit, null);

        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public String ReadRegister(UInt32 regNumber)
        {
            String number = regNumber.ToString("D8") + "()";
            SendCommand(Header.ReadRegister, number);

            Response res = GetResponse();

            // get the value that is stored between (...)
            Match match = Regex.Match(res.Payload, @"\(([^\)]*)\)");

            // Here we check the Match instance.
            if (match.Success)
            {
                // Finally, we get the Group value and display it.
                return match.Groups[1].Value;
            }
            else
            {
                return res.Payload;
            }

        }

        /// <summary>
        /// Sets read/write mode
        /// </summary>
        /// <returns><c>true</c>, if successful, <c>false</c> otherwise.</returns>
        /// <param name="program">If set to <c>true</c> enter programming mode (write), otherwise read.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool SetMode(bool program)
        {
            MemoryStream ms = new MemoryStream(4);
            ms.WriteByte(ACK);
            byte[] cmd = ToByteArray(program ? Commands.ModeProgram : Commands.ModeRead);
            ms.Write(cmd, 0, cmd.Length);

            Response res = ParseData(SendString(ms.ToArray()));

            // correct?
            return (res.Header != null) && (res.Header.Equals(Header.RequiresPassword));
        }


        /// <summary>
        /// Initialize the Smart Meter
        /// </summary>
        private void InitMeter()
        {
            meterID = GetDeviceID();
        }

        /// <summary>
        /// Returns the device ID of the Smart Meter
        /// </summary>
        /// <returns>The device ID (large number as string)</returns>
        String GetDeviceID()
        {
            return ToString(SendString(Commands.DeviceID));
        }
        #endregion

        #region Communication
        /// <summary>
        /// Connect to the smart meter
        /// </summary>
        private void Connect()
        {
            if (connection != null && connection.IsOpen)
            {
                connection.Close();
            }

            connection = new SerialPort(this.serialPort, 9600, Parity.Even, 7, StopBits.One);
            connection.Open();
            connection.ReadTimeout = 100;
        }

        /// <summary>
        /// Sends a command to the device
        /// </summary>
        /// <returns><c>true</c>, if command was sent, <c>false</c> otherwise.</returns>
        /// <param name="command">Command.</param>
        protected byte[] SendString(byte[] command)
        {
            connection.Write(command, 0, command.Length);
            connection.Write(CRLF, 0, CRLF.Length);

            return ReadData();
        }

        protected byte[] SendString(String command)
        {
            return SendString(ToByteArray(command));
        }

        protected void SendCommand(String hdr, String pld)
        {
            byte[] header = ToByteArray(hdr);
            byte[] payload = ToByteArray(pld);

            // bring it in the right format:
            // SOH .... STX .... ETX
            MemoryStream ms = new MemoryStream(100);
            ms.WriteByte(SOH);          // start of header
            ms.Write(header, 0, header.Length); // header
            if (payload != null)
            {
                ms.WriteByte(STX);          // start of text
                ms.Write(payload, 0, payload.Length);
            }
            ms.WriteByte(ETX);

            byte[] complete = ms.ToArray();

            // calculate checksum without SOH
            byte checkSum = CalculateChecksum(complete, 1, complete.Length);

            connection.Write(complete, 0, complete.Length);
            connection.Write(new byte[] {checkSum}, 0, 1);
        }

        /// <summary>
        /// Calculates the XOR checksum
        /// </summary>
        /// <returns>The checksum.</returns>
        /// <param name="input">Input.</param>
        public static byte CalculateChecksum(byte[] input, int offset, int length)
        {
            byte cc = 0;
            for (int i = offset; i < length; i++)
            {
                cc ^= input[i];
            }

            return cc;
               
        }

        public static byte CalculateChecksum(byte[] input)
        {
            return CalculateChecksum(input, 0, input.Length);
        }

        /// <summary>
        /// Reads data from the serial port and returns it as a string
        /// </summary>
        /// <returns>The data.</returns>
        byte[] ReadData()
        {
            byte tmpByte;
            byte[] buffer = new byte[1024];
            int length = 0;

            bool done = false;
            do
            {
                try
                {
                    tmpByte = (byte)connection.ReadByte();


                    buffer[length++] = tmpByte;

                }
                catch(TimeoutException)
                {
                    if (length > 0)
                    {
                        // no more data, apparently...
                        done = true;
                    }
                }

                
                
            } while (!done);

            Array.Resize(ref buffer, length);

            return buffer;
        }

        public Response ParseData(byte[] input)
        {
            Response res = new Response();

            //Console.WriteLine("Parsing data " + ToString(input));

            int pos = 0;
            while (pos < input.Length)
            {
                switch (input[pos])
                {
                    case SOH:
                        res.Header = "";
                        pos = ParseStringFromResponse(ref res.Header, input, pos + 1);
                        break;
                    case STX:
                        res.Payload = "";
                        pos = ParseStringFromResponse(ref res.Payload, input, pos + 1);
                        break;
                    case ETX:
                        pos++;
                        if (pos != input.Length - 1)
                        {
                            throw new InvalidDataException("Length of response wrong. Expected " + (pos + 1) + " but got " + input.Length);
                        }

                        byte cc = input[pos];
                        // calculate the checksum
                        // First, check for the header (SOH), so that we can ignore the SOH
                        byte check = CalculateChecksum(input, 1, pos);
                        if (cc != check)
                        {
                            Console.WriteLine("Error parsing: " + ToString(input));
                            throw new InvalidDataException("The checksum does not match! Expected " + cc + " but calculated " + check);
                        }
                        pos++;  // skip checksum
                        break;  // end of text
                    case ACK:
                        if (pos != 0)
                        {
                            // ACK is supposed to be at the first position!
                            throw new InvalidDataException("ACK received. But not at first position!");
                        }
                        pos++;
                        res.ACK = true;
                        break;  // ACK
                    default:
                        throw new InvalidDataException("Unknown control character encountered: " + (char)input[pos]);

                }
            }

            return res;
        }

        /// <summary>
        /// Reads the byte array starting from <c>offset</c> until the end
        /// of the array is reached or a new control character is ancountered
        /// </summary>
        /// <returns>The new offset</returns>
        /// <param name="target">Target.</param>
        /// <param name="response">Response.</param>
        /// <param name="offset">Offset.</param>
        private int ParseStringFromResponse(ref String target, byte[] response, int offset)
        {
            int length = 0;
            for (int i = offset; i < response.Length; i++)
            {
                byte curr = response[i];
                if (curr < 0x10)
                {
                    // terminate
                    break;
                }
                target += (char)curr;
                length++;
            }

            return offset + length;
        }

        protected Response GetResponse()
        {
            return ParseData(ReadData());
        }

        public static String ToString(byte[] rawdata)
        {
            String tmp = "";
            for (int i = 0; i < rawdata.Length; i++)
            {
                tmp += (char)rawdata[i];
            }

            return tmp;
        }

        public static byte[] ToByteArray(String str)
        {
            if (str != null)
            {
                byte[] buffer = new byte[str.Length];
                for (int i = 0; i < str.Length; i++)
                {
                    buffer[i] = (byte)str[i];
                }

                return buffer;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Disconnects the serial line
        /// </summary>
        public void Disconnect()
        {
            connection.Close();
        }

        #endregion
    }
}

