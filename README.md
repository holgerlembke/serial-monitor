# serial-monitor

This is a (partial?) rewrite of https://github.com/arduino/serial-monitor in C#/.NET 8.0


## Installation

This is about Windows. In theory it should work in linux and osx. I did not test it.

* locate folder of serial-monitor.exe

I found it the easiest way to open the serial monitor in the IDE. Then start the task monitor,
go to "details" tab, look up the process and right click to open the processes file path.

Do not forget to close all serial monitors in the IDE, otherwise the executable will be locked.

Path should look something like

x:\Users\xxxxxxx\AppData\Local\Arduino15\packages\builtin\tools\serial-monitor\0.14.1

* rename the old serial-monitor.exe

I advice to keep a copy of the old serial-monitor.exe. Better safe then sorry.

* unpack the release files

Just right click, extract here. Do not extract into a sub folder, you might need to move the files 
to its correct location.

In the end there should be a bunch of dll's, a serial-monitor.exe, a serial-monitor.dll and a 
platform support folder "runtimes" with lots of stuff in it.

## New Arduino Release

You need to find the new folder and copy the files from old location to new location.

## Storage

Exception files are stored at %temp%\serial-monitor
