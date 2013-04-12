using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Resources;
using System.Text;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using MessengerLibrary.MSNP;
using MessengerLibrary.IO;
using MessengerLibrary.Authentication;
using MessengerLibrary.Connections;
using MessengerLibrary.Threading;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;

using System.Diagnostics;

namespace MessengerLibrary
{

    public class MessengerClient : IDisposable 
    {

        internal AsyncReaderWriterLock @lock;
        internal bool closed;

        IConnection connection;
        ConnectionStream stream;
        CommandReader reader;
        CommandWriter writer;
        internal ResponseTracker responseTracker;
        IConnectableObservable<Command> commands;
        IDisposable commandsDisposable;

        internal Credentials credentials { get; private set; }

        public bool IsLoggedIn { get; private set; }

        Dictionary<string, WeakReference> userCache;
        DateTime lastPurge;

        public LocalUser LocalUser { get; private set; }
        public Groups Groups { get; private set; }
        public UserLists UserLists { get; private set; }
        public IMSessions IMSessions { get; private set; }

        public static readonly string AcceptInvitationsFromAllUsers = "AL";
        public static readonly string AcceptInvitationsFromAllowedUsersOnly = "BL";

        public static readonly string AddUsersAutomatically = "A";
        public static readonly string AddUsersWithPrompt = "N";

        public static readonly string InstantMessagingEnabled = "ON";
        public static readonly string InstantMessagingDisabled = "OFF";

        string syncTimeStamp1; //yyyy-MM-ddTHH:mm:ss.FFFFFFFzzzzzz
        string syncTimeStamp2;

        Dictionary<PrivacySetting, string> privacySettings { get; set; }

        internal TimeSpan defaultTimeout = TimeSpan.FromSeconds(30);

        public MessengerClient(Credentials credentials)
        {

            @lock = new AsyncReaderWriterLock();

            this.credentials = credentials;

            userCache = new Dictionary<string, WeakReference>();

            privacySettings = new Dictionary<PrivacySetting, string>();
            UserLists = new UserLists(this);
            Groups = new Groups();
            IMSessions = new IMSessions();

        }

