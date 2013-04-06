using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.IO;
using MessengerLibrary.MSNP;
using MessengerLibrary.IO;
using MessengerLibrary.Connections;
using MessengerLibrary.Threading;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Linq;
using System.Diagnostics;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;

namespace MessengerLibrary
{


    public class IMSession : IDisposable
    {

        MessengerClient client;

        AsyncReaderWriterLock @lock = new AsyncReaderWriterLock();
        bool connected;
        bool closed;

        IConnection connection;
        ConnectionStream stream;
        CommandReader reader;
        CommandWriter writer;
        ResponseTracker responseTracker;
        IConnectableObservable<Command> commands;
        IDisposable commandsDisposable;

        public Users Users { get; private set; }


        internal IMSession(MessengerClient client)
        {

            this.client = client;

            Users = new Users();
        }

        internal async Task ConnectByInvitation(IMSessionInvitation invitation)
        {

            await @lock.WriterLockAsync();

            try
            {

                await InitConnection(invitation.endPoint);

                AnswerCommand answer = new AnswerCommand(client.LocalUser.LoginName, invitation.authString, invitation.sessionId);

                var getRoster = commands
                    .OfType<UserRosterCommand>()
                    .Where(c => c.TrId == answer.TrId)
                    .TakeUntil(c => c.CurrentIndex == c.TotalCount)
                    .Timeout(client.defaultTimeout)
                    .ToArray()
                    .ToTask();

                await responseTracker.GetResponseAsync<AnswerCommand>(answer, client.defaultTimeout);

                var roster = await getRoster;

                foreach (UserRosterCommand cmd in roster)
                {
                    var user = client.GetUserInner(cmd.LoginName);
                    user.UpdateNicknameFromIM(cmd.NickName);
                    Users.AddUserInner(user);
                }

                EnterConnectedState();

            }
            finally
            {
                @lock.WriterRelease();
            }

        }

        public async Task InviteUserAsync(User user)
        {

            //todo: we don't always need the writer lock
            //AsyncReaderWriterLock needs to be modified to allow upgrades

            await @lock.WriterLockAsync();

            try
            {

                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                if (!connected)
                {

                    TransferCommand response = await client.GetSwitchboard();

                    await InitConnection(SocketEndPoint.Parse(response.Host));

                    Command authCmd = new AuthenticateIMCommand(client.LocalUser.LoginName, response.Host2OrSessionID);
                    AuthenticateIMCommand authResponse = await responseTracker.GetResponseAsync<AuthenticateIMCommand>(authCmd, client.defaultTimeout);

                    EnterConnectedState();

                }

                if (Users.Contains(user))
                    throw new UserAlreadyExistsException("User is already a participant of this IMSession.");

                CallUserCommand call = new CallUserCommand(user.LoginName);
                await responseTracker.GetResponseAsync<CallUserCommand>(call, client.defaultTimeout);

            }
            catch (ServerErrorException ex)
            {
                if (ex.ServerError == ServerError.PrincipalNotOnline)
                    throw new UserNotOnlineException();

                throw;

            }
            finally
            {
                @lock.WriterRelease();
            }

        }

        public async Task SendMessageAsync(Message message, MessageOption option = MessageOption.Acknowledgement)
        {

            if (message.Body != null && message.Body.Length > 1664)
                throw new ArgumentException("The message body is too long.", "message");

            SendMessageCommand cmd = null;
            Task<Command> getResponseAsync = null;

            await @lock.ReaderLockAsync();

            try
            {

                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                if (Users.Count == 0)
                    throw new NoParticpantsException("There are no users to send a message to.");

                cmd = new SendMessageCommand(MessageOptionToString(option), message.GetBytes());

                if (option == MessageOption.NoAcknoweldgement)
                {
                    await writer.WriteCommandAsync(cmd);
                    return;
                }

                else if (option == MessageOption.Acknowledgement || option == MessageOption.Data)
                {

                    var response = await responseTracker.GetResponseAsync(cmd, new Type[] { typeof(AcknowledgementCommand), typeof(NegativeAcknowledgementCommand) }, client.defaultTimeout);

                    if (response is NegativeAcknowledgementCommand)
                        throw new MessageDeliveryFailedException();

                    return;

                }
                else if (option == MessageOption.NegativeAcknowledgementOnly)
                {
                    getResponseAsync = responseTracker.GetResponseAsync(cmd, new Type[] { typeof(NegativeAcknowledgementCommand) }, TimeSpan.FromMinutes(2));
                }

            }
            finally
            {
                @lock.ReaderRelease();
            }

            //we'll only get to this point if the MessageOption is NegativeAcknowledgementOnly

            try
            {

                var response = (NegativeAcknowledgementCommand)await getResponseAsync;

                throw new MessageDeliveryFailedException();

            }
            catch (TimeoutException)
            {
                //a timeout here means no NAK was received in two minutes, therefore the message can be considered delivered
                return;
            }
            catch (ObjectDisposedException)
            {
                //it's possible the connection might be closed while we wait
                throw new MessageDeliveryFailedException();
            }



        }

        public async Task DisconnectAsync()
        {

            await @lock.WriterLockAsync();

            try
            {

                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                DisconnectInner();


            }
            finally
            {
                @lock.WriterRelease();
            }

        }

        void DisconnectInner()
        {

            if (!connected)
                return;

            connection.Error -= connection_Error;

            commandsDisposable.Dispose();

            connection.Close();

            reader = null;
            writer = null;
            connection = null;
            responseTracker = null;

            List<UserEventArgs> events = new List<UserEventArgs>();

            foreach (User user in Users.ToList())
            {
                Users.RemoveUserInner(user);
                events.Add(new UserEventArgs(user));
            }

            connected = false;

            foreach (UserEventArgs e in events)
                OnUserParted(e);

        }

