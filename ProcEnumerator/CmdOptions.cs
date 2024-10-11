using System;
using CommandLine;

namespace ProcEnumerator
{
    public class CmdOptions
    {
        [Option('p', "process", Required = false, SetName = "scan", HelpText = "Scan only running processes folders.")]
        public bool Processes { get; set; }

        [Option('s', "service", Required = false, SetName = "scan", HelpText = "Scan only installed services folders.")]
        public bool Services { get; set; }

        [Option('a', "all", Required = false, SetName = "scan", HelpText = "Scan both processes and services folders. This is the default option.")]
        public bool All { get; set; }
    }
}