        public async Task LoginAsync(UserStatus initialStatus = UserStatus.Online)
        {

            await @lock.WriterLockAsync();

            try
            {

                if (IsLoggedIn)
                    return;

                IConnection connection = null;
                ConnectionStream stream = null;
                CommandReader reader = null;
                CommandWriter writer = null;
                ResponseTracker responseTracker = null;
                IConnectableObservable<Command> commands = null;
                IDisposable commandsDisposable = null;

                int transferCount = 0;

                SocketEndPoint endPoint = SocketEndPoint.Parse("messenger.hotmail.com:1863");

                string authTicket = null;

                while (authTicket == null)
                {

                    connection = new SocketConnection();

                    await connection.ConnectAsync(endPoint);

                    stream = new ConnectionStream(connection);

                    writer = new CommandWriter(stream);
                    reader = new CommandReader(stream, new Dictionary<string, Type> {
                        { "VER", typeof(VersionCommand) },
                        { "CVR", typeof(ClientVersionCommand) },
                        { "USR", typeof(AuthenticateCommand) },
                        { "XFR", typeof(TransferCommand) },
                        { "SYN", typeof(SynchronizeCommand) },
                        { "SBS", typeof(SbsCommand) },
                        { "MSG", typeof(MessageCommand) },
                        { "LST", typeof(UserCommand) },
                        { "LSG", typeof(GroupCommand) },
                        { "BPR", typeof(UserPropertyCommand) },
                        { "BLP", typeof(PrivacySettingCommand) },
                        { "GTC", typeof(PrivacySettingCommand) },
                        { "CHG", typeof(ChangeStatusCommand) },
                        { "UBX", typeof(BroadcastCommand) },
                        { "PRP", typeof(LocalPropertyCommand) },
                        { "NLN", typeof(UserOnlineCommand) },
                        { "ILN", typeof(InitialUserOnlineCommand) },
                        { "FLN", typeof(UserOfflineCommand) },
                        { "UUX", typeof(SendBroadcastCommand) },
                        { "NOT", typeof(NotificationCommand) },
                        { "QNG", typeof(PingCommand) },
                        { "CHL", typeof(ChallengeCommand) },
                        { "ADC", typeof(AddContactCommand) },
                        { "REM", typeof(RemoveContactCommand) },
                        { "ADG", typeof(AddGroupCommand) },
                        { "RMG", typeof(RemoveGroupCommand) },
                        { "REG", typeof(RenameGroupCommand) },  
                        { "QRY", typeof(AcceptChallengeCommand) },  
                        { "RNG", typeof(RingCommand) },
                        { "SBP", typeof(ChangeUserPropertyCommand) },
                        { "IMS", typeof(EnableIMCommand) },
                    });
                    
                    commands = reader.GetReadObservable().Publish();
                    responseTracker = new ResponseTracker(writer, commands);

                    commandsDisposable = commands.Connect();

                    var versionCommand = new VersionCommand("MSNP12");
                    var versionResponse = await responseTracker.GetResponseAsync<VersionCommand>(versionCommand, defaultTimeout);

                    if (versionResponse.Versions.Length == 0)
                        throw new ProtocolNotAcceptedException();

                    var clientVersionCommand = new ClientVersionCommand
                    {
                        LocaleId = "0x0409",
                        OsType = "winnt",
                        OsVersion = "5.0",
                        Architecture = "1386",
                        LibraryName = "MSMSGS",
                        ClientVersion = "5.0.0482",
                        ClientName = "WindowsMessenger",
                        LoginName = credentials.LoginName,
                    };

                    await responseTracker.GetResponseAsync<ClientVersionCommand>(clientVersionCommand, defaultTimeout);

                    var userCommand = new AuthenticateCommand("TWN", "I", credentials.LoginName);
                    var userResponse = await responseTracker.GetResponseAsync(userCommand, new Type[] { typeof(AuthenticateCommand), typeof(TransferCommand) }, defaultTimeout);

                    if (userResponse is AuthenticateCommand)
                    {
                        authTicket = (userResponse as AuthenticateCommand).Argument;
                    }

                    else if (userResponse is TransferCommand)
                    {
                        
                        TransferCommand transferResponse = userResponse as TransferCommand;

                        if (transferCount > 3)
                            throw new InvalidOperationException("The maximum number of redirects has been reached.");

                        transferCount++;

                        endPoint = SocketEndPoint.Parse(transferResponse.Host);

                        commandsDisposable.Dispose();

                        reader.Close();
                        writer.Close();
                        connection.Dispose();

                    }

                }

                PassportAuthentication auth = new PassportAuthentication();

                string authToken = await auth.GetToken(credentials.LoginName, credentials.Password, authTicket);

                var authCommand = new AuthenticateCommand("TWN", "S", authToken);
                var authResponse = await responseTracker.GetResponseAsync<AuthenticateCommand>(authCommand, defaultTimeout);

                var synCommand = new SynchronizeCommand(syncTimeStamp1 ?? "0", syncTimeStamp2 ?? "0");
                var synResponse = await responseTracker.GetResponseAsync<SynchronizeCommand>(synCommand, defaultTimeout);

                IDisposable syncCommandsSubscription = null;
                List<Command> syncCommands = null;

                if (synResponse.TimeStamp1 != syncTimeStamp1 || synResponse.TimeStamp2 != syncTimeStamp2)
                {

                    syncCommands = new List<Command>();

                    Type[] syncTypes = new Type[] { 
                        typeof(MessageCommand), 
                        typeof(UserCommand), 
                        typeof(GroupCommand), 
                        typeof(LocalPropertyCommand), 
                        typeof(PrivacySettingCommand),
                    };

                    syncCommandsSubscription = commands
                        .Where(c => syncTypes.Contains(c.GetType()))
                        .Catch(Observable.Empty<Command>())
                        .Subscribe(c => syncCommands.Add(c));

                    //if we're expecting users/groups, wait for them before we proceed
                    if (synResponse.UserCount + synResponse.GroupCount > 0)
                    {

                        await commands
                            .Where(c => c is UserCommand || c is GroupCommand)
                            .Take(synResponse.UserCount + synResponse.GroupCount)
                            .Timeout(defaultTimeout);

                    }

                }

                UserCapabilities capabilities = 0;
                MSNObject displayPicture = MSNObject.Empty;

                if (LocalUser != null)
                {
                    capabilities = LocalUser.Capabilities;
                    displayPicture = LocalUser.DisplayPicture;
                }

                Command changeStatusCommand = new ChangeStatusCommand(User.StatusToString(UserStatus.Online), (uint)capabilities, displayPicture != MSNObject.Empty ? displayPicture.ToString() : "0");
                await responseTracker.GetResponseAsync(changeStatusCommand, defaultTimeout);

                if (syncCommandsSubscription != null)
                    syncCommandsSubscription.Dispose();

                this.writer = writer;
                this.reader = reader;
                this.stream = stream;
                this.connection = connection;
                this.responseTracker = responseTracker;
                this.commands = commands;
                this.commandsDisposable = commandsDisposable;

                if (LocalUser == null)
                {
                    LocalUser = new LocalUser(this, credentials.LoginName);
                    userCache.Add(credentials.LoginName, new WeakReference(LocalUser));
                }

                LocalUser.Status = initialStatus;

                SyncEvents syncEvents = null;

                if (syncCommands != null)
                {
                    syncTimeStamp1 = synResponse.TimeStamp1;
                    syncTimeStamp2 = synResponse.TimeStamp2;

                    syncEvents = ProcessSyncCommands(syncCommands);
                }

                var commandsSafe = commands
                    .Catch<Command, ConnectionErrorException>(tx => Observable.Empty<Command>());

                commandsSafe.OfType<MessageCommand>().Subscribe(cmd => HandleMessages(cmd, connection));
                commandsSafe.OfType<RingCommand>().Subscribe(cmd => HandleRings(cmd, connection));
                commandsSafe.OfType<BroadcastCommand>().Subscribe(cmd => HandleBroadcasts(cmd, connection));
                commandsSafe.OfType<NotificationCommand>().Subscribe(cmd => HandleNotifications(cmd, connection));
                commandsSafe.OfType<AddContactCommand>().Subscribe(cmd => HandleNewUsers(cmd, connection));
                commandsSafe.OfType<OutCommand>().Subscribe(cmd => HandleOut(cmd, connection));
                commandsSafe.OfType<ChallengeCommand>().Subscribe(cmd => HandleChallenges(cmd, connection));
                commandsSafe.OfType<UserOnlineCommand>().Subscribe(cmd => HandleOnlineUsers(cmd, connection));
                commandsSafe.OfType<InitialUserOnlineCommand>().Subscribe(cmd => HandleOnlineUsers(cmd, connection));
                commandsSafe.OfType<UserOfflineCommand>().Subscribe(cmd => HandleOfflineUsers(cmd, connection));

                connection.Error += connection_Error;

                IsLoggedIn = true;

                OnLoggedIn();

                OnUserStatusChanged(new UserStatusEventArgs(LocalUser, initialStatus, UserStatus.Offline, true));

                if (syncEvents != null)
                    RaiseSyncEvents(syncEvents);


            }
            finally
            {
                @lock.WriterRelease();
            }

        }

