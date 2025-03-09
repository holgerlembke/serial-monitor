using System.ComponentModel.Design.Serialization;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

namespace serial_monitor
{
    internal class SimpleLogger
    {
        string logfilename;
        string path = "";

        public SimpleLogger(string logfilename)
        {
            path = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            this.logfilename = logfilename;
            // logline("SL:Start");
        }

        void logit(string line)
        {
            try
            {
                File.AppendAllText(path + logfilename, line);
            }
            catch // could fail because file is in editor. retry. dirty. evil. don't do it.
            {
                Thread.Sleep(500);
                File.AppendAllText(path + logfilename, line);
            }
        }

        protected void logline(string line)
        {
            if (!line.EndsWith('\n'))
            {
                line += '\n';
            }
            line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + line;
            logit(line);
        }
    }

    public enum ShortParity
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

    internal class TCP2SERPumpe : SimpleLogger
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
                            // seems funny, but there is no reliable way to see a port disappear...
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

    internal class SerialMonitor : SimpleLogger
    {
        public SerialMonitor() : base(@"\logmonitor.txt") { }

        string helloanswer = """
        {
          "eventType": "hello",
          "message": "OK",
          "protocolVersion": 1
        }

        """;

        string describeanswer = """
            {
              "eventType": "describe",
              "message": "OK",
              "port_description": {
                "protocol": "serial",
                "configuration_parameters": {
                  "baudrate": {
                    "label": "Baudrate",
                    "type": "enum",
                    "value": [
                      "300",
                      "600",
                      "750",
                      "1200",
                      "2400",
                      "4800",
                      "9600",
                      "19200",
                      "38400",
                      "57600",
                      "115200",
                      "230400",
                      "460800",
                      "500000",
                      "921600",
                      "1000000",
                      "2000000"
                    ],
                    "selected": "9600"
                  },
                  "bits": {
                    "label": "Data bits",
                    "type": "enum",
                    "value": [
                      "5",
                      "6",
                      "7",
                      "8",
                      "9"
                    ],
                    "selected": "8"
                  },
                  "parity": {
                    "label": "Parity",
                    "type": "enum",
                    "value": [
                      "None",
                      "Even",
                      "Odd",
                      "Mark",
                      "Space"
                    ],
                    "selected": "None"
                  },
                  "stop_bits": {
                    "label": "Stop bits",
                    "type": "enum",
                    "value": [
                      "1",
                      "1.5",
                      "2"
                    ],
                    "selected": "1"
                  }
                }
              }
            }

            """;

        string configureanswer = """
            {
              "eventType": "configure",
              "message": "OK"
            }

            """;

        string openanswer = """
            {
              "eventType": "open",
              "message": "OK"
            }

            """;

        // %pp% ist seriel portname
        string openanswerunknownserialport = """
            {
              "eventType": "open",
              "error": true,
              "message": "unknown port %pp%"
            }

            """;

        string openanswerserialportgone = """
            {
              "eventType": "port_closed",
              "message": "serial port disappeared!",
              "error": true
            }

            """;

        string openanswertcpgone = """
            {
              "eventType": "port_closed",
              "message": "lost TCP/IP connection with the client!",
              "error": true
            }

            """;

        string closeanswer = """
            {
              "eventType": "close",
              "message": "OK"
            }

            """;

        string quitanswer = """
            {
              "eventType": "quit",
              "message": "OK"
            }

            """;

        // %pp% is unknown command
        string Idontkknowwhatanswer = """
            {
              "eventType": "command_error",
              "error": true,
              "message": "Command %pp% not supported"
            }

            """;

