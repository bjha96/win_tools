/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/SysStressCLI.
*/

using System.Diagnostics;
using System.Runtime.Versioning;

namespace SysStressCLI
{
    /// A simple utility to simulate system stress covering CPU, IO and Memory.
    /// Run the program without arguments to see the usage notes.
    ///
    /// ** CAUTION: ** 
    /// Running this program with "higher" or "highest" priority can starve 
    /// other processes and cause system instability and crash.
    [SupportedOSPlatform("windows")]
    internal class Program
    {
        private const int PERF_MEASURE_INTERVAL = 1000; // millis

        private const int MAX_RANDOM_READS_COUNT = 100;

        private const int MAX_OBJECTS_IN_MEMORY = 1024 * 1024;

        private const long MAX_FILE_SIZE = 2 * 1024L * 1024 * 1024; //2GB

        private const int MAX_DEFAULT_CPU_USAGE = 90;

        private static volatile bool s_StopSignal = false;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsageAndExit();
                return;
            }

            var cmdLineArgs = ParseCmdLineArgs(args);

            //make this process important
            AdjustProcessPriority(cmdLineArgs);

            //Check for quit signal
            new Thread(CheckStop).Start();

            if (cmdLineArgs.ContainsKey("-cpu"))
            {
                RunCpuStress(cmdLineArgs);
            }

            if (cmdLineArgs.ContainsKey("-mem"))
            {
                RunMemoryStress(cmdLineArgs);
            }


            if (cmdLineArgs.ContainsKey("-io"))
            {
                string? path;
                cmdLineArgs.TryGetValue("-io", out path);
                new Thread(new ParameterizedThreadStart(StressIO)).Start(path);
            }