        public async Task SetPrivacySettingAsync(PrivacySetting setting, string value)
        {

            if (setting == PrivacySetting.AcceptInvitations &&
                value != AcceptInvitationsFromAllUsers &&
                value != AcceptInvitationsFromAllowedUsersOnly)
                throw new ArgumentException("Invalid value for property");

            if (setting == PrivacySetting.AddUsers &&
                value != AddUsersAutomatically &&
                value != AddUsersWithPrompt)
                throw new ArgumentException("Invalid value for property");

            await @lock.ReaderLockAsync();

            try
            {

                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                if (!IsLoggedIn)
                    throw new NotLoggedInException();

                if (privacySettings[setting] == value)
                    throw new ArgumentException("This value is already set.");

                var cmd = new PrivacySettingCommand(PrivacySettingToString(setting), value);
                await responseTracker.GetResponseAsync<PrivacySettingCommand>(cmd, defaultTimeout);

                privacySettings[setting] = value;

                OnPrivacySettingChanged(new PrivacySettingEventArgs(setting, value, false));

            }
            finally
            {
                @lock.ReaderRelease();
            }


        }

        public async Task PingAsync()
        {

            await @lock.ReaderLockAsync();

            try
            {
                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                if (!IsLoggedIn)
                    throw new NotLoggedInException();

                Command cmd = new PingCommand();

                var getResponse = commands
                    .OfType<PingCommand>()
                    .Timeout(TimeSpan.FromMinutes(2))
                    .FirstAsync()
                    .ToTask();

                await writer.WriteCommandAsync(cmd);

                Command response = await getResponse;

            }

            finally
            {
                @lock.ReaderRelease();
            }


        }

        public async Task<Group> CreateGroupAsync(string name)
        {

            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("A name must be specified.");

            if (Encoding.UTF8.GetByteCount(name) > 61)
                throw new ArgumentException("The name specified was too long.");


            await @lock.ReaderLockAsync();

            try
            {


                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                if (!IsLoggedIn)
                    throw new NotLoggedInException();

                if (Groups.Count >= 30)
                    throw new InvalidOperationException("You have too many groups.");

                if (Groups.Where(g => name == g.Name).Count() > 0)
                    throw new ArgumentException("A group with this name already exists.");

                Command cmd = new AddGroupCommand(name);
                AddGroupCommand response = await responseTracker.GetResponseAsync<AddGroupCommand>(cmd, defaultTimeout);

                Group group = new Group(this, response.Guid, name);

                Groups.AddGroup(group);

                OnGroupAdded(new GroupEventArgs(group, false));

                return group;

            }
            finally
            {
                @lock.ReaderRelease();
            }


        }

        public async Task RemoveGroupAsync(Group group)
        {

            await @lock.ReaderLockAsync();

            try
            {


                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                if (!IsLoggedIn)
                    throw new NotLoggedInException();

                if (!Groups.Contains(group))
                    throw new InvalidOperationException("This group is no longer in use.");

                if (group.Users.Count > 0)
                    throw new InvalidOperationException("Remove all users from group first.");

                Command cmd = new RemoveGroupCommand(group.Guid);
                Command response = await responseTracker.GetResponseAsync<RemoveGroupCommand>(cmd, defaultTimeout);

                Groups.RemoveGroup(group);

                OnGroupRemoved(new GroupEventArgs(group, false));

            }

            finally
            {
                @lock.ReaderRelease();
            }

        }

        public async Task<IMSession> CreateIMSession()
        {

            await @lock.ReaderLockAsync();

            try
            {

                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                IMSession imSession = new IMSession(this);

                IMSessions.AddIMSessionInner(imSession);

                OnIMSessionCreated(new IMSessionEventArgs(imSession, null));

                return imSession;

            }

            finally
            {
                @lock.ReaderRelease();
            }

        }

        public async Task<IMSession> AcceptInvitationAsync(IMSessionInvitation invitation)
        {

            await @lock.ReaderLockAsync();

            try
            {

  
                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                if (!IsLoggedIn)
                    throw new NotLoggedInException();

                if (Interlocked.Exchange(ref invitation.accepted, 1) == 1)
                    throw new InvalidOperationException("Invitation already accepted.");

                IMSession imSession = new IMSession(this);

                await imSession.ConnectByInvitation(invitation);

                IMSessions.AddIMSessionInner(imSession);

                OnIMSessionCreated(new IMSessionEventArgs(imSession, invitation));

                return imSession;


            }

            finally
            {
                @lock.ReaderRelease();
            }

        }

