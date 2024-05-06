# WinEventCat
A simple command line tool for reading and extracting Windows events, written in C#.
Run the tool without any cmd line args to get the usage options. There are options to
read (cat), tail (and follow), filter etc., windows event file by name (lookup registry)
or location on the filesystem. This tool will be useful for parsing, searching, exporting
events by event Ids, level etc. The events can be exported to simple text file which can 
be processed further by utilities like "grep".

The goal of this project is to create a simple utility without using 3rd party libraries.
It uses standard APIs and libraries available on the vanilla windows .Net runtime.
