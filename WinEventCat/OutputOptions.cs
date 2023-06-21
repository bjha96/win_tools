/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/WinEventCat.
*/

namespace WinEventCat
{
    internal class OutputOptions
    {
        public string? ExportLocation { get; set; }
        public string? Tail { get; set; }

        public string? Head { get; set; }

        public bool Dump { get; set; }

        public TextWriter GetOutputWriter(string? logNameOrLogFilePath)
        {
            if(String.IsNullOrEmpty(ExportLocation) || String.IsNullOrEmpty(logNameOrLogFilePath))
            {
                return Console.Out;
            }

            var outputFile = BuildOutputFilePath(ExportLocation, logNameOrLogFilePath);
            if(Path.Exists(outputFile))
            {
                var newFilename = Path.ChangeExtension(outputFile, ".old." + DateTime.Now.ToString("yyyyMMddHHmmss"));
                File.Move(outputFile, newFilename, true);
                Console.WriteLine($"Existing {outputFile} file moved to {newFilename}");
            }
            
            var dest = File.CreateText(outputFile);
            return dest;
        }

        public int HeadLines()
        {
            return (String.IsNullOrEmpty(this.Head) ? -1 : Convert.ToInt32(this.Head));
        }

        public int TailLines()
        {
            if (IsFollowTail())
            {
                return -1;
            }

            return (String.IsNullOrEmpty(this.Tail) ? -1 : Convert.ToInt32(this.Tail));
        }

        public bool IsFollowTail()
        {
            return "f".Equals(this.Tail, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool Validate()
        {
            if(!String.IsNullOrEmpty(ExportLocation))
            {
                if(!Path.Exists(ExportLocation))
                {
                    Console.WriteLine($"exportLocation {ExportLocation} does not exist!");
                    return false;
                }
            }

            if(!String.IsNullOrEmpty(Head) && !String.IsNullOrEmpty(Tail))
            {
                Console.WriteLine($"Either head or tail, not both can be specified in the outputOption!");
                return false;            
            }

            try
            {
                if(!String.IsNullOrEmpty(Head))
                {
                    if(HeadLines() <= 0)
                    {
                        Console.WriteLine("head must be a +ve integer!");
                        return false;
                    }
                }

                if( !String.IsNullOrEmpty(Tail) && !IsFollowTail() )
                {
                    if(TailLines() <= 0)
                    {
                        Console.WriteLine("tail must be a +ve integer!");
                        return false;
                    }
                }
            }
            catch(FormatException)
            {
                Console.WriteLine("head or tail, must be +ve integer!");
                return false;
            }

            return true;
        }

        public static OutputOptions Build(IDictionary<string, string> argsMap)
        {
            var opts = new OutputOptions();

            string? output;

            if (argsMap.TryGetValue("-exportLoc", out output))
            {
                opts.ExportLocation = output;
            }

            if (argsMap.TryGetValue("-tail", out output))
            {
                opts.Tail = output;
            }

            if (argsMap.TryGetValue("-head", out output))
            {
                opts.Head = output;
            }

            if (argsMap.TryGetValue("-dump", out output))
            {
                opts.Dump = "true".Equals(output);
            }

            return opts;
        }


        static string BuildOutputFilePath(string destFolder, string logNameOrPath)
        {
            string filename = Path.GetFileName(logNameOrPath);
            var path = Path.Combine(destFolder, filename);
            path = Path.ChangeExtension(path, ".txt");            
            return path;
        }
    }
}