        public string GetPrivacySetting(PrivacySetting setting)
        {
            return GetPrivacySettingInner(setting);
        }

        public User GetUser(string loginName)
        {
            return GetUserInner(loginName);
        }

        public async Task LogoutAsync()
        {

            await @lock.WriterLockAsync();

            try
            {

                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                LogoutInner(LogoutReason.InitiatedByUser, null);

            }
            finally
            {
                @lock.WriterRelease();
            }

        }

        void LogoutInner(LogoutReason reason, ConnectionErrorException connectionError)
        {

            if (!IsLoggedIn)
                return;

            connection.Error -= connection_Error;

            commandsDisposable.Dispose();

            connection.Close();

            reader = null;
            writer = null;
            connection = null;
            responseTracker = null;

            var statusChangeEvents = new List<UserStatusEventArgs>();

            foreach (User user in UserLists.ForwardList.Users.Where(u => u.Status != UserStatus.Offline))
            {
                UserStatus prevStatus = user.Status;
                user.Status = UserStatus.Offline;
                statusChangeEvents.Add(new UserStatusEventArgs(user, UserStatus.Offline, prevStatus, false));
            }

            IsLoggedIn = false;

            OnLoggedOut(new LoggedOutEventArgs(reason, connectionError));

            foreach (UserStatusEventArgs e in statusChangeEvents)
                OnUserStatusChanged(e);

        }

        async void connection_Error(object sender, ConnectionErrorEventArgs e)
        {

            await @lock.WriterLockAsync();

            try
            {

                if (connection == sender)
                    LogoutInner(LogoutReason.ConnectionError, e.Exception);

            }
            finally
            {
                @lock.WriterRelease();
            }

        }

        internal async Task<TransferCommand> GetSwitchboard()
        {

            await @lock.ReaderLockAsync();

            try
            {

                if (closed)
                    throw new ObjectDisposedException(GetType().Name);

                if (!IsLoggedIn)
                    throw new NotLoggedInException();

                Command cmd = new TransferCommand("SB");
                return await responseTracker.GetResponseAsync<TransferCommand>(cmd, defaultTimeout);

            }
            finally
            {
                @lock.ReaderRelease();
            }

        }

        string GetPrivacySettingInner(PrivacySetting setting)
        {
            if (privacySettings.ContainsKey(setting))
                return privacySettings[setting];

            return string.Empty;
        }

        void SetPrivacySettingInner(PrivacySetting setting, string value)
        {

            if (value == String.Empty)
                privacySettings.Remove(setting);

            else if (!privacySettings.ContainsKey(setting))
                privacySettings.Add(setting, value);

            else
                privacySettings[setting] = value;
        }

        internal User GetUserInner(string loginName)
        {
            User user;
            GetUserInner(loginName, out user);
            return user;
        }

        void PurgeUserCache()
        {

            if (lastPurge.AddMinutes(5) >= DateTime.Now)
            {
                foreach (var x in userCache.Where(xx => !xx.Value.IsAlive).ToList())
                    userCache.Remove(x.Key);

                lastPurge = DateTime.Now;
            }

        }

        bool GetUserInner(string loginName, out User user)
        {

            lock (userCache)
            {

                try
                {

                    loginName = loginName.ToLower();

                    WeakReference userReference;

                    if (userCache.TryGetValue(loginName, out userReference))
                    {

                        user = userReference.Target as User;

                        if (user != null)
                            return false;

                    }

                    user = new User(this, loginName);

                    userCache[loginName] = new WeakReference(user);

                    return true;

                }
                finally
                {
                    PurgeUserCache();
                }

            }


        }

        void HandleMessages(MessageCommand messageCommand, IConnection connection)
        {
            Message message = new Message(messageCommand.Payload);
            OnMessageReceived(new MessageEventArgs(messageCommand.Sender, messageCommand.SenderNickname, message, false));
        }

        async void HandleRings(RingCommand ringCommand, IConnection connection)
        {

            await @lock.ReaderLockAsync();

            try
            {

                if (this.closed)
                    return;

                User user = GetUserInner(ringCommand.Caller);

                user.UpdateNicknameFromIM(ringCommand.CallerNickname);

                SocketEndPoint endPoint = SocketEndPoint.Parse(ringCommand.Endpoint);

                IMSessionInvitation invite = new IMSessionInvitation(user, endPoint, ringCommand.SessionId, ringCommand.AuthString);

                OnInvitedToIMSession(new InvitationEventArgs(invite));

            }
            finally
            {
                @lock.ReaderRelease();
            }


        }

        async void HandleBroadcasts(BroadcastCommand broadcastCommand, IConnection connection)
        {


            await @lock.ReaderLockAsync();

            try
            {

                if (this.closed)
                    return;

                User user = GetUserInner(broadcastCommand.LoginName);
                OnUserBroadcast(new UserBroadcastEventArgs(user, broadcastCommand.Message));

            }
            finally
            {
                @lock.ReaderRelease();
            }

        }

        void HandleNotifications(NotificationCommand notificationCommand, IConnection connection)
        {
            OnServerNotification(new ServerNotificationEventArgs(notificationCommand.Message));
        }

