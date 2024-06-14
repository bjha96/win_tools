/*
* Author: Bimalesh Jha (46f6780b4f1beba64cf09a4fee5f0d657bc55a37c3a8feaba3023a5ac2f36d87).
* You may use, modify and distribute this code under the Apache License V2 (https://www.apache.org/licenses/LICENSE-2.0).
* This file was originally hosted at https://github.com/bjha96/win_tools.
*/

///
/// Simple program to find writable and executable folders.
/// It creates a temp batch file in folder and then tests it can be
/// executed by the current process user.
/// USAGE Notes:
/// Use without any cmd line args to scan the current folder (work dir).
/// Use cmd line arg -a to scan all drives.
/// Use cmd line <folder path> to scan only the target <folder path>.
///

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace FSTest;


class ThreadWorker
{
    const string TEST_EXE_NAME = "~$fsTest.bat";

    public static void RunTest(ConcurrentQueue<string> folderPathsQueue)
    {
        Trace.TraceInformation($"RunTest started: {Thread.CurrentThread.Name}");

        if (folderPathsQueue == null)
        {
            Trace.TraceWarning("null work queue!");
            return;
        }

        while(folderPathsQueue.IsEmpty) //folder traversal not started yet
        {
            Trace.TraceInformation($"Folder queue is empty for worker {Thread.CurrentThread.Name}!");
            Thread.Sleep(500);
        }

        while (folderPathsQueue.TryDequeue(out string? folderPath))
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPathsQueue.Enqueue(folderPath); //put back for other workers to see
                Trace.TraceInformation($"End of work: {Thread.CurrentThread.Name}");
                break;
            }

            CheckWriteExecute(folderPath);
        }
    }

    static void CheckWriteExecute(string folderPath)
    {
        Trace.TraceInformation($"Checking w/x in {folderPath}");

        if (CanWriteInFolder(folderPath, out string tmpExePath))
        {
            if (CanExecute(tmpExePath))
            {
                string? tmpFolderPath = Path.GetDirectoryName(tmpExePath);
                Trace.TraceInformation($"W/X possible at {tmpFolderPath}");
                if (!string.IsNullOrEmpty(tmpFolderPath))
                {
                    Console.WriteLine(tmpFolderPath);
                }
            }

            File.Delete(tmpExePath); //cleanup
            return;
        }

        static bool CanWriteInFolder(string folder, out string testExePath)
        {
            testExePath = string.Empty;

            if (!string.IsNullOrEmpty(folder))
            {
                testExePath = Path.Combine(folder, TEST_EXE_NAME);

                try
                {
                    //write test bat file
                    File.WriteAllText(testExePath, "@echo hello world" + Environment.NewLine);
                    return true;
                }
                catch(UnauthorizedAccessException)
                {
                    Trace.TraceWarning($"Write access denied in {folder}");
                }
                catch (Exception e)
                {
                    Trace.TraceWarning($"Failed to write in {folder} due to {e}");
                }
            }

            return false;
        }


        static bool CanExecute(string testExePath)
        {
            if (!string.IsNullOrEmpty(testExePath))
            {
                try
                {
                    using Process proc = new();
                    proc.StartInfo = new ProcessStartInfo(testExePath)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(testExePath)
                    };

                    if (proc.Start() && proc.WaitForExit(5 * 1000))
                    {
                        return (proc.ExitCode == 0);
                    }
                }
                catch(UnauthorizedAccessException)
                {
                    Trace.TraceWarning($"Execute access denied on {testExePath}");
                }
                catch (Exception e)
                {
                    Trace.TraceWarning($"Failed to execute {testExePath} due to {e}");
                }
            }

            return false;
        }
    }
}


[SupportedOSPlatform("windows")]
class Program
{
    static void Main(string[] args)
    {
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
        Trace.AutoFlush = true;

        ConcurrentQueue<string> foldersQueue = new();
       
        Thread[] workers = new Thread[2];
        for (int i = 0; i < workers.Length; i++)
        {
            workers[i] = new(() => ThreadWorker.RunTest(foldersQueue))
            {
                Name = $"worker{i + 1}"
            };
            workers[i].Start();
        }

        IList<string> rootFolders = new List<string>();

        if (args.Length == 0)
        {
            rootFolders.Add(Environment.CurrentDirectory);
        }
        else if ("-a".Equals(args[0], StringComparison.InvariantCultureIgnoreCase))
        {
            Trace.TraceInformation("Scanning all drives and folders...");

            foreach (var di in DriveInfo.GetDrives())
            {
                if (di.DriveType == DriveType.Fixed)
                {
                    rootFolders.Add(di.RootDirectory.FullName);
                }
            }
        }
        else //Scan the given folder
        {
            rootFolders.Add(args[0]);
        }

        foreach (var rootFolder in rootFolders)
        {
            try
            {
                WalkFolders(rootFolder, foldersQueue);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to scan {rootFolder} due to {ex}");
            }
        }

        foldersQueue.Enqueue(string.Empty); //EOF marker for thread to exit

        for (int i = 0; i < workers.Length; i++)
        {
            workers[i].Join();
        }
    }

    private static void WalkFolders(string rootDir, ConcurrentQueue<string> foldersQueue)
    {
        foldersQueue.Enqueue(rootDir);
        Trace.TraceInformation($"Enqueued {rootDir}");

        try
        {
            foreach (var folderPath in Directory.GetDirectories(rootDir))
            {
                WalkFolders(folderPath, foldersQueue);
            }
        }
        catch (Exception e)
        {
            Trace.TraceWarning($"Failed to scan folder {e.Message}");
        }
    }
}
