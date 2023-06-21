# SysStressCLI
A simple command line tool for Windows written in C#.
This tool can be used to simulate stressing of CPU, IO and Memory on a Windows System.
Run the CLI without any cmd line args to get the usage options.

**CAUTION**

Adjusting the **priority** (via cmd line) of this process to "higher" or "highest" can starve
other processes and make the whole system unusable and the only way to recover would be to reboot
the machine. Use this option with extreme care.

**NOTE**

For similar use case on linux, one can use [stress-ng](https://manpages.ubuntu.com/manpages/bionic/man1/stress-ng.1.html).