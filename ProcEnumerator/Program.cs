/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/win_tools.
*/


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Management;
using CommandLine;
using System.Text;


/// <summary>
/// <p>
/// Finds running processes and checks the host directories of these
/// executables and loaded assemblies for write and execute permission 
/// for the current user. Can be used for checking loose folder hardening 
/// for services/processes running with system privileges.
/// </p>
/// <br />
/// <b>Usage:</b>
/// <br />
/// <p>
/// Run with -p for checking only existing process folders.
/// <br />
/// Run with -s for checking only installed services folders.
/// <br />
/// Do not supply any cmd line args, to check both processes and services folders.
/// </p>
/// </summary>
#pragma warning disable CS8604 // Possible null reference argument.
namespace ProcEnumerator
{
    [SupportedOSPlatform("windows")]
    class Program
    {
        //const string TEST_EXE_NAME = "~$psTest.bat";

        static void Main(string[] args)
        {
            try
            {
                using (var debugLog = File.CreateText(Path.Combine(Path.GetTempPath(), "PsEnumerator.log")))
                {
                    SetupDebugLogs(debugLog);

                    var exePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var cmdOpts = Parser.Default.ParseArguments<CmdOptions>(args);
                    cmdOpts.WithParsed<CmdOptions>(O =>
                        {
                            if (O.Services)
                            {
                                CheckInstalledServices(exePaths);
                            }
                            else if (O.Processes)
                            {
                                CheckRunningProcesses(exePaths);
                            }
                            else //if (O.All) is default
                            {
                                CheckInstalledServices(exePaths);
                                CheckRunningProcesses(exePaths);
                            }
                        }
                    );

                    Debug.WriteLine($"Found **{exePaths.Count}** unique exe folders:");
                    Debug.WriteLine(String.Join(Environment.NewLine, exePaths));

                    CheckWXPermissionOnFolders(exePaths);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error {ex}");
            }
        }

        private static void SetupDebugLogs(StreamWriter dest)
        {
            //Trace.Listeners affects .Net core also
            Trace.Listeners.Add(new TextWriterTraceListener(dest));
            Debug.AutoFlush = true;
            Trace.AutoFlush = true;
        }

        private static void CheckInstalledServices(HashSet<string> exePaths)
        {
            try
            {
                using (ManagementObjectSearcher mos = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT Name,PathName,StartMode,State FROM Win32_Service"))
                {
                    foreach (var moq in mos.Get())
                    {
                        using (moq)
                        {
                            string? exeCmdLine = moq["PathName"] as string;
                            Debug.WriteLine($"Found service: {moq["Name"]} {moq["StartMode"]} {moq["State"]} {exeCmdLine}");

                            //Pathname looks like "C:\Program Files\Windows Media Player\wmpnetwk.exe" -k -a "abcdef"
                            //need to parse the exe path and extract the folder from it
                            if (!String.IsNullOrWhiteSpace(exeCmdLine))
                            {
                                var exePath = ExtractExePath(exeCmdLine);
                                exePaths.Add(Path.GetDirectoryName(exePath));
                            }
                        }
                    }
                }
            }
            catch (ManagementException mex)
            {
                Trace.TraceWarning($"Failed to query services due to {mex.Message}");
            }
        }


        //Sample exeCmdLine: 
        //"C:\Program Files\Windows Media Player\wmpnetwk.exe" -k -a "abcdef"
        //C:\windows\system32\xyz.exe -k -a "abcdef"
        private static string ExtractExePath(string exeCmdLine)
        {
            StringBuilder sb = new StringBuilder();
            bool openingQuoteCharSeen = false;
            foreach (char ch in exeCmdLine.ToCharArray())
            {
                if (ch == '"')
                {
                    if (!openingQuoteCharSeen)
                    {
                        openingQuoteCharSeen = true;
                        continue;
                    }

                    openingQuoteCharSeen = false;
                    return sb.ToString(); //end of exe cmd in cmdLine
                }
                else if (ch == ' ')
                {
                    if (!openingQuoteCharSeen) //not a quoted string, cmdline switch break
                    {
                        return sb.ToString();
                    }
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        private static void CheckWXPermissionOnFolders(HashSet<string> exePaths)
        {
            var currUser = WindowsIdentity.GetCurrent();

            foreach (var exePath in exePaths)
            {
                if (CanWriteExecuteInFolder(exePath, currUser.Name))
                {
                    Debug.WriteLine($"Writable folder {exePath} found");
                    //if (CanExecuteInFolder(exePath))
                    {
                        Console.WriteLine(exePath);
                    }
                }
                else //No permission
                {
                    Debug.WriteLine("No write perm: " + exePath);
                }
            }
        }

        private static void CheckRunningProcesses(HashSet<string> exePaths)
        {
            var procs = Process.GetProcesses();
            Debug.WriteLine("Found {0} running processes", procs.Length);

            foreach (var proc in procs)
            {
                try
                {
                    var mods = proc.Modules;

                    for (int i = 0; i < mods.Count; i++)
                    {
                        var path = mods[i].FileName;

                        if (String.IsNullOrEmpty(path))
                        {
                            continue;
                        }
                        exePaths.Add(Path.GetDirectoryName(path));
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to enumerate modules in **{proc.ProcessName}* due to {ex.Message}");
                }
            }
        }

        /*
                static bool CanExecuteInFolder(string folder)
                {
                    var testExePath = Path.Combine(folder, TEST_EXE_NAME);
                    bool allowed = false;

                    try
                    {
                        //write a test bat file
                        File.WriteAllText(testExePath, "@echo hello world" + Environment.NewLine);
                        using Process proc = new();
                        proc.StartInfo = new ProcessStartInfo(testExePath)
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = folder
                        };

                        if (proc.Start() && proc.WaitForExit(5 * 1000))
                        {
                            allowed = (proc.ExitCode == 0);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Failed to execute in {folder} due to {e}");
                    }

                    if (File.Exists(testExePath))
                    {
                        File.Delete(testExePath);
                    }

                    return allowed;
                }
        */

        private static bool CanWriteExecuteInFolder(string folderPath, string userPrincipal)
        {
            DirectoryInfo dirInfo = new(folderPath);
            bool allowed = false;

            try
            {
                DirectorySecurity dirSec = dirInfo.GetAccessControl();
                AuthorizationRuleCollection rules = dirSec.GetAccessRules(true, true, typeof(NTAccount));

                foreach (AuthorizationRule rule in rules)
                {
                    if (rule.IdentityReference.Value.Equals(userPrincipal, StringComparison.CurrentCultureIgnoreCase))
                    {
                        FileSystemAccessRule fsRule = (FileSystemAccessRule)rule;

                        if ((fsRule.FileSystemRights & FileSystemRights.WriteData) == FileSystemRights.WriteData
                            && (fsRule.AccessControlType == AccessControlType.Allow))
                        {
                            //check execute
                            if ((FileSystemRights.ExecuteFile & fsRule.FileSystemRights) == FileSystemRights.ExecuteFile
                                && (fsRule.AccessControlType == AccessControlType.Allow))
                            {
                                allowed = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch (PrivilegeNotHeldException)
            {
                Debug.WriteLine($"Failed to read permissions on {folderPath}");
            }

            Debug.Close();
            Trace.Close();

            return allowed;
        }
    }
}
#pragma warning restore CS8604 // Possible null reference argument.
