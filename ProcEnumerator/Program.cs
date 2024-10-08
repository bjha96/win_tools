/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/win_tools.
*/


namespace ProcEnumerator;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

/// <summary>
/// Finds running processes and checks the host directories of these
/// executables and loaded assemblies for write and execute permission 
/// for the current user. Can be used for checking loose folder hardening 
/// for services/processes running with system privileges.
/// </summary>
#pragma warning disable CS8604 // Possible null reference argument.
[SupportedOSPlatform("windows")]
class Program
{
    const string TEST_EXE_NAME = "~$psTest.bat";

    static void Main(string[] args)
    {
        //Trace.Listeners affects .Net core also
        //Trace.Listeners.Add(new ConsoleTraceListener());
        Debug.AutoFlush = true;
        //Trace.AutoFlush = true;

        var exePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                Debug.WriteLine("Failed to enumerate modules in process {0} due to {1}", proc.ProcessName, ex.Message);
            }
        }

        Debug.WriteLine("Found {0} unique exePaths", exePaths.Count);

        var currUser = WindowsIdentity.GetCurrent();

        foreach (var exePath in exePaths)
        {
            if (CanWriteInFolder(exePath, currUser.Name))
            {
                Debug.WriteLine($"Writable folder {exePath} found");
                if(CanExecuteInFolder(exePath))
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

    private static bool CanWriteInFolder(string folderPath, string userPrincipal)
    {
        DirectoryInfo dirInfo = new(folderPath);

        try
        {            
            DirectorySecurity dirSec = dirInfo.GetAccessControl();
         
            AuthorizationRuleCollection rules = dirSec.GetAccessRules(true, true, typeof(NTAccount));

            foreach (AuthorizationRule rule in rules)
            {
                if (rule.IdentityReference.Value.Equals(userPrincipal, StringComparison.InvariantCultureIgnoreCase))
                {
                    FileSystemAccessRule fsRule = (FileSystemAccessRule)rule;

                    if ((fsRule.FileSystemRights & FileSystemRights.WriteData) > 0)
                    {
                        return true;
                    }
                }
            }
        }
        catch (PrivilegeNotHeldException)
        {
            Debug.WriteLine("Failed to read permissions on " + folderPath);
        }

        Debug.Close();
        Trace.Close();

        return false;
    }
}
#pragma warning restore CS8604 // Possible null reference argument.
