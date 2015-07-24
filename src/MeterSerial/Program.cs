using System;

namespace MeterSerial
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            Meter smartMeter = new Meter("/dev/ttyUSB0");
            Console.WriteLine("Connected to Meter " + smartMeter.MeterID);

            Console.WriteLine("Asking for programming mode...");
            smartMeter.SetMode(true);
            Console.WriteLine("Sending password");
            if (smartMeter.Login())
            {
                Console.WriteLine("Login successfull");
            }
            else
            {
                Console.WriteLine("Smart Meter did not accept password.");
            }

            while (true)
            {
                Console.Clear();

                Console.WriteLine("Voltage  : " + smartMeter.GetVoltage() + " V");
                Console.WriteLine("Frequency: " + smartMeter.GetFrequency() + " Hz");
                Console.WriteLine("Current  : " + smartMeter.GetCurrent() + " A");
                Console.WriteLine("Power    : " + smartMeter.GetPower() + " W");

                System.Threading.Thread.Sleep(1000);
            }

            smartMeter.Logout();

            /*
            String cmd;
            do
            {
                cmd = Console.ReadLine();
                byte[] toSend = Meter.ToByteArray(cmd);

                for(int i = 0; i < toSend.Length; i++)
                {
                    switch(toSend[i])
                    {
                        case (byte)'#':
                            // ACK
                            toSend[i] = Meter.ACK;
                            break;
                    }
                }

                Console.WriteLine("Sending " + Meter.ToString(toSend));

                byte[] buffer = smartMeter.SendString(toSend);

                Console.WriteLine("Response: " + Meter.ToString(buffer));


                for(int i = 0; i < buffer.Length; i++)
                {
                    Console.Write((char) buffer[i] + "  ");
                }
                Console.WriteLine();
                for(int i = 0; i < buffer.Length; i++)
                {
                    Console.Write("{0:x2} ", buffer[i]);
                }
                Console.WriteLine();

            } while (cmd != "exit");
            */

            smartMeter.Disconnect();
        }
    }
}
