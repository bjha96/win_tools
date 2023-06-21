/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/WinEventCat.
*/
namespace WinEventCat
{
    internal class InputOptions
    {
        public string? LogName { get; set; }
        public string? SourcePath { get; set; }

        public bool Validate()
        {
            //atleast 1 source must
            if (String.IsNullOrEmpty(LogName) && String.IsNullOrEmpty(SourcePath))
            {
                Console.WriteLine("Atleast 1 inputOptions must be specified!");
                return false;
            }

            //Only 1 source must be specified.
            if (!String.IsNullOrEmpty(LogName) && !String.IsNullOrEmpty(SourcePath))
            {
                Console.WriteLine("Only 1 inputOptions can be specified!");
                return false;
            }

            return true;
        }

        public static InputOptions Build(IDictionary<string, string> argsMap)
        {
            var srcOpts = new InputOptions();

            string? output;

            if (argsMap.TryGetValue("-logName", out output))
            {
                srcOpts.LogName = output;
            }

            if (argsMap.TryGetValue("-sourcePath", out output))
            {
                srcOpts.SourcePath = output;
            }

            return srcOpts;
        }
    }
}