using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;

namespace IDM.Message
{
    public class MessageHandler
    {
        static readonly Dictionary<MSGField, MSGFieldType> FieldType = new Dictionary<MSGField, MSGFieldType>(){
           { MSGField.URL,        MSGFieldType.String },
           { MSGField.Origin,     MSGFieldType.String },
           { MSGField.Referrer,   MSGFieldType.String },
           { MSGField.Cookies,    MSGFieldType.String },
           { MSGField.UserAgent,  MSGFieldType.String }
        };
        static readonly Dictionary<string, MSGField> FieldMap = new Dictionary<string, MSGField>(){
           { "6",  MSGField.URL },
           { "7",  MSGField.Origin},
           { "50", MSGField.Referrer },
           { "51", MSGField.Cookies },
           { "54", MSGField.UserAgent }
        };

        WebSocket WS;
        public MessageHandler(WebSocket Socket)
        {
            WS = Socket;
        }

        public void ProcessMessage(string Message)
        {
            Console.WriteLine("Processing MSG: " + Message);
            var Paramters = Message.Split(',');
            var Header = Paramters.First().Split('#');
            var Fields = MergeInvalidFields(Paramters.Skip(1).ToArray());

            if (!Header[0].Equals("MSG", StringComparison.Ordinal))
            {
                Console.WriteLine("Unexpected Message Type");
                return;
            }

            if (Header[(int)MSGHeader.Event].Equals(MSG.Download.ValueToString(), StringComparison.Ordinal))
            {
                Console.WriteLine("Download Event Received...");
                string URL = null, Origin = null, Referrer = null, Cookies = null, UserAgent = null;

                foreach (var FieldData in Fields)
                {
                    var Field = CastField(FieldData);
                    switch (Field)
                    {
                        case MSGField.URL:
                            URL = CastFieldString(FieldData);
                            break;
                        case MSGField.Origin:
                            Origin = CastFieldString(FieldData);
                            break;
                        case MSGField.Referrer:
                            Referrer = CastFieldString(FieldData);
                            break;
                        case MSGField.Cookies:
                            Cookies = CastFieldString(FieldData);
                            break;
                        case MSGField.UserAgent:
                            UserAgent = CastFieldString(FieldData);
                            break;
                        default:
                            break;
                    }

                }

                Program.Bash(Program.SetVariables(Program.CMD, UserAgent, Referrer, Origin, Cookies, URL));
            }
        }

        //Cool, looks like the IDM don't escape the special characters :)
        //The correct way to read is parse the MSG while reading like a stream
        //since the strings have a length prefix.
        private string[] MergeInvalidFields(string[] FieldList)
        {
            List<string> Fields = new List<string>();
            for (int i = 0; i < FieldList.Length; i++)
            {
                var FieldData = FieldList[i];
                int Index = FieldData.IndexOf('=', StringComparison.Ordinal);
                bool Invalid = Index < 0;

                if (!Invalid)
                    Invalid = !int.TryParse(FieldData.Substring(0, Index), out _);

                if (!Invalid)
                {
                    Fields.Add(FieldData);
                    continue;
                }
                Fields[Fields.Count - 1] += ',' + FieldData;
            }

            return Fields.ToArray();
        }

        private MSGField CastField(string FieldData)
        {
            int Index = FieldData.IndexOf('=', StringComparison.Ordinal);

            if (Index < 0)
                throw new Exception("Unk Message Field Format:" + FieldData);

            var ID = FieldData.Substring(0, Index);

            if (FieldMap.ContainsKey(ID))
                return FieldMap[ID];

            return MSGField.Unk;
        }

        private string CastFieldString(string FieldData)
        {
            int Index = FieldData.IndexOf('=', StringComparison.Ordinal);

            if (Index < 0)
                throw new Exception("Unk Message Field Format:" + FieldData);

            var Content = FieldData.Substring(Index + 1);

            Index = Content.IndexOf(':', StringComparison.Ordinal);

            if (Index < 0)
                throw new Exception("Unk Message Field Content Format:" + Content);

            var Len = int.Parse(Content.Substring(0, Index));
            Content = Content.Substring(Index + 1);

            if (Content.Length < Len)
                throw new Exception("Expected String Length don't Matched: " + Content);
            
            if (Content.Length > Len)
                Content = Content.Substring(0, Len);

            if (Content.StartsWith("idmfc", StringComparison.Ordinal))
            {
                Content = Content.Substring(5);
            }

            return Content;
        }
    }
}
