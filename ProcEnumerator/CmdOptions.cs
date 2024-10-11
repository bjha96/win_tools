/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/win_tools.
*/

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