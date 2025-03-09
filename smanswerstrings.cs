
namespace serial_monitor
{
    internal partial class SerialMonitor : LogingBase
    {
        string helloanswer = """
        {
          "eventType": "hello",
          "message": "OK",
          "protocolVersion": 1
        }

        """;

        string describeanswer = """
            {
              "eventType": "describe",
              "message": "OK",
              "port_description": {
                "protocol": "serial",
                "configuration_parameters": {
                  "baudrate": {
                    "label": "Baudrate",
                    "type": "enum",
                    "value": [
                      "300",
                      "600",
                      "750",
                      "1200",
                      "2400",
                      "4800",
                      "9600",
                      "19200",
                      "38400",
                      "57600",
                      "115200",
                      "230400",
                      "460800",
                      "500000",
                      "921600",
                      "1000000",
                      "2000000"
                    ],
                    "selected": "9600"
                  },
                  "bits": {
                    "label": "Data bits",
                    "type": "enum",
                    "value": [
                      "5",
                      "6",
                      "7",
                      "8",
                      "9"
                    ],
                    "selected": "8"
                  },
                  "parity": {
                    "label": "Parity",
                    "type": "enum",
                    "value": [
                      "None",
                      "Even",
                      "Odd",
                      "Mark",
                      "Space"
                    ],
                    "selected": "None"
                  },
                  "stop_bits": {
                    "label": "Stop bits",
                    "type": "enum",
                    "value": [
                      "1",
                      "1.5",
                      "2"
                    ],
                    "selected": "1"
                  }
                }
              }
            }

            """;

        string configureanswer = """
            {
              "eventType": "configure",
              "message": "OK"
            }

            """;

        string openanswer = """
            {
              "eventType": "open",
              "message": "OK"
            }

            """;

        // %pp% ist seriel portname
        string openanswerunknownserialport = """
            {
              "eventType": "open",
              "error": true,
              "message": "unknown port %pp%"
            }

            """;

        string openanswerserialportgone = """
            {
              "eventType": "port_closed",
              "message": "serial port disappeared!",
              "error": true
            }

            """;

        string openanswertcpgone = """
            {
              "eventType": "port_closed",
              "message": "lost TCP/IP connection with the client!",
              "error": true
            }

            """;

        string closeanswer = """
            {
              "eventType": "close",
              "message": "OK"
            }

            """;

        string quitanswer = """
            {
              "eventType": "quit",
              "message": "OK"
            }

            """;

        // %pp% is unknown command
        string Idontkknowwhatanswer = """
            {
              "eventType": "command_error",
              "error": true,
              "message": "Command %pp% not supported"
            }

            """;

    }
}
