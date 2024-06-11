///
/// Simple Network monitor CLI
///


using System.Diagnostics;

namespace NetWatchCLI
{
    class NetInfo
    {
        public string? SourceIp { get; set; }
        public string? DestIp { get; set; }
        public string? SourcePort { get; set; }
        public string? DestPort { get; set; }
        public int ProcId { get; set; }
        public string? ProcBinPath { get; set; }

        public string? Protocol { get; set; }

        public string? State { get; set; }

        // override object.Equals
        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return (ToNormalizedString().Equals(obj.ToString()));
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return ToNormalizedString().GetHashCode();
        }

        public override string ToString()
        {
            //return "";
            return string.Format(
                $"[{{0}}:{{1}}-{{2}}:{{3}}] {{4}} {{5}} {{6}} {{7}}",
                 SourceIp, SourcePort, DestIp, DestPort, ProcId, ProcBinPath, Protocol, State);
        }

        public string ToNormalizedString()
        {
            //return "";
            return string.Format(
                $"[{{0}}:{{1}}-{{2}}:{{3}}]",
                 SourceIp, SourcePort, DestIp, DestPort);
        }
    }



    class ThreadWorkers
    {
        public static void ConsumeStream(StreamReader stream, IList<string> outputLines)
        {
            string? line;

            while ((line = stream.ReadLine()) != null)
            {
                outputLines.Add(line.Trim());
            }

            //stream.Close();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
            Trace.AutoFlush = true;

            IList<NetInfo> ConnectionsList = GetConnections();
            foreach (NetInfo netInfo in ConnectionsList)
            {
                Console.WriteLine(netInfo.ToString());
            }
        }


        private static IList<NetInfo> GetConnections()
        {
            IList<NetInfo> netInfoList = new List<NetInfo>();

            using (Process proc = new())
            {
                proc.StartInfo = new ProcessStartInfo("netstat.exe", "-ano")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                proc.Start();
                Trace.TraceInformation($"Started process: {proc.ProcessName}:{proc.Id}");

                Thread? stdoutReader = null, stderrReader = null;
                IList<string> outputLines = new List<string>();

                if (proc.StartInfo.RedirectStandardOutput)
                {
                    var stdout = proc.StandardOutput;
                    stdoutReader = new Thread(() => ThreadWorkers.ConsumeStream(stdout, outputLines));
                    stdoutReader.Start();
                }

                if (proc.StartInfo.RedirectStandardError)
                {
                    var stderr = proc.StandardError;
                    stderrReader = new Thread(() => ThreadWorkers.ConsumeStream(stderr, outputLines));
                    stderrReader.Start();
                }

                if (!proc.WaitForExit(60 * 1000))//wait for 60s
                {
                    Trace.TraceWarning("Process hang!");
                    proc.Kill();
                }

                stdoutReader?.Join();
                stderrReader?.Join();

                ParseNetstatOutputLines(outputLines, netInfoList);

                var ec = proc.ExitCode;
                Trace.TraceInformation($"process exit code: {ec}");
                if (ec != 0)
                {
                    throw new Exception($"Unexpected process exit code: {ec}");
                }
            }

            return netInfoList;
        }

        private static void ParseNetstatOutputLines(IList<string> outputLines, IList<NetInfo> netInfoList)
        {
            foreach (var line in outputLines)
            {
                Trace.TraceInformation(line);

                if (!IsValidParseableLine(line))
                {
                    continue;
                }

                //Sample lines to parse. 
                //Note UDP line doesn't have socket state column
                //Proto  Local Address          Foreign Address        State           PID
                //TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       1596
                //UDP    127.0.0.1:1900         *:*                                    10192
                //UDP    127.0.0.1:49664        127.0.0.1:49664                        5472
                //An Ipv6 line looks like
                //TCP    [::]:2179              [::]:0                 LISTENING    1234

                var tokens = line.Split(" ", 5, StringSplitOptions.RemoveEmptyEntries);

                if (line.StartsWith("UDP", StringComparison.InvariantCultureIgnoreCase) && tokens.Length == 4)
                {
                    //Adjust for UDP socket state
                    //UDP has no connection state, only 4 tokens in the output line
                    //We will put 4th index as socket state -                
                    Trace.TraceInformation($"Adjusting UDP tokens {line}");
                    var tokens2 = new string[5];
                    for (int i = 0; i <= 2; i++)
                    {
                        tokens2[i] = tokens[i];
                    }
                    tokens2[3] = "-"; //socket state for udp
                    tokens2[4] = tokens[3]; //process Id
                    tokens = tokens2;
                }

                //Local IP and port
                string[] sipNport = ExtractIPandPort(tokens[1]);
                Trace.TraceInformation($"sip:port= {sipNport[0]}<->{sipNport[1]}");

                //Remote IP and port
                var dipNport = ExtractIPandPort(tokens[2]);
                Trace.TraceInformation($"dip:port= {dipNport[0]}<->{dipNport[1]}");

                NetInfo netInfo = new NetInfo
                {
                    Protocol = tokens[0],
                    SourceIp = sipNport[0],
                    // SourcePort = Convert.ToInt32(sipNport[1]),
                    SourcePort = sipNport[1],
                    DestIp = dipNport[0],
                    // DestPort = Convert.ToInt32(dipNport[1]),
                    DestPort = dipNport[1],
                    State = tokens[3],
                    ProcId = Convert.ToInt32(tokens[4])
                };

                netInfoList.Add(netInfo);
            }
        }

        private static bool IsValidParseableLine(string line)
        {
            if (!String.IsNullOrEmpty(line))
            {
                string[] validProtos = ["TCP", "UDP"]; //valid protocols

                foreach (string proto in validProtos)
                {
                    if (line.StartsWith(proto, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string[] ExtractIPandPort(string ipAndPort)
        {
            int idx = ipAndPort.LastIndexOf(':');

            if (idx == -1)
                throw new ArgumentException($"Bad ip:port data {ipAndPort}");

            string[] values = [ipAndPort[..idx].Trim(), ipAndPort[(idx + 1)..].Trim()];
            return values;
        }
    }
}