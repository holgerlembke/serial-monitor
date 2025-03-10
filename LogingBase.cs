
namespace serial_monitor
{
    internal class LogingBase
    {
        string logfilename;
        string path = "";

        public LogingBase(string logfilename)
        {
            path = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)+
                   Path.DirectorySeparatorChar;

            this.logfilename = logfilename;
            // logline("SL:Start");
        }

        void logit(string line)
        {
// no debug files on release
#if !DEBUG
            return;
#endif

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
}
