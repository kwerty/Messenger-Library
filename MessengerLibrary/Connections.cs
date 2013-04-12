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

namespace MessengerLibrary.Connections
{

    public interface IConnection : IDisposable
    {
        Task ConnectAsync(IEndPoint remoteEP);
        Task SendAsync(byte[] buffer, int offset, int size);
        Task<int> ReceiveAsync(byte[] buffer, int offset, int size);
        void Close();
        event EventHandler<ConnectionErrorEventArgs> Error;
    }

    public interface IEndPoint
    {

    }

    public class ConnectionStream : Stream
    {

        IConnection connection;

        public ConnectionStream(IConnection connection)
        {
            this.connection = connection;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return connection.SendAsync(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return connection.ReceiveAsync(buffer, offset, count);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
    }

    public abstract class ConnectionErrorException : Exception
    {

        internal ConnectionErrorException(string message)
            : base(message)
        {
        }

        internal ConnectionErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

    }

    public class ConnectionErrorEventArgs : EventArgs
    {

        internal ConnectionErrorException Exception { get; private set; }

        internal ConnectionErrorEventArgs(ConnectionErrorException exception)
        {
            Exception = exception;
        }
    }

    public class SocketConnection : IConnection
    {

        Socket socket;
        int eventRaised;
        bool closed;

        public async Task ConnectAsync(IEndPoint remoteEP)
        {

            SocketEndPoint socketEndPoint = remoteEP as SocketEndPoint;

            if (socketEndPoint == null)
                throw new ArgumentException("remoteEP must be of type SocketEndPoint", "remoteEP");

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                await Task.Factory.FromAsync(socket.BeginConnect(socketEndPoint.Address, socketEndPoint.Port, null, null), socket.EndConnect);
            }
            catch (SocketException ex)
            {
                throw new SocketConnectionException(ex);
            }
            catch (ObjectDisposedException)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

        }

        public async Task SendAsync(byte[] data, int offset, int size)
        {

            try
            {

                int remains = size;

                while (remains > 0)
                {

                    int sent = await Task.Factory.FromAsync<int>(socket.BeginSend(data, offset + (size - remains), remains, SocketFlags.None, null, null), socket.EndSend);

                    if (sent == 0)
                        throw new SocketConnectionException("Unexpected disconnect");

                    remains -= size;

                }

            }
            catch (SocketException ex)
            {

                ConnectionErrorException connectionException = new SocketConnectionException(ex);

                if (Interlocked.Exchange(ref eventRaised, 1) == 0)
                    OnError(new ConnectionErrorEventArgs(connectionException));

                throw new SocketConnectionException(ex);

            }
            catch (ObjectDisposedException)
            {
                throw new ObjectDisposedException(GetType().Name);
            }


        }

        public async Task<int> ReceiveAsync(byte[] data, int offset, int size)
        {

            try
            {

                int received = await Task.Factory.FromAsync<int>(socket.BeginReceive(data, offset, size, SocketFlags.None, null, null), socket.EndReceive);

                if (received == 0)
                    throw new SocketConnectionException("Unexpected disconnect");

                return received;

            }
            catch (SocketException ex)
            {

                ConnectionErrorException connectionException = new SocketConnectionException(ex);

                if (Interlocked.Exchange(ref eventRaised, 1) == 0)
                    OnError(new ConnectionErrorEventArgs(connectionException));

                throw new SocketConnectionException(ex);

            }
            catch (ObjectDisposedException)
            {
                throw new ObjectDisposedException(GetType().Name);
            }


        }

        public void Close()
        {

            if (closed)
                return;

            closed = true;

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            socket.Close();

        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public event EventHandler<ConnectionErrorEventArgs> Error;

        void OnError(ConnectionErrorEventArgs e)
        {
            EventHandler<ConnectionErrorEventArgs> handler = Error;
            if (handler != null) handler(this, e);
        }

    }

    public class SocketEndPoint : IEndPoint
    {

        public string Address { get; private set; }
        public int Port { get; private set; }

        SocketEndPoint(string address, int port)
        {
            Address = address;
            Port = port;
        }

        public override string ToString()
        {
            return String.Format("{0}:{1}", Address, Port);
        }

        public static SocketEndPoint Parse(string s)
        {
            string[] parts = s.Split(':');
            return new SocketEndPoint(parts[0], Int32.Parse(parts[1]));
        }

    }

    public class SocketConnectionException : ConnectionErrorException
    {

        internal SocketConnectionException(string message)
            : base(message)
        {
        }

        internal SocketConnectionException(SocketException innerException)
            : base("Underlying socket error: " + innerException.Message, innerException)
        {
        }


    }

}