            var th = new Thread(MeasurePerf);
            th.Start();
            th.Join();
        }

        private static void RunMemoryStress(IDictionary<string, string> cmdLineArgs)
        {
            string? memUsagePercent;
            if (!cmdLineArgs.TryGetValue("-mem", out memUsagePercent))
            {
                PrintUsageAndExit();
                return;
            }

            int memPc;
            if (!int.TryParse(memUsagePercent, out memPc))
            {
                Console.WriteLine("Memory usage must be positive digit!");
                PrintUsageAndExit();
                return;
            }

            if (memPc < 0)
            {
                Console.WriteLine("Memory usage can't be negative digit!");
                PrintUsageAndExit();
                return;
            }

            new Thread(new ParameterizedThreadStart(StressMemory)).Start(memPc);
        }


        private static void AdjustProcessPriority(IDictionary<string, string> cmdLineArgs)
        {
            string? priority;
            if (cmdLineArgs.TryGetValue("-priority", out priority))
            {

                ProcessPriorityClass ppc;

                switch (priority)
                {
                    case "highest":
                        ppc = ProcessPriorityClass.RealTime;
                        break;
                    case "higher":
                        ppc = ProcessPriorityClass.High;
                        break;
                    case "high":
                        ppc = ProcessPriorityClass.AboveNormal;
                        break;
                    default:
                        ppc = ProcessPriorityClass.Normal;
                        break;
                }

                Console.WriteLine("Adjusting process priority to: " + ppc);
                Process.GetCurrentProcess().PriorityClass = ppc;
            }
        }

        private static IDictionary<string, string> ParseCmdLineArgs(string[] args)
        {
            string[] validArgs =
            {
                "-cpu",
                "-mem",
                "-io",
                "-priority"
            };

            IDictionary<string, string> cmdLineArgs = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                var cc = arg.Split('=', 2);
                var cmdKey = cc[0];
                var cmdValue = (cc.Length > 1) ? cc[1] : "";
                if (validArgs.Contains(cmdKey))
                {
                    cmdLineArgs.Add(cmdKey, cmdValue);
                }
                else
                {
                    Console.WriteLine($"Unknown option: {cmdKey}");
                    PrintUsageAndExit();
                    break;
                }
            }

            return cmdLineArgs;
        }

        private static void RunCpuStress(IDictionary<string, string> cmdLineArgs)
        {
            string? cpuPercentageValue;
            if (!cmdLineArgs.TryGetValue("-cpu", out cpuPercentageValue))
            {
                PrintUsageAndExit();
                return;
            }

            int cpuPc;
            if (!int.TryParse(cpuPercentageValue, out cpuPc))
            {
                Console.WriteLine("Cpu usage must be positive digit!");
                PrintUsageAndExit();
                return;
            }

            if (cpuPc < 0)
            {
                Console.WriteLine("Cpu usage can't be negative digit!");
                PrintUsageAndExit();
                return;
            }

            if (String.IsNullOrEmpty(cpuPercentageValue))
            {
                Console.WriteLine("Cpu usage can't be empty");
                PrintUsageAndExit();
                return;
            }

            new Thread(new ParameterizedThreadStart(StressCPU)).Start(cpuPercentageValue);
        }

        private static void PrintUsageAndExit()
        {
            //Console.WriteLine("Bad input!");
            Console.WriteLine($"Usage: {Process.GetCurrentProcess().ProcessName} [-cpu=<xx>] [-mem=<yy>] [-io=<[testPath]>] [-priority=<high|higher|highest>]");
            Console.WriteLine($"Where xx means stressing CPU to xx percent and yy means using yy percentage of RAM for memory stress.");
            Environment.Exit(1);
        }

        private static void StressCPU(object? cpuUsage)
        {
            int maxThreads = Environment.ProcessorCount;
            Console.WriteLine($"Stressing CPU upto {cpuUsage}% with {maxThreads} threads...");

            for (int i = 0; i < maxThreads; i++)
            {
                Thread t = new Thread(new ParameterizedThreadStart(StressCpuCores));
                t.Start(cpuUsage);
            }
        }

        private static void StressIO(object? testPath)
        {
            string? testPathLoc = Convert.ToString(testPath);
            if (String.IsNullOrEmpty(testPathLoc))
            {
                testPathLoc = Path.GetTempPath();
            }
            testPathLoc = Path.GetFullPath(testPathLoc);
            Console.WriteLine($"Stressing IO at {testPathLoc} ...");

            Random random = new Random();
            byte[] buff = new byte[32 * 1024]; //32k
            bool fileCreated = false;
            string? tempFilePath = null;
            long bytesProcessed = 0L;
            Stopwatch watch = Stopwatch.StartNew();

            while (!s_StopSignal)
            {
                tempFilePath = GetTempFilePath(testPathLoc);

                //write a temp file of max length
                using (var file = File.Create(tempFilePath))
                {
                    fileCreated = true;

                    long fileSize = 0;
                    while (fileSize <= MAX_FILE_SIZE)
                    {
                        random.NextBytes(buff);
                        file.Write(buff);
                        fileSize += buff.Length;
                    }

                    file.Flush(true); //force flush to disk

                    bytesProcessed += fileSize;
                }


                //read back temp file randomly       
                using (var file = File.OpenRead(tempFilePath))
                {
                    long fileSize = file.Length;
                    int readCounts = 0;
                    using (var safeHandle = file.SafeFileHandle)
                    {
                        while ( !s_StopSignal && (readCounts++ < MAX_RANDOM_READS_COUNT))
                        {
                            long offset = random.NextInt64(fileSize);
                            int bytesRead = RandomAccess.Read(safeHandle, buff, offset);
                            //Console.WriteLine("RandomAccess bytes read: "+ bytesRead);
                            bytesProcessed += bytesRead;
                        }
                    }
                }

                var timeElapsedSeconds = watch.ElapsedMilliseconds / 1000.0d;
                if (timeElapsedSeconds >= 1)
                {
                    var mbPerSec = Math.Round((bytesProcessed / (1024 * 1024)) / timeElapsedSeconds); //MB
                    Console.WriteLine($"I/O done at {mbPerSec} MB/s");

                    watch.Restart();
                    bytesProcessed = 0L;
                }

                if (fileCreated)
                {
                    File.Delete(tempFilePath);
                    fileCreated = false;
                    tempFilePath = null;
                }
            }

            watch.Stop();

            //probably not needed?
            if (fileCreated && tempFilePath != null)
            {
                File.Delete(tempFilePath);
            }
        }


        private static string GetTempFilePath(string tmpFolder)
        {
            string tmpFolderPath = Path.GetFullPath(tmpFolder);
            if (Directory.Exists(tmpFolderPath))
            {
                var path = Path.Combine(tmpFolder, Guid.NewGuid().ToString());
                path = Path.ChangeExtension(path, ".test");
                return path;
            }

            Console.WriteLine($"I/O path ${tmpFolderPath} does not exist!");
            PrintUsageAndExit();
            return tmpFolderPath;
        }


        private static void StressMemory(object? memUsagePercent)
        {
            long totalMemoryToUse = (GC.GetGCMemoryInfo().TotalAvailableMemoryBytes * Convert.ToInt64(memUsagePercent)) / 100L;
            //No arbitrarily small obejct size, minimum 1k
            int buffSize = Math.Max(1024, Convert.ToInt32(totalMemoryToUse / MAX_OBJECTS_IN_MEMORY));
            Console.WriteLine($"Stressing memory with {MAX_OBJECTS_IN_MEMORY} objects of {buffSize} bytes each...");

            //Keep the ref to prevent GC
            IDictionary<int, byte[]> bufferList = new Dictionary<int, byte[]>(MAX_OBJECTS_IN_MEMORY);
            Random rand = new Random();
            int buffListPointer = 0;
            while (!s_StopSignal)
            {
                byte[] buff = new byte[buffSize];
                rand.NextBytes(buff);
                bufferList[buffListPointer++] = buff;
                //GC.KeepAlive(buff);

                if (buffListPointer >= MAX_OBJECTS_IN_MEMORY)
                {
                    buffListPointer = 0; //Fixed size buffer
                }
            }
        }

        //Ref- https://stackoverflow.com/questions/2514544/simulate-steady-cpu-load-and-spikes
        private static void StressCpuCores(object? cpuUsage)
        {
            int cpuUsagePercent = Convert.ToInt32(cpuUsage ?? MAX_DEFAULT_CPU_USAGE);

            Parallel.For(0, 1, new Action<int>((int i) =>
            {
                Stopwatch watch = Stopwatch.StartNew();
                var rand = new Random();

                while (!s_StopSignal)
                {
                    //Do some arbitrary stuff to consume CPU              
                    var x = Math.Pow(rand.Next(1000), rand.Next(10));
                    x = x++ * rand.NextDouble();

                    if ((100 - cpuUsagePercent >= 0) && (watch.ElapsedMilliseconds > cpuUsagePercent))
                    {
                        Thread.Sleep(100 - cpuUsagePercent);
                        watch.Restart();
                    }
                }

                watch.Stop();
            }));
        }


        private static void CheckStop()
        {
            Console.WriteLine("Starting test, enter 'q' to quit.");

            while (!s_StopSignal)
            {
                if (Environment.HasShutdownStarted)
                {
                    s_StopSignal = true;
                    break;
                }

                var cmd = Console.ReadLine(); //blocks

                if ("q".Equals(cmd, StringComparison.InvariantCultureIgnoreCase))
                {
                    s_StopSignal = true;
                    Console.WriteLine("Shutting down...");
                    break;
                }
            }
        }


        private static void MeasurePerf()
        {
            if (OperatingSystem.IsWindows())
            {
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                var ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                var totalmem = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024.0);

                while (!s_StopSignal)
                {
                    var availableMem = ramCounter.NextValue();
                    var percentAvailableMem = 100.0 * (availableMem / totalmem); //in %
                    percentAvailableMem = Math.Round(percentAvailableMem, 1); //round to 1 decimal
                    var cpuUsage = Math.Round(cpuCounter.NextValue(), 1);

                    Console.WriteLine($"CPU={cpuUsage}%, Available memory={availableMem}MB ({percentAvailableMem}%)");

                    Thread.Sleep(PERF_MEASURE_INTERVAL);
                }
            }
            else
            {
                Console.WriteLine("Unsupported performance counters on OS: " + Environment.OSVersion);
            }
        }
    }
}
