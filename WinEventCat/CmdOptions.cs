/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/WinEventCat.
*/

namespace WinEventCat
{
    internal class CmdOptions
    {
        public string? Cmd { get; set; }

        public bool Validate()
        {
            string[] validCmds = {"list", "read", "readAll"};
            
            if(!validCmds.Contains(Cmd ?? "-"))
            {
                Console.WriteLine($"cmd must be one of {String.Join(", ", validCmds)}");
                return false;
            }
            
            return true;
        }

        public static CmdOptions Build(IDictionary<string, string> argsMap)
        {
            var opts = new CmdOptions();

            string? output;
            if (argsMap.TryGetValue("-cmd", out output))
            {
                opts.Cmd = output;
            }

            return opts;
        }
    }
}