        async void HandleNewUsers(AddContactCommand addContactCommand, IConnection connection)
        {


            await @lock.ReaderLockAsync();

            try
            {

                if (this.connection != connection)
                    return;

                if (addContactCommand.TrId == null && addContactCommand.List == UserLists.ReverseList.listCode)
                {

                    User user = GetUserInner(addContactCommand.LoginName);
                    UserLists.PendingList.Users.AddUserInner(user);

                    OnUserAddedToList(new UserListUserEventArgs(user, UserLists.PendingList, false));

                }

            }
            finally
            {
                @lock.ReaderRelease();
            }

        }

        async void HandleOut(OutCommand outCommand, IConnection connection)
        {

            await @lock.WriterLockAsync();

            try
            {

                if (this.connection == connection)
                {

                    LogoutReason reason = LogoutReason.ConnectionError;

                    if (outCommand.OutCode == "OTH")
                        reason = LogoutReason.LoggedInElsewhere;
                    else if (outCommand.OutCode == "SSD")
                        reason = LogoutReason.ServerShuttingDown;

                    LogoutInner(reason, null);
                }

            }
            finally
            {
                @lock.WriterRelease();
            }

        }

        async void HandleChallenges(ChallengeCommand challengeCommand, IConnection connection)
        {

            await @lock.ReaderLockAsync();

            try
            {

                if (this.connection != connection)
                    return;

                string productID = "PROD0120PW!CCV9@";
                string productKey = "C1BX{V4W}Q3*10SM";

                Debug.WriteLine("[Challenge Received]");

                string data = HandshakeUtility.GenerateChallengeRespose(challengeCommand.ChallengeString, productID, productKey);

                AcceptChallengeCommand cmd = new AcceptChallengeCommand(productID, data);

                try
                {

                    Command response = await responseTracker.GetResponseAsync<AcceptChallengeCommand>(cmd, defaultTimeout);

                    Debug.WriteLine("[Challenge Success]");

                }
                catch (TimeoutException)
                {
                    //there is nothing we can do here
                    return;
                }
                catch (ConnectionErrorException)
                {
                    //there is nothing we can do here
                    return;
                }

            }
            finally
            {
                @lock.ReaderRelease();
            }

        }

        async void HandleOnlineUsers(UserOnlineCommandBase userStatusCommand, IConnection connection)
        {


            await @lock.ReaderLockAsync();

            try
            {

                if (this.connection != connection)
                    return;

                bool statusChanged = false;
                bool nicknameChanged = false;
                bool capabilitiesChanged = false;
                bool dpicChanged = false;

                UserStatus newStatus = User.StringToStatus(userStatusCommand.Status);

                UserCapabilities newCapabilities = (UserCapabilities)userStatusCommand.Capabilities;
                MSNObject newDisplayPicture = userStatusCommand.DisplayPicture != "0" ? MSNObject.Parse(userStatusCommand.DisplayPicture) : MSNObject.Empty;

                UserStatus prevStatus = UserStatus.Offline;
                string prevNickname = null;

                User user = GetUserInner(userStatusCommand.LoginName);

                if (newStatus != user.Status)
                {
                    prevStatus = user.Status;
                    user.Status = newStatus;

                    statusChanged = true;

                }

                if (user.Nickname != userStatusCommand.Nickname)
                {

                    prevNickname = user.Nickname;
                    user.Nickname = userStatusCommand.Nickname;

                    nicknameChanged = true;

                    //When a user's name changes we have to update our own copy on the server

                    Command cmd = new ChangeUserPropertyCommand(user.guid, "MFN", userStatusCommand.Nickname);

                    try
                    {
                        ChangeUserPropertyCommand response = await responseTracker.GetResponseAsync<ChangeUserPropertyCommand>(cmd, defaultTimeout);

                    }
                    catch (TimeoutException)
                    {
                        //there is nothing we can do here
                        return;
                    }
                    catch (ConnectionErrorException)
                    {
                        //there is nothing we can do here
                        return;
                    }


                }

                if (user.Capabilities != newCapabilities)
                {
                    user.Capabilities = newCapabilities;
                    capabilitiesChanged = true;
                }

                if (user.DisplayPicture != newDisplayPicture)
                {
                    user.DisplayPicture = newDisplayPicture;
                    dpicChanged = true;
                }

                if (statusChanged)
                    OnUserStatusChanged(new UserStatusEventArgs(user, user.Status, prevStatus, userStatusCommand is InitialUserOnlineCommand));

                if (nicknameChanged)
                    OnUserNicknameChanged(new UserNicknameEventArgs(user, user.Nickname, prevNickname, userStatusCommand is InitialUserOnlineCommand));

                if (capabilitiesChanged)
                    OnUserCapabilitiesChanged(new UserCapabilitiesEventArgs(user, user.Capabilities, userStatusCommand is InitialUserOnlineCommand));

                if (dpicChanged)
                    OnUserDisplayPictureChanged(new UserDisplayPictureEventArgs(user, user.DisplayPicture, userStatusCommand is InitialUserOnlineCommand));

            }
            finally
            {
                @lock.ReaderRelease();
            }

        }

        async void HandleOfflineUsers(UserOfflineCommand userOfflineCommand, IConnection connection)
        {

            await @lock.ReaderLockAsync();

            try
            {

                if (this.connection != connection)
                    return;

                User user = GetUserInner(userOfflineCommand.LoginName);

                UserStatus prevStatus = user.Status;
                user.Status = UserStatus.Offline;

                OnUserStatusChanged(new UserStatusEventArgs(user, UserStatus.Offline, prevStatus, false));

            }
            finally
            {
                @lock.ReaderRelease();
            }

        }

