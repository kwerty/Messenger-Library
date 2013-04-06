using System;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Web;
using System.IO;
using System.Threading.Tasks;
using MessengerLibrary.MSNP;

namespace MessengerLibrary.IO
{

    public class LineWriter
    {

        Stream stream;

        public LineWriter(Stream stream)
        {
            this.stream = stream;
        }

        public async Task WriteLineAsync(string line)
        {

            Debug.WriteLine("-> " + line);

            byte[] data = UTF8Encoding.UTF8.GetBytes(line + "\r\n");

            await stream.WriteAsync(data, 0, data.Length);

        }

        public Task WriteLineAsync(string format, params object[] arg)
        {
            return WriteLineAsync(String.Format(format, arg));
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count)
        {

            Debug.WriteLine("-> ({0} bytes)", count);

            await stream.WriteAsync(buffer, offset, count);

        }

    }

    public class LineReader
    {
        Stream stream;
        byte[] incoming;
        byte[] buffer;
        int cursor;
        int readSize = 1024;

        public LineReader(Stream stream)
        {
            this.stream = stream;
            this.buffer = new byte[0];
        }

        public async Task<bool> ReadAsync(byte[] buffer, int offset, int count)
        {

            while (true)
            {

                if (TakeFromBuffer(buffer, offset, count))
                {
                    Debug.WriteLine("<- ({0} bytes)", count);

                    return true;
                }

                if (await GetMoreData() == false)
                    return false;

            }

        }

        public bool TakeFromBuffer(byte[] dst, int dstOffset, int count)
        {

            if (buffer.Length >= count)
            {

                Buffer.BlockCopy(this.buffer, 0, dst, dstOffset, count);

                byte[] newBuffer = new byte[buffer.Length - count];
                Buffer.BlockCopy(buffer, count, newBuffer, dstOffset, buffer.Length - count);
                this.buffer = newBuffer;

                return true;

            }

            return false;

        }

        public async Task<string> ReadLineAsync()
        {
 
            while (true)
            {
                string line = GetLineFromBuffer();

                if (line != null)
                {
                    Debug.WriteLine("<- " + line);

                    return line;
                }

                if (await GetMoreData() == false)
                    return null;

            }



        }

        internal string GetLineFromBuffer()
        {

            int cr;

            while (true)
            {

                cr = Array.IndexOf(buffer, (byte)'\r', cursor);

                if (cr == -1)
                {
                    cursor = buffer.Length;
                    return null;
                }

                if (buffer.Length < (cr + 2))
                {
                    cursor = cr;
                    return null;
                }

                if (buffer[cr + 1] == (byte)'\n')
                    break;

                cursor = cr + 1;

            }

            byte[] line = new byte[cr];
            Buffer.BlockCopy(buffer, 0, line, 0, cr);

            byte[] newBuffer = new byte[buffer.Length - (cr + 2)];
            Buffer.BlockCopy(buffer, cr + 2, newBuffer, 0, buffer.Length - (cr + 2));
            buffer = newBuffer;

            cursor = 0;

            return Encoding.UTF8.GetString(line);

        }

        internal async Task<bool> GetMoreData()
        {
            incoming = new byte[readSize];

            int len = await stream.ReadAsync(incoming, 0, readSize);

            if (len == 0)
                return false;

            Array.Resize<byte>(ref buffer, buffer.Length + len);
            Buffer.BlockCopy(incoming, 0, buffer, buffer.Length - len, len);

            incoming = null;

            return true;

        }


    }

}

