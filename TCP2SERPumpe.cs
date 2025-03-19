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
        public string PortName = "";
        public string BaudRate = "";
        public string DataBits = "";
        public string StopBits = "";
        public string Parity = "";
        public string Rts = "";
        public string Dtr = "";
    }

    internal class TCP2SERPumpe : LogingBase
    {
        public TCP2SERPumpe(string ip, string port,
                            SerialPortData serportdata) : base(@"logpumpe.txt")
        {
            this.ip = ip;
            this.port = port;
            this.serportdata = serportdata;
        }

        const int serprotocolmax = 5 * 1024;
        string serprotocol = "";
        string ip;
        string port;
        SerialPortData serportdata;
        ExceptionScanner.ExceptionScanner exceptionscanner = new();
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

        public enum Reason { none, serialdeath, tcpdeath, serialfail, tcpfail };

        // signal: ich bin tod
        bool imdead = false;
        public bool ImDead { get { return imdead; } }
        // signal: todesursache
        private Reason inquest = Reason.none;
        public Reason Inquest { get { return inquest; } }
        // signal: ich soll sterben
        private bool die = false;
        public bool Die { set { die = value; } }
        // Überwachungszähler USB-Port
        int slept = 0;

        void serialPortDataRecieved(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] data = new byte[serialPort.BytesToRead];
            int dataread = serialPort.Read(data, 0, data.Length);
            inbytes += dataread;
            // gerade was empfangen, also nix zu überwachen
            slept = 0;

            tcpstream.Write(data, 0, dataread);

            // merken
            if (serprotocol.Length > serprotocolmax)
            {
                serprotocol = serprotocol.Remove(0, serprotocol.Length - serprotocolmax);
            }
            serprotocol += System.Text.Encoding.Default.GetString(data);

            // Wenn Exception gefunden, dann den Mitschnitt einfach löschen
            string exceptiontext = exceptionscanner.checkAndSeparateExceptionText(serprotocol);
            if (exceptiontext != "")
            {
                File.WriteAllText(exceptionscanner.ExceptionFilename, exceptiontext);
                serprotocol = "";
                logline("SP:exception detected.");
            }
        }
        void job()
        {
            inbytes = 0;
            outbytes = 0;
            logline("SP:job starts.");
            try
            {
                const int sleeptime = 10;

                tcpstream = tcpPort.GetStream();

                // now we can 
                serialPort.DataReceived += new SerialDataReceivedEventHandler(serialPortDataRecieved);

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
                            // other than going deep into the hardware signaling rabbithole
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
                                inquest = Reason.serialdeath;
                                die = true;
                            }
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                logline("EP:" + e.ToString());
                inquest = Reason.tcpdeath;
            }
            catch (Exception e)
            {
                logline("EP:" + e.ToString());
            }
            // Close
            tcpstream.Close();
            tcpPort.Close();
            serialPort.Close();
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
                serialPort.DtrEnable = serportdata.Dtr.ToLower() != "off";
                serialPort.RtsEnable = serportdata.Dtr.ToLower() != "off";
                serialPort.Open(); // Open port.
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

        public bool start()
        {
            logline("SP:Start");
            if (!OpenSerialPort())
            {
                logline("SP:Failed to open serial port.");
                inquest = Reason.serialfail;
                return false;
            }
            if (!OpenTcpPort())
            {
                logline("SP:Failed to open tcp port.");
                inquest = Reason.tcpfail;
                return false;
            }
            Thread worker = new(job);
            worker.Start();

            return true;
        }
    }
}