        SyncEvents ProcessSyncCommands(List<Command> syncCommands)
        {

            SyncEvents events = new SyncEvents();

            List<User> referencedUsers = new List<User>();
            List<Group> referencedGroups = new List<Group>();

            User lastUser = null;

            foreach (Command cmd in syncCommands)
            {

                if (cmd is UserCommand)
                {

                    UserCommand listCmd = cmd as UserCommand;

                    User user = null;

                    if (GetUserInner(listCmd.LoginName, out user))
                    {
                        user.guid = listCmd.Guid;
                        user.Nickname = listCmd.Nickname;
                    }
                    else
                    {

                        user.guid = listCmd.Guid;

                        if (user.Nickname != listCmd.Nickname)
                        {
                            string prevName = user.Nickname;
                            user.Nickname = listCmd.Nickname;

                            events.userNickNameChangedEvents.Add(new UserNicknameEventArgs(user, user.Nickname, prevName, false));
                        }

                    }

                    //user added to lists
                    foreach (UserList list in UserLists.Where(x => listCmd.Lists.Contains(x.listCode) && !x.Users.Contains(user)))
                    {
                        list.Users.AddUserInner(user);
                        events.userAddedToListEvents.Add(new UserListUserEventArgs(user, list, true));
                    }

                    //user removed from lists
                    foreach (UserList list in UserLists.Where(x => !listCmd.Lists.Contains(x.listCode) && x.Users.Contains(user)))
                    {
                        list.Users.RemoveUserInner(user);
                        events.userRemovedFromListEvents.Add(new UserListUserEventArgs(user, list, true));
                    }

                    //user added to groups
                    foreach (Group group in Groups.Where(x => listCmd.Groups.Contains(x.Guid) && !x.Users.Contains(user)))
                    {
                        group.Users.AddUserInner(user);
                        events.userAddedToGroupEvents.Add(new GroupUserEventArgs(user, group, true));

                    }

                    //user removed from groups
                    foreach (Group group in Groups.Where(x => !listCmd.Groups.Contains(x.Guid) && x.Users.Contains(user)))
                    {
                        group.Users.RemoveUserInner(user);
                        events.userRemovedFromGroupEvents.Add(new GroupUserEventArgs(user, group, true));

                    }

                    referencedUsers.Add(user);

                    lastUser = user;

                }
                else if (cmd is GroupCommand)
                {

                    GroupCommand groupCmd = cmd as GroupCommand;

                    Group group = Groups.Where(gx => gx.Guid == groupCmd.Guid).SingleOrDefault();

                    if (group == null)
                    {

                        group = new Group(this, groupCmd.Guid, groupCmd.Name);

                        Groups.AddGroup(group);

                        events.groupAddedEvents.Add(new GroupEventArgs(group, true));

                    }
                    else
                    {

                        if (group.Name != groupCmd.Name)
                        {

                            string prevName = group.Name;
                            group.Name = groupCmd.Name;

                            events.groupNameChangedEvents.Add(new GroupNameEventArgs(group, groupCmd.Name, prevName, true));
                        }

                    }

                    referencedGroups.Add(group);

                }

                else if (cmd is UserPropertyCommand)
                {

                    UserPropertyCommand propCmd = cmd as UserPropertyCommand;

                    UserProperty property = User.StringToProperty(propCmd.Key);

                    if (lastUser.GetPropertyInner(property) != propCmd.Value)
                    {
                        lastUser.SetPropertyInner(property, propCmd.Value);
                        events.userPropertyChangedEvents.Add(new UserPropertyEventArgs(lastUser, property, propCmd.Value, true));
                    }

                }

                else if (cmd is LocalPropertyCommand && ((LocalPropertyCommand)cmd).Key == "MFN")
                {

                    LocalPropertyCommand nameCmd = cmd as LocalPropertyCommand;

                    if (LocalUser.Nickname != nameCmd.Value)
                    {

                        string prevName = LocalUser.Nickname;
                        LocalUser.Nickname = nameCmd.Value;

                        events.userNickNameChangedEvents.Add(new UserNicknameEventArgs(LocalUser, nameCmd.Value, prevName, true));
                    }

                }

                else if (cmd is LocalPropertyCommand)
                {

                    LocalPropertyCommand propCmd = cmd as LocalPropertyCommand;

                    UserProperty property = User.StringToProperty(propCmd.Key);

                    if (LocalUser.GetPropertyInner(property) != propCmd.Value)
                    {
                        LocalUser.SetPropertyInner(property, propCmd.Value);
                        events.userPropertyChangedEvents.Add(new UserPropertyEventArgs(LocalUser, property, propCmd.Value, true));
                    }

                }
                else if (cmd is PrivacySettingCommand)
                {

                    PrivacySettingCommand privCmd = cmd as PrivacySettingCommand;

                    PrivacySetting setting = MessengerClient.StringToPrivacySetting(privCmd.Key);

                    if (GetPrivacySettingInner(setting) != privCmd.Value)
                    {
                        SetPrivacySettingInner(setting, privCmd.Value);
                        events.privacySettingChangedEvents.Add(new PrivacySettingEventArgs(setting, privCmd.Value, true));
                    }

                }
                else if (cmd is MessageCommand)
                {
                    MessageCommand message = cmd as MessageCommand;

                    Message msg = new Message(message.Payload);
                    events.messageEvents.Add(new MessageEventArgs(message.Sender, message.SenderNickname, msg, true));

                }
                else if (cmd is SbsCommand)
                {
                    //todo: do something with this command
                }



            }

            // remove groups that no longer exist
            foreach (Group group in Groups.Except(referencedGroups))
            {
                Groups.RemoveGroup(group);

                events.groupRemovedEvents.Add(new GroupEventArgs(group, true));
            }

            //remove users that no longer exist from lists and groups which currently contain them

            foreach (UserList list in UserLists)
                foreach (User user in list.Users.Except(referencedUsers))
                {
                    list.Users.RemoveUserInner(user);
                    events.userRemovedFromListEvents.Add(new UserListUserEventArgs(user, list, true));
                }

            foreach (Group group in Groups)
                foreach (User user in group.Users.Except(referencedUsers))
                {
                    group.Users.RemoveUserInner(user);
                    events.userRemovedFromGroupEvents.Add(new GroupUserEventArgs(user, group, true));

                }


            return events;

        }

