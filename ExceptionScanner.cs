
namespace serial_monitor
{
    class ExceptionScanner
    {
        const string exceptionseperatorEsp8266 = @"--------------- CUT HERE FOR EXCEPTION DECODER ---------------";
        const string exceptionseperatorEsp32 = @"Guru Meditation Error:";
        const string exceptionseperatorEsp32end = @"Rebooting...";
        /* Liebe Kinder, was lernen wir?
           Wenn man sowas baut, dann so, dass es einfach maschinenauswertbar ist. 
           Also vernünftige Anfang-Ende-Kenner, die eineindeutig zu identifizieren sind.
           Alles Neuland. 2025
        */

        public string checkAndSeparateExceptionText(string monitor)
        {
            // esp8266
            int i = monitor.LastIndexOf(exceptionseperatorEsp8266);
            if (i > -1)
            {   // hinten wegschneiden
                monitor = monitor.Substring(0, i);
                i = monitor.LastIndexOf(exceptionseperatorEsp8266);
                if (i > -1)
                {   // hinteres Ende
                    monitor = monitor.Substring(i + exceptionseperatorEsp8266.Length);
                    if (monitor.IndexOf("Exception") > -1)
                    {
                        try
                        {
                            monitor = monitor.Trim();
                            return monitor;
                        }
                        catch
                        {
                            // dont care
                        }
                    }
                }
            }
            else
            {
                // esp32
                i = monitor.LastIndexOf(exceptionseperatorEsp32);
                if (i > -1)
                {   // vorne wegschneiden
                    monitor = monitor.Substring(i+ exceptionseperatorEsp32.Length);

                    // Ende-Kennung
                    i = monitor.IndexOf(exceptionseperatorEsp32end);
                    if (i > -1)
                    {
                        monitor = monitor.Substring(0, i).Trim();
                        return monitor;
                    }
                }
            }
            return "";
        }
    }
}

/*
 esp8266
--------------- CUT HERE FOR EXCEPTION DECODER ---------------

Exception (29):
epc1=0x40201080 epc2=0x00000000 epc3=0x00000000 excvaddr=0x00000000 depc=0x00000000

>>>stack>>>

ctx: cont
sp: 3ffffe60 end: 3fffffd0 offset: 0150
3fffffb0:  feefeffe feefeffe 3ffee54c 402019d0  
3fffffc0:  feefeffe feefeffe 3fffdab0 40100d19  
<<<stack<<<

--------------- CUT HERE FOR EXCEPTION DECODER --------------- 
 
 


esp32-s3



Build:Mar 27 2021
rst:0xc (RTC_SW_CPU_RST),boot:0x8 (SPI_FAST_FLASH_BOOT)
Saved PC:0x403798c2
SPIWP:0xee
mode:DIO, clock div:1
load:0x3fce2820,len:0x1188
load:0x403c8700,len:0x4
load:0x403c8704,len:0xbf0
load:0x403cb700,len:0x30e4
entry 0x403c88ac
Exception test
Guru Meditation Error: Core  1 panic'ed (StoreProhibited). Exception was unhandled.

Core  1 register dump:
PC      : 0x42001c9d  PS      : 0x00060330  A0      : 0x82003f55  A1      : 0x3fca2cd0  
A2      : 0x00000000  A3      : 0x0000002c  A4      : 0x0000002b  A5      : 0xffffffff  
A6      : 0xffffffff  A7      : 0x3fc97a98  A8      : 0x00000000  A9      : 0x00000013 
A10     : 0x00001388  A11     : 0x3c030120  A12     : 0x00000000  A13     : 0x3fc94704  
A14     : 0xffffffff  A15     : 0x0000000e  SAR     : 0x0000001e  EXCCAUSE: 0x0000001d  
EXCVADDR: 0x00000000  LBEG    : 0x40056f08  LEND    : 0x40056f12  LCOUNT  : 0x00000000  


Backtrace: 0x42001c9a:0x3fca2cd0 0x42003f52:0x3fca2cf0 0x4037caf2:0x3fca2d10




ELF file SHA256: daeaa4fc3

Rebooting...
ESP-ROM:esp32s3-20210327
Build:Mar 27 2021
rst:0xc (RTC_SW_CPU_RST),boot:0x8 (SPI_FAST_FLASH_BOOT)
Saved PC:0x4










 
 */