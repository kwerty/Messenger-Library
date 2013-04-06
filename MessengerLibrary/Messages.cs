using System;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MessengerLibrary
{
    public class Message
    {

        public Message()
        {
            Headers = new Dictionary<String, String>();
            Headers.Add("MIME-Version", "1.0");
            Headers.Add("Content-Type", "text/plain; charset=UTF-8");
            Body = new byte[0];
        }

        internal Message(byte[] bytes)
        {

            byte[] body = null;
            byte[] hBytes = null;
            byte[] separator = new byte[] { 13, 10, 13, 10 };

            if (!ByteExt.Split(bytes, separator, out hBytes, out body))
                throw new InvalidOperationException();

            string hStr = Encoding.UTF8.GetString(hBytes);

            Headers = hStr.Split("\r\n")
                .Select(h => h.Split(": "))
                .ToDictionary(h => h[0], h => h[1]);

            Body = body;

        }

        public string ContentType {
            get
            {
                return Headers["Content-Type"];
            }
            set
            {
                Headers["Content-Type"] = value;
            }
        }

        public Dictionary<String, String> Headers { get; private set; }

        public byte[] Body { get; set; }

        internal byte[] GetBytes()
        {

            StringBuilder b = new StringBuilder();

            foreach (var h in Headers)
                b.AppendFormat("{0}: {1}", h.Key, h.Value).AppendLine();

            b.AppendLine();

            string hStr = b.ToString();

            byte[] hBytes = Encoding.UTF8.GetBytes(hStr);

            return ByteExt.Concat(hBytes, Body);

        }

    }

    public class MessageFormatter
    {

        public MessageFormatter()
        {
            Font = "Microsoft Sans Serif";
        }

        public String Font { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Strikethrough { get; set; }
        public Color Color { get; set; }

        public void SetRandomColor()
        {
            Random random = new Random();
            Color = Color.FromArgb(random.Next(255), random.Next(255), random.Next(255));
        }

        static public void CopyFormat(Message source, Message dest)
        {

            if (source.ContentType != "text/plain; charset=UTF-8" ||
                (dest.ContentType != "text/plain; charset=UTF-8"))
                throw new InvalidOperationException();

            if (!source.Headers.ContainsKey("X-MMS-IM-Format"))
                return;

            dest.Headers["X-MMS-IM-Format"] = source.Headers["X-MMS-IM-Format"];
        }


        public void ApplyFormat(Message message)
        {

            if (message.ContentType != "text/plain; charset=UTF-8")
                throw new InvalidOperationException();

            Dictionary<string, string> args = new Dictionary<string, string>();

            args.Add("FN", Uri.EscapeDataString(Font));
            args.Add("EF", (Bold ? "B" : null) + (Italic ? "I" : null) + (Underline ? "U" : null) + (Strikethrough ? "S" : null));
            args.Add("CO", Color.B.ToString("X2") + Color.G.ToString("X2") + Color.R.ToString("X2"));
            args.Add("CS", "0");
            args.Add("PF", "22");

            message.Headers["X-MMS-IM-Format"] = String.Join("; ", args.Select(m => String.Format("{0}={1}", m.Key, m.Value)).ToArray());

        }
        public void CopyFormat(Message message)
        {

            if (message.ContentType != "text/plain; charset=UTF-8")
                throw new InvalidOperationException();

            if (!message.Headers.ContainsKey("X-MMS-IM-Format"))
                return;

            string fHeader = message.Headers["X-MMS-IM-Format"];

            var fArgs = fHeader.Split("; ")
                .Select(s => s.Split('='))
                .ToDictionary(s => s[0], s => s[1]);

            if (fArgs.ContainsKey("FN"))
                Font = Uri.UnescapeDataString(fArgs["FN"]);

            if (fArgs.ContainsKey("EF"))
            {
                Bold = fArgs["EF"].Contains("B");
                Italic = fArgs["EF"].Contains("I");
                Strikethrough = fArgs["EF"].Contains("S");
                Underline = fArgs["EF"].Contains("U");
            }

            if (fArgs.ContainsKey("CO") && fArgs["CO"] != "0")
            {

                string[] rgb = (from Match m in Regex.Matches(fArgs["CO"], @"[\d\w]{2}") select m.Value)
                    .Reverse()
                    .ToArray();

                Color = ColorTranslator.FromHtml("#" + String.Concat(rgb));
            }
                    

        }



    }

}