        void RaiseSyncEvents(SyncEvents data)
        {

            foreach (MessageEventArgs e in data.messageEvents)
                OnMessageReceived(e);

            foreach (GroupEventArgs e in data.groupAddedEvents)
                OnGroupAdded(e);

            foreach (GroupEventArgs e in data.groupRemovedEvents)
                OnGroupRemoved(e);

            foreach (UserListUserEventArgs e in data.userAddedToListEvents)
                OnUserAddedToList(e);

            foreach (UserListUserEventArgs e in data.userRemovedFromListEvents)
                OnUserRemovedFromList(e);

            foreach (GroupUserEventArgs e in data.userAddedToGroupEvents)
                OnUserAddedToGroup(e);

            foreach (GroupUserEventArgs e in data.userRemovedFromGroupEvents)
                OnUserRemovedFromGroup(e);

            foreach (GroupNameEventArgs e in data.groupNameChangedEvents)
                OnGroupNameChanged(e);

            foreach (UserNicknameEventArgs e in data.userNickNameChangedEvents)
                OnUserNicknameChanged(e);

            foreach (UserPropertyEventArgs e in data.userPropertyChangedEvents)
                OnUserPropertyChanged(e);

            foreach (PrivacySettingEventArgs e in data.privacySettingChangedEvents)
                OnPrivacySettingChanged(e);
        }

