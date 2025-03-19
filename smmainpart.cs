using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace serial_monitor
{
    internal partial class SerialMonitor : LogingBase
    {
        public SerialMonitor() : base(@"logmonitor.txt") { }

        SerialPortData serialportdata = null;
        TCP2SERPumpe pumpe = null;
        Stream stdin = null;
        Stream stdout = null;
        int pid;

        void bufferhandler(ref string buffers)
        {
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
                    if (msg.StartsWith("CONFIGURE dtr "))
                    {
                        serialportdata.Dtr = msg.Substring(14);
                    }
                    else
                    if (msg.StartsWith("CONFIGURE rts "))
                    {
                        serialportdata.Rts = msg.Substring(14);
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
                    logline("SP:" + serialportdata.Dtr);
                    logline("SP:" + serialportdata.Rts);
                    /**/

                    if ((pumpe == null) && (serialportdata != null))
                    {
                        logline("RR:pumpe init");
                        pumpe = new(tcps[0], tcps[1], serialportdata);
                        logline("RR:pumpe starts");
                        if (pumpe.start())
                        {
                            logline("RR:pumpe runs");

                            byte[] answer = Encoding.ASCII.GetBytes(openanswer);
                            stdout.Write(answer, 0, answer.Length);
                        }
                        else
                        {
                            // Port nicht auf
                            if (pumpe.Inquest == TCP2SERPumpe.Reason.serialfail)
                            {
                                byte[] answer = Encoding.ASCII.GetBytes(openanswerunknownserialport.Replace("%pp%", cmd[2]));
                                stdout.Write(answer, 0, answer.Length);
                            }
                            else
                            if (pumpe.Inquest == TCP2SERPumpe.Reason.tcpfail)
                            {
                                byte[] answer = Encoding.ASCII.GetBytes(openanswerunknownserialport.Replace("%pp%", cmd[1]));
                                stdout.Write(answer, 0, answer.Length);
                            }

                            logline("RR:error " + pumpe.Inquest);
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
                        serialportdata = null;
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

                    logline($"RR:Quit {pid}");
                    Thread.Sleep(10); // wait to settle down
                    Environment.Exit(0); // and dead
                }
                else
                {
                    logline("??" + msg);
                }
            }
        }

        public void job()
        {
            try
            {
                using (stdin = Console.OpenStandardInput())
                {
                    using (stdout = Console.OpenStandardOutput())
                    {
                        byte[] buffer = new byte[1000];
                        string buffers = "";
                        do
                        {
                            int bytes = stdin.Read(buffer, 0, buffer.Length);
                            if ((bytes > 0) || (buffers != ""))
                            {
                                buffers += Encoding.UTF8.GetString(buffer, 0, bytes);
                                bufferhandler(ref buffers);
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

        // does this speed up the entire process? I don't think so.
        void AssemblyPreloader()
        {
            GC.KeepAlive(typeof(SerialPort));
            GC.KeepAlive(typeof(TcpClient));
            GC.KeepAlive(typeof(NetworkStream));
        }

        const int STD_INPUT_HANDLE = -10;
        const int STD_OUTPUT_HANDLE = -11;
        const int STD_ERROR_HANDLE = -12;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CancelIoEx(IntPtr handle, IntPtr lpOverlapped);

        bool StdinReaderPumpeDead = false;
        void StdinReaderPumpe()
        {
            // nix mit töten so einfach, der steht wahrscheinlich in einem dauer-stdin.read()
            byte[] buffer = new byte[1000];
            string buffers = "";
            do
            {
                int bytes = stdin.Read(buffer, 0, buffer.Length);
                if ((bytes > 0) || (buffers != ""))
                {
                    buffers += Encoding.UTF8.GetString(buffer, 0, bytes);
                    bufferhandler(ref buffers);
                }
            } while (!StdinReaderPumpeDead);
        }

        /*
            reader.ReadLine blockiert, daher braucht es eine andere strategie, 
            um die pumpe zu überwachen

            bufferhandler() läuft jedesmal, wenn via stdin irgendwelche befehle kommen
        */
        public void betterjob()
        {
            pid = Process.GetCurrentProcess().Id;
            try
            {
                /* oder in den ersten CONFIGURE Befehl? Lohnt es sich überhaupt?
                logline("RR:Preload");
                Thread preloader = new(AssemblyPreloader);
                preloader.Start();
                */

                logline($"RR:Start {pid}");
                using (stdout = Console.OpenStandardOutput())
                {
                    using (stdin = Console.OpenStandardInput())
                    {
                        Thread worker = new(StdinReaderPumpe);
                        worker.Start();

                        do
                        {
                            // Check the state
                            if (pumpe != null)
                            {
                                if (pumpe.ImDead)
                                {
                                    if (pumpe.Inquest == TCP2SERPumpe.Reason.serialdeath)
                                    {
                                        byte[] answer = Encoding.ASCII.GetBytes(openanswerserialportgone);
                                        stdout.Write(answer, 0, answer.Length);
                                    }
                                    else
                                    if (pumpe.Inquest == TCP2SERPumpe.Reason.tcpdeath)
                                    {
                                        byte[] answer = Encoding.ASCII.GetBytes(openanswertcpgone);
                                        stdout.Write(answer, 0, answer.Length);
                                    }

                                    // Cancel stdin bzw. den Wait auf Daten
                                    StdinReaderPumpeDead = true;
                                    var handle = GetStdHandle(STD_INPUT_HANDLE);
                                    CancelIoEx(handle, IntPtr.Zero);

                                    logline("RR:Pumpe died");
                                    break;
                                }
                            }
                            // 10 ms?
                            Thread.Sleep(10);
                        } while (true);
                    }
                }
            }
            catch (Exception e)
            {
                logline("EM:" + e.ToString());
                // eat it.
            }
            logline($"RR:End {pid}");
        }
    }
}