        public void job()
        {
            SerialPortData serialportdata = null;
            TCP2SERPumpe pumpe = null;

            try
            {
                using (Stream stdin = Console.OpenStandardInput())
                {
                    using (Stream stdout = Console.OpenStandardOutput())
                    {
                        byte[] buffer = new byte[1000];
                        string buffers = "";
                        do
                        {
                            int bytes = stdin.Read(buffer, 0, buffer.Length);
                            if ((bytes > 0) || (buffers != ""))
                            {
                                buffers += Encoding.UTF8.GetString(buffer, 0, bytes);

                                int lineend = buffers.IndexOf('\n');
                                if (lineend > -1)
                                {
                                    string msg = buffers.Substring(0, lineend);
                                    buffers = buffers.Remove(0, lineend + 1);

                                    logline(@">>" + msg);

                                    if (msg.StartsWith("HELLO "))
                                    {
                                        byte[] answer = Encoding.ASCII.GetBytes(helloanswer);
                                        stdout.Write(answer, 0, answer.Length);
                                    }
                                    else
                                    if (msg.StartsWith("DESCRIBE"))
                                    {
                                        byte[] answer = Encoding.ASCII.GetBytes(describeanswer);
                                        stdout.Write(answer, 0, answer.Length);
                                    }
                                    else
                                    if (msg.StartsWith("CONFIGURE "))
                                    {
                                        if (serialportdata == null)
                                        {
                                            serialportdata = new();
                                        }
                                        // config
                                        if (msg.StartsWith("CONFIGURE bits "))
                                        {
                                            serialportdata.DataBits = msg.Substring(15);
                                        }
                                        else
                                        if (msg.StartsWith("CONFIGURE parity "))
                                        {
                                            serialportdata.Parity = msg.Substring(17);
                                        }
                                        else
                                        if (msg.StartsWith("CONFIGURE stop_bits "))
                                        {
                                            serialportdata.StopBits = msg.Substring(20);
                                        }
                                        else
                                        if (msg.StartsWith("CONFIGURE baudrate "))
                                        {
                                            serialportdata.BaudRate = msg.Substring(19);
                                        }
                                        else
                                        {
                                            // ignorieren
                                            logline("RR:" + msg);
                                        }

                                        byte[] answer = Encoding.ASCII.GetBytes(configureanswer);
                                        stdout.Write(answer, 0, answer.Length);
                                    }
                                    else
                                    if (msg.StartsWith("OPEN ")) // OPEN 127.0.0.1:27676 COM4
                                    {
                                        string[] cmd = msg.Split(" ");
                                        string[] tcps = cmd[1].Split(":");
                                        serialportdata.PortName = cmd[2];

                                        /** /
                                        logline("SI:" + tcps[0]);
                                        logline("SI:" + tcps[1]);
                                        logline("SP:" + serialportdata.PortName);
                                        logline("SP:" + serialportdata.BaudRate);
                                        logline("SP:" + serialportdata.DataBits);
                                        logline("SP:" + serialportdata.Parity);
                                        logline("SP:" + serialportdata.StopBits);
                                        /**/

                                        if ((pumpe == null) && (serialportdata != null))
                                        {
                                            pumpe = new(tcps[0], tcps[1], serialportdata);
                                            logline("RR:pumpe starts");
                                            int res = pumpe.start();
                                            if (res == 0)
                                            {
                                                logline("RR:pumpe runs");

                                                byte[] answer = Encoding.ASCII.GetBytes(openanswer);
                                                stdout.Write(answer, 0, answer.Length);
                                            }
                                            else
                                            {
                                                // Port nicht auf
                                                if (res == 1)
                                                {
                                                    byte[] answer = Encoding.ASCII.GetBytes(openanswerunknownserialport.Replace("%pp%", cmd[2]));
                                                    stdout.Write(answer, 0, answer.Length);
                                                }
                                                else
                                                if (res == 2)
                                                {
                                                    byte[] answer = Encoding.ASCII.GetBytes(openanswerunknownserialport.Replace("%pp%", cmd[1]));
                                                    stdout.Write(answer, 0, answer.Length);
                                                }

                                                logline("RR:error " + res);
                                            }
                                        }
                                        else
                                        {
                                            if (pumpe != null)
                                            {
                                                logline("!!:pumpe already exists");
                                            }
                                            if (serialportdata == null)
                                            {
                                                logline("!!:no serial port config data");
                                            }
                                        }
                                    }
                                    else
                                    if (msg.StartsWith("CLOSE"))
                                    {
                                        if (pumpe != null)
                                        {
                                            pumpe.Die = true;
                                            do
                                            {
                                                Thread.Sleep(100);
                                            } while (!pumpe.ImDead);
                                            pumpe = null;
                                        }
                                        byte[] answer = Encoding.ASCII.GetBytes(closeanswer);
                                        stdout.Write(answer, 0, answer.Length);

                                        logline("RR:pump is dead");
                                    }
                                    else
                                    if (msg.StartsWith("QUIT"))
                                    {
                                        byte[] answer = Encoding.ASCII.GetBytes(quitanswer);
                                        stdout.Write(answer, 0, answer.Length);

                                        logline("Quit");
                                        Environment.Exit(0); // and dead
                                    }
                                    else
                                    {
                                        logline("??" + msg);
                                    }
                                }
                            }
                        } while (true);
                    }
                }
            }
            catch (Exception e)
            {
                logline("EM:" + e.ToString());
                // eat it.
            }

            logline("RR:End");
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            SerialMonitor sm = new();
            sm.job();
        }
    }
}