        public async void Close()
        {

            await @lock.WriterLockAsync();

            try
            {

                if (closed)
                    return;

                foreach (var imSession in IMSessions.ToList())
                    imSession.Close();

                LogoutInner(LogoutReason.InitiatedByUser, null);

                IMSessions = null;
                Groups = null;
                userCache = null;

                privacySettings = null;

                UserLists = null;
                LocalUser = null;

                credentials = null;

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

        static string PrivacySettingToString(PrivacySetting setting)
        {

            switch (setting)
            {
                case PrivacySetting.AddUsers:
                    return "GTC";
                case PrivacySetting.AcceptInvitations:
                    return "BLP";
                default:
                    throw new NotSupportedException();
            }
        }

        static PrivacySetting StringToPrivacySetting(string inString)
        {
            switch (inString)
            {
                case "GTC":
                    return PrivacySetting.AddUsers;
                case "BLP":
                    return PrivacySetting.AcceptInvitations;
                default:
                    throw new NotSupportedException();
            }
        }

        public event EventHandler LoggedIn;
        public event EventHandler<LoggedOutEventArgs> LoggedOut;
        public event EventHandler<UserStatusEventArgs> UserStatusChanged;
        public event EventHandler<UserBroadcastEventArgs> UserBroadcast;
        public event EventHandler<UserNicknameEventArgs> UserNicknameChanged;
        public event EventHandler<UserDisplayPictureEventArgs> UserDisplayPictureChanged;
        public event EventHandler<UserCapabilitiesEventArgs> UserCapabilitiesChanged;
        public event EventHandler<UserPropertyEventArgs> UserPropertyChanged;
        public event EventHandler<MessageEventArgs> MessageReceived;
        public event EventHandler<IMSessionEventArgs> IMSessionCreated;

        public event EventHandler<InvitationEventArgs> InvitedToIMSession;
        public event EventHandler<UserListUserEventArgs> UserAddedToList;
        public event EventHandler<UserListUserEventArgs> UserRemovedFromList;
        public event EventHandler<GroupNameEventArgs> GroupNameChanged;
        public event EventHandler<GroupEventArgs> GroupAdded;
        public event EventHandler<GroupEventArgs> GroupRemoved;
        public event EventHandler<GroupUserEventArgs> UserAddedToGroup;
        public event EventHandler<GroupUserEventArgs> UserRemovedFromGroup;
        public event EventHandler<PrivacySettingEventArgs> PrivacySettingChanged;
        public event EventHandler<ServerNotificationEventArgs> ServerNotification;

        public event EventHandler Closed;


        void OnUserBroadcast(UserBroadcastEventArgs e)
        {
            EventHandler<UserBroadcastEventArgs> handler = UserBroadcast;
            if (handler != null) handler(this, e);
        }

        void OnServerNotification(ServerNotificationEventArgs e)
        {
            EventHandler<ServerNotificationEventArgs> handler = ServerNotification;
            if (handler != null) handler(this, e);
        }

        void OnPrivacySettingChanged(PrivacySettingEventArgs e)
        {
            EventHandler<PrivacySettingEventArgs> handler = PrivacySettingChanged;
            if (handler != null) handler(this, e);
        }

        void OnLoggedIn()
        {
            EventHandler handler = LoggedIn;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        void OnLoggedOut(LoggedOutEventArgs e)
        {
            EventHandler<LoggedOutEventArgs> handler = LoggedOut;
            if (handler != null) handler(this, e);
        }

        internal void OnUserStatusChanged(UserStatusEventArgs e)
        {
            EventHandler<UserStatusEventArgs> handler = UserStatusChanged;
            if (handler != null) handler(this, e);
        }

        internal void OnUserDisplayPictureChanged(UserDisplayPictureEventArgs e)
        {
            EventHandler<UserDisplayPictureEventArgs> handler = UserDisplayPictureChanged;
            if (handler != null) handler(this, e);
        }

        internal void OnUserCapabilitiesChanged(UserCapabilitiesEventArgs e)
        {
            EventHandler<UserCapabilitiesEventArgs> handler = UserCapabilitiesChanged;
            if (handler != null) handler(this, e);
        }

        internal void OnUserPropertyChanged(UserPropertyEventArgs e)
        {
            EventHandler<UserPropertyEventArgs> handler = UserPropertyChanged;
            if (handler != null) handler(this, e);
        }

        internal void OnUserNicknameChanged(UserNicknameEventArgs e)
        {
            EventHandler<UserNicknameEventArgs> handler = UserNicknameChanged;
            if (handler != null) handler(this, e);
        }

        void OnMessageReceived(MessageEventArgs e)
        {
            EventHandler<MessageEventArgs> handler = MessageReceived;
            if (handler != null) handler(this, e);
        }

        void OnIMSessionCreated(IMSessionEventArgs e)
        {
            EventHandler<IMSessionEventArgs> handler = IMSessionCreated;
            if (handler != null) handler(this, e);
        }

        void OnInvitedToIMSession(InvitationEventArgs e)
        {
            EventHandler<InvitationEventArgs> handler = InvitedToIMSession;
            if (handler != null) handler(this, e);
        }

        internal void OnUserAddedToList(UserListUserEventArgs e)
        {
            EventHandler<UserListUserEventArgs> handler = UserAddedToList;
            if (handler != null) handler(this, e);
        }

        internal void OnUserRemovedFromList(UserListUserEventArgs e)
        {
            EventHandler<UserListUserEventArgs> handler = UserRemovedFromList;
            if (handler != null) handler(this, e);
        }

        internal void OnGroupNameChanged(GroupNameEventArgs e)
        {
            EventHandler<GroupNameEventArgs> handler = GroupNameChanged;
            if (handler != null) handler(this, e);
        }

        internal void OnGroupAdded(GroupEventArgs e)
        {
            EventHandler<GroupEventArgs> handler = GroupAdded;
            if (handler != null) handler(this, e);
        }

        internal void OnGroupRemoved(GroupEventArgs e)
        {
            EventHandler<GroupEventArgs> handler = GroupRemoved;
            if (handler != null) handler(this, e);
        }

        internal void OnUserAddedToGroup(GroupUserEventArgs e)
        {
            EventHandler<GroupUserEventArgs> handler = UserAddedToGroup;
            if (handler != null) handler(this, e);
        }

        internal void OnUserRemovedFromGroup(GroupUserEventArgs e)
        {
            EventHandler<GroupUserEventArgs> handler = UserRemovedFromGroup;
            if (handler != null) handler(this, e);
        }

        void OnClosed()
        {
            EventHandler handler = Closed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

    }

    internal class SyncEvents
    {
        public List<MessageEventArgs> messageEvents = new List<MessageEventArgs>();
        public List<UserListUserEventArgs> userAddedToListEvents = new List<UserListUserEventArgs>();
        public List<UserListUserEventArgs> userRemovedFromListEvents = new List<UserListUserEventArgs>();
        public List<GroupUserEventArgs> userAddedToGroupEvents = new List<GroupUserEventArgs>();
        public List<GroupUserEventArgs> userRemovedFromGroupEvents = new List<GroupUserEventArgs>();
        public List<UserNicknameEventArgs> userNickNameChangedEvents = new List<UserNicknameEventArgs>();
        public List<UserPropertyEventArgs> userPropertyChangedEvents = new List<UserPropertyEventArgs>();
        public List<GroupNameEventArgs> groupNameChangedEvents = new List<GroupNameEventArgs>();
        public List<PrivacySettingEventArgs> privacySettingChangedEvents = new List<PrivacySettingEventArgs>();
        public List<GroupEventArgs> groupAddedEvents = new List<GroupEventArgs>();
        public List<GroupEventArgs> groupRemovedEvents = new List<GroupEventArgs>();
    }

    public class Credentials
    {
        public string LoginName { get; private set; }
        public string Password { get; private set; }

        public Credentials(string loginName, string password)
        {
            LoginName = loginName;
            Password = password;
        }
    }


}

