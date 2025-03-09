using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace serial_monitor
{
    internal partial class SerialMonitor : LogingBase
    {
        public SerialMonitor() : base(@"\logmonitor.txt") { }

        SerialPortData serialportdata = null;
        TCP2SERPumpe pumpe = null;
        Stream stdin = null;
        Stream stdout = null;

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
    }

}
