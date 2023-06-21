/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/WinEventCat.
*/

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Diagnostics.Eventing.Reader;

namespace WinEventCat
{
    /// A simple utility to read and tail Windows Event logs.
    /// This utility can read offline *.evtx dump files or tail and read
    // online, live windows event logs.
    [SupportedOSPlatform("windows")]
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsageAndExit();
                return;
            }

            var cmdLineArgs = ParseCmdLineArgs(args);

            CmdOptions cmd = CmdOptions.Build(cmdLineArgs);
            if (!cmd.Validate())
            {
                PrintUsageAndExit();
                return;
            }

            OutputOptions outOpts = OutputOptions.Build(cmdLineArgs);
            if (!outOpts.Validate())
            {
                PrintUsageAndExit();
                return;
            }

            FilterOptions filterOpts = FilterOptions.Build(cmdLineArgs);
            if (!filterOpts.Validate())
            {
                PrintUsageAndExit();
                return;
            }

            if ("list".Equals(cmd.Cmd))
            {
                var logs = ListConfiguredLogs();
                int i = 1;

                Console.WriteLine("Slno logName:numberofEvents");
                foreach (var logName in logs.Keys)
                {
                    Console.WriteLine($"{i++} {logName}:{logs[logName]}");
                }

                return;
            }
            else if ("readAll".Equals(cmd.Cmd))
            {
                var logs = ListConfiguredLogs();
                foreach (var logName in logs.Keys)
                {
                    if (logs[logName] > 0) //process only those logs where event counts are >0
                    {
                        ProcesOnlineLogSource(logName, outOpts, filterOpts);
                    }
                }

                return;
            }
            else //read given inputs
            {
                InputOptions inOpts = InputOptions.Build(cmdLineArgs);
                if (!inOpts.Validate())
                {
                    PrintUsageAndExit();
                    return;
                }

                if (!String.IsNullOrEmpty(inOpts.SourcePath))
                {
                    ProcessOffilneLogFiles(inOpts.SourcePath, outOpts, filterOpts);
                    return;
                }

                if (!String.IsNullOrEmpty(inOpts.LogName))
                {
                    if (outOpts.IsFollowTail())
                    {
                        TailOnlineLogSource(inOpts.LogName, outOpts, filterOpts);
                    }
                    else
                    {
                        ProcesOnlineLogSource(inOpts.LogName, outOpts, filterOpts);
                    }
                }
            }
        }



        private static IDictionary<string, int> ListConfiguredLogs()
        {
            var logsConfigured = new Dictionary<string, int>();

            using (var session = new EventLogSession())
            {
                foreach (var eventLogName in session.GetLogNames())
                {
                    using (EventLog evl = new EventLog(eventLogName))
                    {
                        try
                        {
                            var eventsCount = evl.Entries.Count;
                            logsConfigured[eventLogName] = eventsCount;
                        }
                        catch //access errors
                        {
                            logsConfigured[eventLogName] = -1;
                        }
                    }
                }
            }

            return logsConfigured;
        }


        private static void ProcesOnlineLogSource(string logName, OutputOptions outOpts, FilterOptions filterOpts)
        {
            Console.WriteLine($"Processing live {logName} logs...");

            string query = filterOpts.BuildXpathQuery(logName);

            var elq = new EventLogQuery(logName, PathType.LogName, query);

            TextWriter? destWriter = null;
            try
            {
                destWriter = outOpts.GetOutputWriter(Path.GetFileName(logName));
                ProcesEventLog(elq, outOpts, destWriter);
            }
            finally
            {
                if (destWriter != null && destWriter != Console.Out)
                {
                    destWriter.Dispose();
                }
            }
        }


        private static void ProcesEventLog(EventLogQuery elq, OutputOptions outOpts, TextWriter destWriter)
        {
            int firstX = outOpts.HeadLines();
            int lastX = outOpts.TailLines();
            string[] linesToPrint = new string[firstX > 0 ? firstX : lastX > 0 ? lastX : 0];

            using (var reader = new EventLogReader(elq))
            {
                long recCounter = 0;
                string? line = null;
                EventRecord record;

                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        line = EventRecordToString(record, outOpts.Dump);
                        recCounter++;
                    }

                    if (line != null)
                    {
                        if (linesToPrint.Length == 0) //head or tail not set
                        {
                            destWriter.WriteLine(line);
                        }
                        else
                        {
                            //tail set and accumulator array full?
                            //Shift cells up by 1 and fill new line
                            //at the last element in the array
                            if (lastX > 0 && recCounter > lastX)
                            {
                                string[] temp = new string[lastX];
                                Array.Copy(linesToPrint, 1, temp, 0, lastX - 1);
                                temp[lastX - 1] = line;
                                linesToPrint = temp;
                            }
                            else
                            {
                                linesToPrint[recCounter - 1] = line;
                            }
                        }

                        line = null;
                    }

                    //head, break if we are done printing
                    if (firstX > 0 && recCounter == firstX)
                    {
                        break;
                    }
                }
            }

            //print lines collected for head or tail options
            foreach (var ll in linesToPrint)
            {
                if (String.IsNullOrEmpty(ll))
                {
                    break; //exhausted array, tail or head value is more than matching lines
                }

                destWriter.WriteLine(ll);
            }
        }


        private static void TailOnlineLogSource(string logName, OutputOptions outOpts, FilterOptions filterOpts)
        {
            //TODO
            Console.WriteLine($"Tailing on {logName} logs. Enter q to quit watching...");

            using (var session = new EventLogSession())
            {
                string xpathQ = filterOpts.BuildXpathQuery(logName);

                var query = new EventLogQuery(logName, PathType.LogName, xpathQ)
                {
                    TolerateQueryErrors = true,
                    Session = session
                };

                EventLogWatcher watcher;
                using (watcher = new EventLogWatcher(query))
                {
                    watcher.EventRecordWritten += new EventHandler<EventRecordWrittenEventArgs>(EventsWrittenLogWatcher);
                    
                    try
                    {
                        watcher.Enabled = true; //starts listening

                        //block untill quit
                        while (!Environment.HasShutdownStarted)
                        {
                            if ("q".Equals(Console.ReadLine()))
                            {
                                break;
                            }
                        }
                    }
                    finally
                    {
                        watcher.Enabled = false;
                    }
                }
            }
        }


        private static void EventsWrittenLogWatcher(object? sender, EventRecordWrittenEventArgs e)
        {
            var line = EventRecordToString(e.EventRecord, false);
            Console.WriteLine(line);
        }

        private static void ProcessOffilneLogFiles(string sourcePath, OutputOptions outOpts, FilterOptions filter)
        {
            if (!Path.Exists(sourcePath))
            {
                PrintUsageAndExit();
                return;
            }

            FileInfo finfo = new FileInfo(sourcePath);
            if (finfo.Exists) //is file
            {
                ReadEventsFromFile(finfo.FullName, outOpts, filter);
                return;
            }

            //process each evtx file in the given folder
            DirectoryInfo dirInfo = new DirectoryInfo(sourcePath);
            if (dirInfo.Exists)
            {
                foreach (var f in dirInfo.EnumerateFiles("*.evtx"))
                {
                    ReadEventsFromFile(f.FullName, outOpts, filter);
                }
            }
        }

        private static void ReadEventsFromFile(string logPath, OutputOptions outOpts, FilterOptions filter)
        {
            Console.WriteLine($"Processing file: {logPath} ...");
            var fileUri = $"file://{logPath}";
            var query = filter.BuildXpathQuery(fileUri);
            var elq = new EventLogQuery(fileUri, PathType.FilePath, query);


            TextWriter? destWriter = null;
            try
            {
                destWriter = outOpts.GetOutputWriter(Path.GetFileName(logPath));
                ProcesEventLog(elq, outOpts, destWriter);
            }
            finally
            {
                if (destWriter != null && destWriter != Console.Out)
                {
                    destWriter.Dispose();
                }
            }
        }


        private static string? EventRecordToString(EventRecord record, bool dump)
        {
            string taskName;
            try
            {
                taskName = record.TaskDisplayName;
            }
            catch
            {
                taskName = "---";
            }

            string logLevel;
            try
            {
                logLevel = record.LevelDisplayName;
            }
            catch
            {
                logLevel = Convert.ToString(record.Level ?? 0);
            }

            string description;
            try
            {
                description = record.FormatDescription();
            }
            catch (Exception e)
            {
                description = e.Message;
            }

            var line = !dump ? String.Format(
                                "#{0}: {1} {2} {3} {4} {5} [{6}] {7}",
                                record.RecordId,
                                record.LogName,
                                logLevel,
                                record.TimeCreated,
                                record.Id,
                                taskName,
                                description,
                                record.ProviderName)
                            : record.ToXml();

            return line;
        }

        private static IDictionary<string, string> ParseCmdLineArgs(string[] args)
        {
            string[] knownCmds =
            {
                "-cmd",

                //Input
                "-logName",
                "-sourcePath",                
                
                //output
                "-exportLoc",
                "-tail",
                "-head",
                "-dump",                
               
               //filter conditions
                "-include",
                "-exclude",
                "-logLevel",
                "-before",
                "-after",
                "-between"
            };


            IDictionary<string, string> cmdLineArgs = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                var cc = arg.Split('=', 2);
                var cmdKey = cc[0];
                var cmdValue = (cc.Length > 1) ? cc[1] : "";
                if (knownCmds.Contains(cmdKey))
                {
                    cmdLineArgs.Add(cmdKey, cmdValue);
                }
                else
                {
                    Console.WriteLine($"Unknown input {cmdKey}");
                    PrintUsageAndExit();
                }
            }

            return cmdLineArgs;
        }

        private static void PrintUsageAndExit()
        {
            var cmdName = Process.GetCurrentProcess().ProcessName;

            string[] options =
            {
                //Cmd
                "-cmd=<list|read|readAll> where List cmd is used to find Logs configured and read is used to process Logs. ReadAll process all logs configured on this system.",

                //Input
                "-logName=<live windows event log name>",
                "-sourcePath=<folder or file to scan for offline log files>",                
                
                //output
                "-exportLoc=<[outputFolderLocation]>",
                "-tail=<nn number of lines or f to follow>",
                "-head=<nn number of lines>",
                "-dump=<true>",                
               
               //filter conditions
                "-include=<eventId1,eventId2,...>",
                "-exclude=<eventId1,eventId2,...>",
                "-logLevel=<info|warn|error>",
                "-before=<yyyyMMdd>",
                "-after=<yyyyMMdd>",
                "-between=<yyyyMMdd1,yyyyMMdd2>"
            };

            string optionsStr = "";
            foreach (string opt in options)
            {
                optionsStr += $"[{opt}],{Environment.NewLine}";
            }
            optionsStr = optionsStr.TrimEnd().TrimEnd(',');

            Console.WriteLine($"Usage: {cmdName} {optionsStr}");
            Environment.Exit(1);
        }
    }
}
