using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Ninja.WebSockets;
using Ninja.WebSockets.Internal;
using System.Threading;
using System.Text;
using System.Net.WebSockets;
using System.Collections.Generic;
using IDM.Message;
using System.Diagnostics;

namespace IDM
{
    static class Program
    {
        public static string CMD;
        static void Main(string[] args)
        {
            Console.WriteLine("Running As: " + Environment.UserName);

            if (!Environment.UserName.Equals("root", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("YOU MUST RUN AS ROOT");
                return;
            }

            var SettingsPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            SettingsPath = Path.Combine(SettingsPath, "IDMan");

            if (!Directory.Exists(SettingsPath))
                Directory.CreateDirectory(SettingsPath);

            SettingsPath = Path.Combine(SettingsPath, "IDM.conf");

            if (!File.Exists(SettingsPath))
            {
                Console.WriteLine("Welcome! Before anything you must write the command line template to the IDM execute when you download anything.");
                Console.WriteLine("You can set this variables:");

                Console.WriteLine("UA = UserAgent");
                Console.WriteLine("URL = Download URL");
                Console.WriteLine("ORI = Origin URL");
                Console.WriteLine("REF = Referrer URL");
                Console.WriteLine("COK = Cookies String");

                bool OK = false;
                while (!OK)
                {
                    Console.WriteLine("Type the command line:");
                    CMD = Console.ReadLine();

                    var Color = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("This is how look like your command:");
                    Console.WriteLine(SetVariables(CMD, "My_User_Agent", "The_Referrer", "The_Origin", "The_Cookies", "The_URL"));
                    Console.ForegroundColor = Color;


                    Console.WriteLine("Are you sure? Y/N");
                    switch (Console.ReadKey().KeyChar)
                    {
                        case 'Y':
                        case 'y':
                            OK = true;
                            break;
                    }

                    Console.WriteLine();
                }

                File.WriteAllText(SettingsPath, CMD);
                Console.WriteLine("You can change it at anytime at " + SettingsPath);
            }
            else
                CMD = File.ReadAllText(SettingsPath);

            Console.WriteLine("Service Started!");

            var WSFactory = new WebSocketServerFactory();
            IBufferPool bufferPool = new BufferPool();
            Func<MemoryStream> bufferFactory = bufferPool.GetBuffer;


            var Tcp = new TcpListener(IPAddress.Any, 1001);
            Tcp.Start();

#if DEBUG
            using (Stream PIPE = File.Open("/tmp/idm", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
#endif
                while (true)
                {
                    var Client = Tcp.AcceptTcpClient();

                    var WSContext = WSFactory.ReadHttpHeaderFromStreamAsync(Client.GetStream()).Result;
                    if (WSContext.IsWebSocketRequest)
                    {
                        Console.WriteLine("Header: " + WSContext.HttpHeader);
                        Console.WriteLine("Path: " + WSContext.Path);
                        foreach (var Protocol in WSContext.WebSocketRequestedProtocols)
                        {
                            if (Protocol != "plugin.v3.internetdownloadmanager.com")
                                throw new Exception($"Unk IDM Protocol: {Protocol}, Report this bug in the github.");
                            Console.WriteLine("Protocol: " + Protocol);
                        }

                        WSContext.WSHandshake(WSContext.WebSocketRequestedProtocols.First());

                        WebSocket WS = new WebSocketImplementation(Guid.NewGuid(), bufferFactory, WSContext.Stream, TimeSpan.FromMinutes(1), null, false, false, null);
                        var MSGHandler = new MessageHandler(WS);

                        List<byte> Received = new List<byte>();
                        byte[] Buffer = new byte[2048];

                        while (WS.State == WebSocketState.Open)
                        {
                            WebSocketReceiveResult Result = null;

                            while (true)
                            {
                                Result = WS.ReceiveAsync(Buffer, CancellationToken.None).Result;
                                if (Result.Count > 0)
                                    Received.AddRange(Buffer.Take(Result.Count));
                                if (Result.EndOfMessage)
                                    break;
                            }

                            Console.WriteLine("Type: " + Result.MessageType + " | Len: " + Received.Count);
#if DEBUG
                            PIPE.Write(Received.ToArray(), 0, Received.Count);
                            PIPE.Flush();
#endif

                            var Message = Encoding.UTF8.GetString(Received.ToArray());
                            Received.Clear();

                            MSGHandler.ProcessMessage(Message);
                        }
                    }
                    Client.Client.Close();
                    Client.Close();
                }

        }

        static void WSHandshake(this WebSocketHttpContext Context, string subProtocol = null)
        {
            const string WSHeaderKey = "Sec-WebSocket-Key:";
            var Index = Context.HttpHeader.IndexOf(WSHeaderKey);
            if (Index < 0)
                throw new WebException("Missing Handshake Key");
            var HS = Context.HttpHeader.Substring(Index + WSHeaderKey.Length);
            HS = HS.Split('\n')[0].Trim();

            var WSAccept = HttpHelper.ComputeSocketAcceptString(HS);


            string response = ("HTTP/1.1 101 Switching Protocols\r\n"
                             + "Connection: Upgrade\r\n"
                             + "Upgrade: websocket\r\n"
                             + (subProtocol != null ? $"Sec-WebSocket-Protocol: {subProtocol}\r\n" : "")
                             + $"Sec-WebSocket-Accept: {WSAccept}\r\n\r\n");

            var Buffer = Encoding.UTF8.GetBytes(response);
            Context.Stream.Write(Buffer, 0, Buffer.Length);
            Context.Stream.Flush();
        }

        public static string ValueToString<T>(this T Enum) where T : Enum
        {
            int Value = (int)(object)Enum;
            return Value.ToString();
        }

        public static string SetVariables(string cmd, string UserAgent, string Referrer, string Origin, string Cookies, string URL)
        {
            return cmd
                .Replace("UA", UserAgent)
                .Replace("URL", URL)
                .Replace("ORI", Origin)
                .Replace("REF", Referrer)
                .Replace("COK", Cookies);
        }
        public static string Bash(this string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }
}
