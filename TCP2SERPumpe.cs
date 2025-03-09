using System.Net.Sockets;
using System.IO.Ports;

namespace serial_monitor
{
    internal enum ShortParity
    {
        None = 0,
        Odd = 1,
        Even = 2,
        Mark = 3,
        Space = 4,
    }

    internal class SerialPortData
    {
        public string PortName;
        public string BaudRate;
        public string DataBits;
        public string StopBits;
        public string Parity;
    }

    internal class TCP2SERPumpe : LogingBase
    {
        public TCP2SERPumpe(string ip, string port,
                            SerialPortData serportdata) : base(@"\logpumpe.txt")
        {
            this.ip = ip;
            this.port = port;
            this.serportdata = serportdata;
        }

        string ip;
        string port;
        SerialPortData serportdata;
        /*
          damit SerialPort klappt, muss 
           -- die System.IO.Port 8.x mit nuget hinzugefügt werden
           -- das bin inc. unterverzeichnisse kopiert werden
        */
        SerialPort serialPort = null;
        TcpClient tcpPort = null;
        NetworkStream tcpstream = null;

        int inbytes = 0;
        int outbytes = 0;

        public enum reason { none, serialdeath, tcpdeath };

        // signal: ich bin tod
        bool imdead = false;
        public bool ImDead { get { return imdead = false; } }
        // signal: todesursache
        private reason inquest = reason.none;
        public reason Inquest { get { return inquest; } }
        // signal: ich soll sterben
        private bool die = false;
        public bool Die { set { die = value; } }

        void serialPortDataRecieved(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] data = new byte[serialPort.BytesToRead];
            int dataread = serialPort.Read(data, 0, data.Length);
            inbytes += dataread;

            tcpstream.Write(data, 0, dataread);
        }
        void job()
        {
            logline("SP:job starts.");
            try
            {
                const int sleeptime = 10;
                int slept = 0;

                tcpstream = tcpPort.GetStream();

                // Worker-Thread-Schleife
                while ((!die) && (tcpPort.Connected))
                {
                    if (tcpstream.DataAvailable)
                    {
                        byte[] data = new byte[1000];
                        int dataread = tcpstream.Read(data, 0, data.Length);
                        outbytes += dataread;
                        serialPort.Write(data, 0, dataread);
                    }
                    else
                    {
                        Thread.Sleep(sleeptime); // this eats the wait time, so no load
                        slept += sleeptime;
                        // ist der serielle Port noch da?
                        if (slept >= 1000)
                        {
                            slept = 0;
                            // seems funny, but there is no reliable way to see a port disappear
                            // other than going deep into the hardware signaling rabithole
                            bool stillhasport = false;
                            foreach (string port in SerialPort.GetPortNames())
                            {
                                if (port.ToUpper() == serportdata.PortName)
                                {
                                    stillhasport = true;
                                    break;
                                }
                            }
                            if (!stillhasport)
                            {
                                inquest = reason.serialdeath;
                                die = true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logline("EP:" + e.ToString());
                return;
            }
            // Close
            tcpstream.Close();
            tcpPort.Close();
            // clean up.
            tcpstream = null;
            tcpPort = null;
            serialPort = null;

            logline($"SP:job ends. {inbytes} byte read, {outbytes} bytes written.");
            imdead = true;
        }
        bool OpenSerialPort()
        {
            try
            {
                serialPort = new SerialPort();
                serialPort.PortName = serportdata.PortName;
                serialPort.BaudRate = Convert.ToInt32(serportdata.BaudRate);
                serialPort.DataBits = Convert.ToInt32(serportdata.DataBits);
                serialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), serportdata.StopBits);
                serialPort.Parity = (Parity)Enum.Parse(typeof(ShortParity), serportdata.Parity);
                serialPort.Open(); // Open port.
                serialPort.DataReceived += new SerialDataReceivedEventHandler(serialPortDataRecieved);
            }
            catch (Exception e)
            {
                logline("EP:" + e.ToString());
                serialPort = null;
                return false;
            }
            serportdata.PortName = serportdata.PortName.ToUpper();
            return true;
        }
        bool OpenTcpPort()
        {
            try
            {
                tcpPort = new(ip, Int32.Parse(port));
            }
            catch (SocketException e)
            {
                logline($"EP: socket exception {e.SocketErrorCode} " + e.ToString());
                inquest = reason.tcpdeath;
                tcpPort = null;
                return false;
            }
            catch (Exception e)
            {
                logline("EP:" + e.ToString());
                tcpPort = null;
                return false;
            }

            return true;
        }

        public int start()
        {
            logline("SP:Start");
            if (!OpenSerialPort())
            {
                logline("SP:Failed to open serial port.");
                return 1;
            }
            if (!OpenTcpPort())
            {
                logline("SP:Failed to open tcp port.");
                return 2;
            }
            Thread worker = new(job);
            worker.Start();

            return 0;
        }
    }
}