        async void connection_Error(object sender, ConnectionErrorEventArgs e)
        {
            await @lock.WriterLockAsync();

            try
            {

                if (connection == sender)
                    DisconnectInner();

            }
            finally
            {
                @lock.WriterRelease();
            }
        }

        async Task InitConnection(IEndPoint endPoint)
        {

            connection = new SocketConnection();

            await connection.ConnectAsync(endPoint);

            stream = new ConnectionStream(connection);

            writer = new CommandWriter(stream);
            reader = new CommandReader(stream, new Dictionary<string, Type> {
                { "MSG", typeof(MessageCommand) },
                { "ANS", typeof(AnswerCommand) },
                { "CAL", typeof(CallUserCommand) },
                { "USR", typeof(AuthenticateIMCommand) },
                { "IRO", typeof(UserRosterCommand) },
                { "JOI", typeof(UserJoinedCommand) },
                { "BYE", typeof(UserPartedCommand) },
                { "ACK", typeof(AcknowledgementCommand) },
                { "NAK", typeof(NegativeAcknowledgementCommand) },
            });

            commands = reader.GetReadObservable().Publish();
            responseTracker = new ResponseTracker(writer, commands);

            commandsDisposable = commands.Connect();

        }

        void EnterConnectedState()
        {

            connection.Error += connection_Error;

            var commandsSafe = commands
                .Catch<Command, ConnectionErrorException>(tx => Observable.Empty<Command>());

            commandsSafe.OfType<MessageCommand>().Subscribe(cmd => HandleMessages(cmd, connection));
            commandsSafe.OfType<UserJoinedCommand>().Subscribe(cmd => HandleJoiningUsers(cmd, connection));
            commandsSafe.OfType<UserPartedCommand>().Subscribe(cmd => HandlePartingUsers(cmd, connection));

            connected = true;

        }

        async void HandleMessages(MessageCommand messageCommand, IConnection connection)
        {

            await @lock.ReaderLockAsync();

            try
            {

                if (closed)
                    return;

                User user = client.GetUserInner(messageCommand.Sender);

                Message message = new Message(messageCommand.Payload);

                OnMessageReceived(new MessageEventArgs(user, message));

            }
            finally
            {
                @lock.ReaderRelease();
            }

        }

        async void HandleJoiningUsers(UserJoinedCommand userJoinedCommand, IConnection connection)
        {

            await @lock.ReaderLockAsync();

            try
            {

                if (this.connection != connection)
                    return;

                User user = client.GetUserInner(userJoinedCommand.LoginName);

                user.UpdateNicknameFromIM(userJoinedCommand.NickName);

                //server sometimes sends both IRO and JOI
                if (Users.Contains(user))
                    return;

                Users.AddUserInner(user);

                OnUserJoined(new UserEventArgs(user));

            }
            finally
            {
                @lock.ReaderRelease();
            }


        }

        async void HandlePartingUsers(UserPartedCommand userPartedCommand, IConnection connection)
        {

            await @lock.ReaderLockAsync();

            try
            {

                if (this.connection != connection)
                    return;

                User user = client.GetUserInner(userPartedCommand.LoginName);

                Users.RemoveUserInner(user);

                OnUserParted(new UserEventArgs(user));

            }
            finally
            {
                @lock.ReaderRelease();
            }

        }

        public async void Close()
        {

            await @lock.WriterLockAsync();

            try
            {

                if (closed)
                    return;

                DisconnectInner();

                Users = null;

                closed = true;

                OnClosed();

            }
            finally
            {
                @lock.WriterRelease();
            }

        }

        void IDisposable.Dispose()
        {
            Close();
        }

        static string MessageOptionToString(MessageOption option)
        {
            switch (option)
            {
                case MessageOption.Acknowledgement:
                    return "A";
                case MessageOption.NegativeAcknowledgementOnly:
                    return "N";
                case MessageOption.Data:
                    return "D";
                case MessageOption.NoAcknoweldgement:
                    return "U";
                default:
                    throw new NotSupportedException();
            }
        }

        public event EventHandler<UserEventArgs> UserJoined;

        public event EventHandler<UserEventArgs> UserParted;

        public event EventHandler<MessageEventArgs> MessageReceived;

        public event EventHandler Closed;

        void OnClosed()
        {
            EventHandler handler = Closed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        void OnUserJoined(UserEventArgs e)
        {
            EventHandler<UserEventArgs> handler = UserJoined;
            if (handler != null) handler(this, e);
        }

        void OnUserParted(UserEventArgs e)
        {
            EventHandler<UserEventArgs> handler = UserParted;
            if (handler != null) handler(this, e);
        }

        void OnMessageReceived(MessageEventArgs e)
        {
            EventHandler<MessageEventArgs> handler = MessageReceived;
            if (handler != null) handler(this, e);
        }

    }


    public class IMSessions : ReadOnlyCollection<IMSession>
    {

        internal IMSessions()
            : base(new List<IMSession>())
        {
        }

        internal void AddIMSessionInner(IMSession imSession)
        {
            Items.Add(imSession);
        }

        internal void RemoveIMSessionInner(IMSession imSession)
        {
            Items.Remove(imSession);
        }

    }

    public class IMSessionInvitation
    {

        internal int accepted;
        internal SocketEndPoint endPoint;
        internal string sessionId;
        internal string authString;

        public User InvitingUser { get; internal set; }

        internal IMSessionInvitation(User invitingUser, SocketEndPoint endPoint, string sessionId, string authString)
        {
            InvitingUser = invitingUser;

            this.endPoint = endPoint;
            this.sessionId = sessionId;
            this.authString = authString;
        }


    }

}
