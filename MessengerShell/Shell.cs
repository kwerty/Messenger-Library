using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MessengerLibrary;
using MessengerLibrary.Connections;
using MessengerLibrary.Authentication;
using System.Threading;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;


namespace MessengerShell
{

    class MessengerShell
    {

        MessengerClient Msgr { get; set; }

        bool ParrotMode { get; set; }
        bool RandomColor { get; set; }

        ConsoleColor backgroundColor = ConsoleColor.DarkBlue;
        ConsoleColor foregroundColor = ConsoleColor.White;
        ConsoleColor eventColor = ConsoleColor.Green;
        ConsoleColor errorColor = ConsoleColor.Red;
        ConsoleColor infoColor = ConsoleColor.White;
        ConsoleColor titleColor = ConsoleColor.Yellow;

        ManualResetEvent endEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            new MessengerShell();
        }

        public MessengerShell()
        {

            SetConsoleDefaults();

            using (ConsoleExt.WithColor(titleColor))
                Console.WriteLine("☻ Kwerty Messenger Library Test Shell");

            Console.WriteLine("Login like this - 'login example@test.com password123'");
            Console.WriteLine();

            ParrotMode = true;
            RandomColor = true;

            ThreadPool.QueueUserWorkItem(TakeInput);

            endEvent.WaitOne();

        }

        void SetConsoleDefaults()
        {
            Console.BackgroundColor = backgroundColor;
            Console.ForegroundColor = foregroundColor;
            Console.CursorVisible = true;
            Console.Clear();
        }

        public async void TakeInput(object state)
        {

            string line = await Console.In.ReadLineAsync();

            string cmd = line.Split(new char[] { ' ' }, 2)[0];

            try
            {

                if (cmd != "login" && Msgr == null)
                    throw new ArgumentException("First you must login...");

                switch (cmd)
                {

                    case "login":
                        {

                            var args = line.Split(' ').Skip(1).ToArray();

                            if (Msgr == null)
                            {


                                var credentials = new Credentials();

                                //if (args.Length == 0)
                                //{
                                //    credentials.LoginName = "dontshootthemsgr@hotmail.com";
                                //    credentials.Password = "testpass";

                                //}
                                if (args.Length == 2)
                                {
                                    credentials.LoginName = args[0];
                                    credentials.Password = args[1];
                                }

                                else if (args.Length != 2)
                                    throw new ArgumentException("Login name and password required");

                                Msgr = new MessengerClient(credentials);

                                AddHandlers();

                            }

                            try
                            {
                                await Msgr.LoginAsync();

                            }
                            catch (AuthenticationErrorException ex)
                            {
                                using (ConsoleExt.WithColor(errorColor))
                                    Console.WriteLine(ex.Message);
                            }
                            catch (ProtocolNotAcceptedException ex)
                            {
                                using (ConsoleExt.WithColor(errorColor))
                                    Console.WriteLine(ex.Message);
                            }

                        }
                        break;

                    case "logout":

                        await Msgr.LogoutAsync();

                        break;

                    case "user":
                        {
                            string[] args = line.Split(new char[] { ' ' }).Skip(1).ToArray();

                            if (args.Length != 1)
                                throw new ArgumentException("An email address is required");

                            string loginName = args[0];

                            if (loginName == "me")
                                loginName = Msgr.LocalUser.LoginName;

                            User user = Msgr.GetUser(loginName);

                            var lists = Msgr.UserLists.Where(l => l.Users.Contains(user));
                            var groups = Msgr.Groups.Where(g => g.Users.Contains(user));

                            using (ConsoleExt.WithColor(infoColor))
                            {

                                using (ConsoleExt.WithColor(titleColor))
                                    Console.WriteLine("{0}:", user.LoginName);

                                Console.WriteLine("Login name: {0}", user.LoginName);
                                Console.WriteLine("Nickname: {0}", user.Nickname ?? "(n/a)");
                                Console.WriteLine("Status: {0}", user.Status);
                                Console.WriteLine("Capabilities: {0}", user.Capabilities);

                                using (ConsoleExt.WithColor(titleColor))
                                    Console.WriteLine("Properties:");

                                Console.WriteLine("AuthorizedMobile: {0}", user.GetProperty(UserProperty.AuthorizedMobile));
                                Console.WriteLine("HasBlog: {0}", user.GetProperty(UserProperty.HasBlog));
                                Console.WriteLine("HasMobileDevice: {0}", user.GetProperty(UserProperty.MobileDevice));
                                Console.WriteLine("HomePhone: {0}", user.GetProperty(UserProperty.HomePhone));
                                Console.WriteLine("MobilePhone: {0}", user.GetProperty(UserProperty.MobilePhone));
                                Console.WriteLine("MSNDirectDevice: {0}", user.GetProperty(UserProperty.MSNDirectDevice));
                                Console.WriteLine("WorkPhone: {0}", user.GetProperty(UserProperty.WorkPhone));

                                if (lists.Count() > 0)
                                {
                                    using (ConsoleExt.WithColor(titleColor))
                                        Console.WriteLine("Member of lists:");

                                    foreach (var l in lists)
                                        Console.WriteLine(l.ToString());

                                }

                                if (groups.Count() > 0)
                                {

                                    using (ConsoleExt.WithColor(titleColor))
                                        Console.WriteLine("Member of groups:");

                                    foreach (var g in groups)
                                        Console.WriteLine(g.Name);
                                }
                            }

                        }
                        break;

                    case "name":
                        {

                            string[] args = line.Split(new char[] { ' ' }, 2).Skip(1).ToArray();

                            if (args.Length == 0)
                                using (ConsoleExt.WithColor(infoColor))
                                    Console.WriteLine("Your name is '{0}'", Msgr.LocalUser.Nickname ?? "(none)");

                            else
                            {
                                string newName = args[0];

                                await Msgr.LocalUser.ChangeNicknameAsync(newName);
                            }

                        }
                        break;

                    case "status":
                        {

                            string[] args = line.Split(new char[] { ' ' }, 2).Skip(1).ToArray();

                            if (args.Length == 0)
                                using (ConsoleExt.WithColor(infoColor))
                                    Console.WriteLine("Your status is '{0}'", Msgr.LocalUser.Status);

                            else
                            {

                                UserStatus newStatus = StrToStatus(args[0]);

                                await Msgr.LocalUser.ChangeStatusAsync(newStatus);
                            }

                        }
                        break;

                    case "caps":
                        {

                            string[] args = line.Split(new char[] { ' ' }, 2).Skip(1).ToArray();

                            if (args.Length == 0)
                                using (ConsoleExt.WithColor(infoColor))
                                    Console.WriteLine("Your capabilities are '{0}'", Msgr.LocalUser.Capabilities);

                            else
                            {
                                await Msgr.LocalUser.ChangeCapabilitiesAsync(UserCapabilities.RendersGif | UserCapabilities.Version10);
                            }

                        }
                        break;

                    case "props":
                        {
                            var ranNum = new Random().Next(500, 100000).ToString();

                            await Msgr.LocalUser.SetPropertyAsync(UserProperty.HomePhone, ranNum);
                            await Msgr.LocalUser.SetPropertyAsync(UserProperty.WorkPhone, ranNum);
                            await Msgr.LocalUser.SetPropertyAsync(UserProperty.MobilePhone, ranNum);
                            await Msgr.LocalUser.SetPropertyAsync(UserProperty.MSNDirectDevice, Msgr.LocalUser.GetProperty(UserProperty.MSNDirectDevice) == User.MSNDirectDeviceEnabled ? User.MSNDirectDeviceDisabled : User.MSNDirectDeviceEnabled);
                        }

                        break;

                    case "groups":

                        foreach (Group group in Msgr.Groups)
                            using (ConsoleExt.WithColor(infoColor))
                            {
                                using (ConsoleExt.WithColor(titleColor))
                                    Console.WriteLine("{0}:", group.Name);

                                foreach (User u in group.Users)
                                    Console.WriteLine("{0} ({1})", u.Nickname ?? u.LoginName, u.Status);

                            }

                        break;


                    case "ims":


                        int index = 0;

                        foreach (IMSession im in Msgr.IMSessions.ToList())
                            using (ConsoleExt.WithColor(infoColor))
                            {

                                index++;

                                using (ConsoleExt.WithColor(titleColor))
                                    Console.WriteLine("IM session #{0} ({1} users):", index, im.Users.Count());

                                foreach (User u in im.Users)
                                    Console.WriteLine("{0} ({1})", u.Nickname ?? u.LoginName, u.Status);
                            }

                        break;


                    case "privs":
                        await Msgr.SetPrivacySettingAsync(PrivacySetting.AddUsers, Msgr.GetPrivacySetting(PrivacySetting.AddUsers) == MessengerClient.AddUsersWithPrompt ? MessengerClient.AddUsersAutomatically : MessengerClient.AddUsersWithPrompt);
                        await Msgr.SetPrivacySettingAsync(PrivacySetting.AcceptInvitations, Msgr.GetPrivacySetting(PrivacySetting.AcceptInvitations) == MessengerClient.AcceptInvitationsFromAllUsers ? MessengerClient.AcceptInvitationsFromAllowedUsersOnly : MessengerClient.AcceptInvitationsFromAllUsers);

                        break;

                    case "ingroups":

                        using (ConsoleExt.WithColor(infoColor))
                            foreach (Group group in Msgr.Groups)
                                Console.WriteLine("{0} has {1} users", group.Name, group.Users.Count);

                        break;

                    case "inlists":

                        using (ConsoleExt.WithColor(infoColor))
                            foreach (UserList list in Msgr.UserLists)
                                Console.WriteLine("{0} has {1} users", list, list.Users.Count);

                        break;

                    case "users":

                        using (ConsoleExt.WithColor(infoColor))
                            foreach (UserList list in Msgr.UserLists)
                            {
                                using (ConsoleExt.WithColor(titleColor))
                                    Console.WriteLine("{0} ({1} users):", list, list.Users.Count);

                                foreach (User u in list.Users)
                                    Console.WriteLine("{0} ({1})", u.Nickname ?? u.LoginName, u.Status);
                            }

                        break;

                    case "online":

                        using (ConsoleExt.WithColor(infoColor))
                        {
                            using (ConsoleExt.WithColor(titleColor))
                                Console.WriteLine("Online:");

                            foreach (User u in Msgr.UserLists.ForwardList.Users.Where(u => u.Status != UserStatus.Offline))
                                Console.WriteLine("{0} ({1})", u.Nickname ?? u.LoginName, u.Status);

                            using (ConsoleExt.WithColor(titleColor))
                                Console.WriteLine("Offline:");

                            foreach (User u in Msgr.UserLists.ForwardList.Users.Where(u => u.Status == UserStatus.Offline))
                                Console.WriteLine("{0} ({1})", u.Nickname ?? u.LoginName, u.Status);
                        }

                        break;

                    case "groupname":
                        {
                            string[] args = line.Split(new char[] { ' ' }, 2).Skip(1).ToArray();

                            if (args.Length == 0)
                                throw new ArgumentException("Enter a name");

                            string name = args[0];

                            await Msgr.Groups.FirstOrDefault().ChangeNameAsync(name);

                        }
                        break;

                    case "remgroups":
                        foreach (Group group in Msgr.Groups.Where(g => g.Users.Count == 0))
                            await Msgr.RemoveGroupAsync(group);

                        break;

                    case "pmsg":
                        {

                            string[] args = line.Split(new char[] { ' ' }, 2).Skip(1).ToArray();

                            if (args.Length == 0)
                                throw new ArgumentException("Enter a message");

                            string msg = args[0];

                            await Msgr.LocalUser.BroadcastAsync(String.Format("<Data><PSM>{0}</PSM><CurrentMedia></CurrentMedia></Data>", msg));

                        }

                        break;

                    case "remblock":

                        foreach (User u in Msgr.UserLists.BlockList.Users.ToArray())
                            await Msgr.UserLists.BlockList.RemoveUserAsync(u);

                        break;

                    case "addall":
                        {

                            var rev = Msgr.UserLists.ReverseList.Users.Concat(Msgr.UserLists.PendingList.Users).Distinct();

                            foreach (var o in rev)
                            {

                                if (!Msgr.UserLists.ReverseList.Users.Contains(o))
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(8));
                                    await Msgr.UserLists.ReverseList.AddUserAsync(o);
                                }

                                if (!Msgr.UserLists.AllowList.Users.Contains(o))
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(8));
                                    await Msgr.UserLists.AllowList.AddUserAsync(o);
                                }

                                if (!Msgr.UserLists.ForwardList.Users.Contains(o))
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(8));
                                    await Msgr.UserLists.ForwardList.AddUserAsync(o);
                                }

                            }

                        }
                        break;

                    case "allow":
                        {

                            foreach (User u in Msgr.UserLists.PendingList.Users.ToList())
                            {

                                if (!Msgr.UserLists.ReverseList.Users.Contains(u))
                                    await Msgr.UserLists.ReverseList.AddUserAsync(u);

                                if (!Msgr.UserLists.AllowList.Users.Contains(u))
                                    await Msgr.UserLists.AllowList.AddUserAsync(u);

                                await Msgr.UserLists.PendingList.RemoveUserAsync(u);

                            }

                        }
                        break;

                    case "copydpic":
                        {
                            var args = line.Split(' ').Skip(1).ToArray();

                            if (args.Length == 0)
                                throw new ArgumentException("Specify the user whose dpic you wish to copy");

                            string usr = args[0];

                            await Msgr.LocalUser.ChangeDisplayPictureAsync(Msgr.GetUser(usr).DisplayPicture);


                        }
                        break;

                    case "addgroup":
                        {

                            string[] args = line.Split(new char[] { ' ' }, 2).Skip(1).ToArray();

                            if (args.Length == 0)
                                throw new ArgumentException("Enter a name");

                            string name = args[0];

                            await Msgr.CreateGroupAsync(name);

                        }
                        break;

                    case "remgroup":
                        await Msgr.RemoveGroupAsync(Msgr.Groups.FirstOrDefault());
                        break;

                    case "addtogroup":
                        {
                            var args = line.Split(' ').Skip(1).ToArray();

                            if (args.Length == 0)
                                throw new ArgumentException("Specify the user you wish to add");

                            string usr = args[0];

                            await Msgr.Groups.FirstOrDefault().AddUserAsync(Msgr.GetUser(usr));

                        }
                        break;

                    case "remfromgroup":
                        {

                            Group group = Msgr.Groups.Where(g => g.Users.Count > 0).FirstOrDefault();

                            if (group == null)
                            {
                                using (ConsoleExt.WithColor(errorColor))
                                    Console.WriteLine("No groups with users in them");

                                break;
                            }

                            User user = group.Users.FirstOrDefault();

                            await group.RemoveUserAsync(user);

                        }
                        break;


                    case "add":
                        {
                            var args = line.Split(' ').Skip(1).ToArray();

                            if (args.Length != 2)
                                throw new ArgumentException("Wrong number of args");

                            string lst = args[0];
                            string usr = args[1];

                            UserList list = StrToList(lst);

                            if (list == null)
                                throw new ArgumentException("Invalid list argument");

                            await list.AddUserAsync(Msgr.GetUser(usr));
                        }
                        break;

                    case "rem":
                        {
                            var args = line.Split(' ').Skip(1).ToArray();

                            if (args.Length != 2)
                                throw new ArgumentException("Wrong number of args");

                            string lst = args[0];
                            string usr = args[1];

                            UserList list = StrToList(lst);

                            if (list == null)
                                throw new ArgumentException("Invalid list argument");

                            await list.RemoveUserAsync(Msgr.GetUser(usr));
                        }
                        break;

                    case "im":
                        {

                            var args = line.Split(' ').Skip(1).ToArray();

                            if (args.Length == 0)
                                throw new ArgumentException("Specify the user who you wish to talk to");

                            string usr = args[0];

                            var imSession = await Msgr.CreateIMSession();

                            await imSession.InviteUserAsync(Msgr.GetUser(usr));
                        }
                        break;

                    case "msg":
                        {

                            string[] args = line.Split(new char[] { ' ' }, 2).Skip(1).ToArray();

                            if (args.Length == 0)
                                throw new ArgumentException("Enter a message");

                            string msg = args[0];

                            Message message = new Message();

                            if (RandomColor)
                            {
                                MessageFormatter mf = new MessageFormatter();
                                mf.SetRandomColor();
                                mf.ApplyFormat(message);
                            }
                            message.Body = UTF8Encoding.UTF8.GetBytes(msg);

                            foreach (IMSession im in Msgr.IMSessions)
                                await im.SendMessageAsync(message, MessageOption.NoAcknoweldgement);


                        }
                        break;

                    case "msg2":
                        {

                            string[] args = line.Split(new char[] { ' ' }, 2).Skip(1).ToArray();

                            if (args.Length == 0)
                                throw new ArgumentException("Enter a message");

                            string msg = args[0];

                            Message message = new Message();
                            message.Body = UTF8Encoding.UTF8.GetBytes(msg);

                            await Msgr.IMSessions.FirstOrDefault().SendMessageAsync(message, MessageOption.NegativeAcknowledgementOnly);


                        }
                        break;

                    case "disim":

                        foreach (IMSession im in Msgr.IMSessions)
                            await im.DisconnectAsync();

                        break;

                    case "parrot":
                        ParrotMode = !ParrotMode;

                        using (ConsoleExt.WithColor(infoColor))
                            Console.WriteLine("Parrot mode now " + ParrotMode);

                        break;

                    case "color":

                        RandomColor = !RandomColor;

                        using (ConsoleExt.WithColor(infoColor))
                            Console.WriteLine("Random color mode now " + RandomColor);

                        break;

                    case "closeim":

                        foreach (IMSession im in Msgr.IMSessions.ToList())
                            im.Close();

                        break;

                    case "inviteall":

                        foreach (User user in Msgr.UserLists.ForwardList.Users.Where(u => u.Status != UserStatus.Offline))
                            foreach (IMSession im in Msgr.IMSessions.Where(im => !im.Users.Contains(user)))
                                await im.InviteUserAsync(user);

                        break;

                    case "ping":
                        await Msgr.PingAsync();
                        break;

                    case "exit":
                        endEvent.Set();
                        break;

                    default:
                        throw new ArgumentException("Command not recognized");
                }

            }
            catch (TimeoutException ex)
            {
                using (ConsoleExt.WithColor(errorColor))
                    Console.WriteLine(ex.Message);
            }
            catch (NotLoggedInException ex)
            {
                using (ConsoleExt.WithColor(errorColor))
                    Console.WriteLine(ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                using (ConsoleExt.WithColor(errorColor))
                    Console.WriteLine(ex.Message);
            }
            catch (ConnectionErrorException ex)
            {
                using (ConsoleExt.WithColor(errorColor))
                    Console.WriteLine(ex.Message);
            }
            catch (ArgumentException ex)
            {
                using (ConsoleExt.WithColor(errorColor))
                    Console.WriteLine(ex.Message);
            }

            ThreadPool.QueueUserWorkItem(TakeInput);

        }


        void Msgr_LoggedIn(object sender, EventArgs a)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("Now logged in");
        }

        void Msgr_LoggedOut(object sender, LoggedOutEventArgs e)
        {

            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("Logged out");

        }

        void Msgr_ServerNotification(object sender, ServerNotificationEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
            {
                Console.WriteLine("Server notification:");
                Console.Write(e.Message);
                Console.WriteLine();
            }

        }

        void Msgr_UserStatusChanged(object sender, UserStatusEventArgs e) {

            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} changed status to {1}", e.User.Nickname ?? e.User.LoginName, e.Status);
        }

        void Msgr_UserDisplayPictureChanged(object sender, UserDisplayPictureEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} display picture changed", e.User.Nickname ?? e.User.LoginName);
        }

        void Msgr_UserCapabilitiesChanged(object sender, UserCapabilitiesEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} capabilities changed to {1}", e.User.Nickname ?? e.User.LoginName, e.Capabilities);
        }

        void Msgr_UserPropertyChanged(object sender, UserPropertyEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} property {1} changed to '{2}'", e.User.Nickname ?? e.User.LoginName, e.Property, e.Value);
        }

        void Msgr_PrivacySettingChanged(object sender, PrivacySettingEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} privacy setting changed to {1}", e.PrivacySetting, e.Value);
        }

        void Msgr_UserNicknameChanged(object sender, UserNicknameEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} name changed to {1}", e.PreviousNickname ?? "(None)", e.Nickname ?? "(None)");
        }

        void Msgr_MessageReceived(object sender, MessageEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("Server message from {0}", e.SenderNickname);
        }

        async void Msgr_InvitedToIMSession(object sender, InvitationEventArgs e)
        {

            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("Accepting IM Session invitation from {0}", e.Invitation.InvitingUser.Nickname ?? e.Invitation.InvitingUser.LoginName);

            IMSession imSession = null;

            try
            {
                imSession = await Msgr.AcceptInvitationAsync(e.Invitation);
            }
            catch (ConnectionErrorException ex)
            {
                using (ConsoleExt.WithColor(errorColor))
                    Console.WriteLine("Connection error while esablishing IM session: " + ex.Message);

                return;
            }

            Message message = new Message();

            MessageFormatter mf = new MessageFormatter();
            mf.Italic = true;
            mf.Color = Color.Tomato;
            mf.ApplyFormat(message);

            message.Body = UTF8Encoding.UTF8.GetBytes(String.Format("Gee, thanks for inviting me {0}", e.Invitation.InvitingUser.Nickname ?? e.Invitation.InvitingUser.LoginName));

            await imSession.SendMessageAsync(message);
        }

        void Msgr_UserRemovedFromList(object sender, UserListUserEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} removed from {1}", e.User.Nickname ?? e.User.LoginName, e.UserList);
        }

        void Msgr_UserAddedToList(object sender, UserListUserEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} added to {1}", e.User.Nickname ?? e.User.LoginName, e.UserList);
        }

        void Msgr_GroupNameChanged(object sender, GroupNameEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("Group '{0}' name changed to '{1}'", e.PreviousName, e.Name);
        }

        void Msgr_GroupAdded(object sender, GroupEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("Group '{0}' added", e.Group.Name);
        }

        void Msgr_GroupRemoved(object sender, GroupEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("Group '{0}' removed", e.Group.Name);
        }

        void Msgr_UserAddedToGroup(object sender, GroupUserEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} added to group '{1}'", e.User.Nickname ?? e.User.LoginName, e.Group.Name);
        }

        void Msgr_UserRemovedFromGroup(object sender, GroupUserEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} removed from group '{1}'", e.User.Nickname ?? e.User.LoginName, e.Group.Name);
        }

        void Msgr_UserBroadcast(object sender, UserBroadcastEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("Broadcast from {0}: {1}...", e.User.Nickname ?? e.User.LoginName, e.Message.Substring(0, 10));
        }

        void Msgr_IMSessionCreated(object sender, IMSessionEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("An IM session has been created");

            AddIMhandlers(e.IMSession);
        }

        void IMSession_Closed(object sender, EventArgs e)
        {

            var imSession = (IMSession)sender;

            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("An IM session has been closed");

            RemoveIMhandlers(imSession);
        }

        void IMSession_UserParted(object sender, UserEventArgs e)
        {
            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} left an IM session", e.User.Nickname ?? e.User.LoginName);
        }

        async void IMSession_UserJoined(object sender, UserEventArgs e)
        {

            var imSession = (IMSession)sender;

            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0} joined an IM session", e.User.Nickname ?? e.User.LoginName);

            Message message = new Message();

            MessageFormatter mf = new MessageFormatter();
            mf.Color = Color.Purple;
            mf.Bold = true;
            mf.ApplyFormat(message);

            message.Body = UTF8Encoding.UTF8.GetBytes(String.Format("Hey, you joined {0}", e.User.LoginName ?? e.User.Nickname));

            await imSession.SendMessageAsync(message);
        }


        async void IMSession_MessageReceived(object sender, MessageEventArgs e)
        {
            var imSession = (IMSession)sender;

            if (e.Message.Headers.ContainsKey("TypingUser") && ParrotMode)
            {
                e.Message.Headers["TypingUser"] = Msgr.LocalUser.LoginName;
                await imSession.SendMessageAsync(e.Message, MessageOption.NoAcknoweldgement);
                return;
            }

            if (e.Message.ContentType != "text/plain; charset=UTF-8")
                return;

            using (ConsoleExt.WithColor(eventColor))
                Console.WriteLine("{0}: {1}", e.Sender.Nickname ?? e.Sender.LoginName, Encoding.UTF8.GetString(e.Message.Body));

            if (!ParrotMode)
                return;

            if (RandomColor)
            {
                MessageFormatter mf = new MessageFormatter();
                mf.SetRandomColor();
                mf.ApplyFormat(e.Message);
            }

            await imSession.SendMessageAsync(e.Message, MessageOption.NoAcknoweldgement);

        }

        void AddHandlers()
        {
            Msgr.LoggedIn += new EventHandler(Msgr_LoggedIn);
            Msgr.LoggedOut += new EventHandler<LoggedOutEventArgs>(Msgr_LoggedOut);
            Msgr.UserStatusChanged += new EventHandler<UserStatusEventArgs>(Msgr_UserStatusChanged);
            Msgr.UserDisplayPictureChanged += new EventHandler<UserDisplayPictureEventArgs>(Msgr_UserDisplayPictureChanged);
            Msgr.UserCapabilitiesChanged += new EventHandler<UserCapabilitiesEventArgs>(Msgr_UserCapabilitiesChanged);
            Msgr.UserNicknameChanged += new EventHandler<UserNicknameEventArgs>(Msgr_UserNicknameChanged);
            Msgr.UserPropertyChanged += new EventHandler<UserPropertyEventArgs>(Msgr_UserPropertyChanged);
            Msgr.MessageReceived += new EventHandler<MessageEventArgs>(Msgr_MessageReceived);
            Msgr.IMSessionCreated += new EventHandler<IMSessionEventArgs>(Msgr_IMSessionCreated);
            Msgr.InvitedToIMSession += new EventHandler<InvitationEventArgs>(Msgr_InvitedToIMSession);
            Msgr.UserAddedToList += new EventHandler<UserListUserEventArgs>(Msgr_UserAddedToList);
            Msgr.UserRemovedFromList += new EventHandler<UserListUserEventArgs>(Msgr_UserRemovedFromList);
            Msgr.GroupNameChanged += new EventHandler<GroupNameEventArgs>(Msgr_GroupNameChanged);
            Msgr.GroupAdded += new EventHandler<GroupEventArgs>(Msgr_GroupAdded);
            Msgr.GroupRemoved += new EventHandler<GroupEventArgs>(Msgr_GroupRemoved);
            Msgr.UserAddedToGroup += new EventHandler<GroupUserEventArgs>(Msgr_UserAddedToGroup);
            Msgr.UserRemovedFromGroup += new EventHandler<GroupUserEventArgs>(Msgr_UserRemovedFromGroup);
            Msgr.PrivacySettingChanged += new EventHandler<PrivacySettingEventArgs>(Msgr_PrivacySettingChanged);
            Msgr.ServerNotification += new EventHandler<ServerNotificationEventArgs>(Msgr_ServerNotification);
            Msgr.UserBroadcast += new EventHandler<UserBroadcastEventArgs>(Msgr_UserBroadcast);
        }

        void RemoveHandlers()
        {
            Msgr.LoggedIn -= new EventHandler(Msgr_LoggedIn);
            Msgr.LoggedOut -= new EventHandler<LoggedOutEventArgs>(Msgr_LoggedOut);
            Msgr.UserStatusChanged -= new EventHandler<UserStatusEventArgs>(Msgr_UserStatusChanged);
            Msgr.UserDisplayPictureChanged -= new EventHandler<UserDisplayPictureEventArgs>(Msgr_UserDisplayPictureChanged);
            Msgr.UserCapabilitiesChanged -= new EventHandler<UserCapabilitiesEventArgs>(Msgr_UserCapabilitiesChanged);
            Msgr.UserNicknameChanged -= new EventHandler<UserNicknameEventArgs>(Msgr_UserNicknameChanged);
            Msgr.UserPropertyChanged -= new EventHandler<UserPropertyEventArgs>(Msgr_UserPropertyChanged);
            Msgr.MessageReceived -= new EventHandler<MessageEventArgs>(Msgr_MessageReceived);
            Msgr.IMSessionCreated -= new EventHandler<IMSessionEventArgs>(Msgr_IMSessionCreated);
            Msgr.InvitedToIMSession -= new EventHandler<InvitationEventArgs>(Msgr_InvitedToIMSession);
            Msgr.UserAddedToList -= new EventHandler<UserListUserEventArgs>(Msgr_UserAddedToList);
            Msgr.UserRemovedFromList -= new EventHandler<UserListUserEventArgs>(Msgr_UserRemovedFromList);
            Msgr.GroupNameChanged -= new EventHandler<GroupNameEventArgs>(Msgr_GroupNameChanged);
            Msgr.GroupAdded -= new EventHandler<GroupEventArgs>(Msgr_GroupAdded);
            Msgr.GroupRemoved -= new EventHandler<GroupEventArgs>(Msgr_GroupRemoved);
            Msgr.UserAddedToGroup -= new EventHandler<GroupUserEventArgs>(Msgr_UserAddedToGroup);
            Msgr.UserRemovedFromGroup -= new EventHandler<GroupUserEventArgs>(Msgr_UserRemovedFromGroup);
            Msgr.PrivacySettingChanged -= new EventHandler<PrivacySettingEventArgs>(Msgr_PrivacySettingChanged);
            Msgr.ServerNotification -= new EventHandler<ServerNotificationEventArgs>(Msgr_ServerNotification);
            Msgr.UserBroadcast -= new EventHandler<UserBroadcastEventArgs>(Msgr_UserBroadcast);
        }

        void AddIMhandlers(IMSession imSession)
        {
            imSession.Closed += new EventHandler(IMSession_Closed);
            imSession.UserJoined += new EventHandler<UserEventArgs>(IMSession_UserJoined);
            imSession.UserParted += new EventHandler<UserEventArgs>(IMSession_UserParted);
            imSession.MessageReceived += new EventHandler<MessageEventArgs>(IMSession_MessageReceived);
        }

        void RemoveIMhandlers(IMSession imSession)
        {
            imSession.UserJoined -= new EventHandler<UserEventArgs>(IMSession_UserJoined);
            imSession.UserParted -= new EventHandler<UserEventArgs>(IMSession_UserParted);
            imSession.MessageReceived -= new EventHandler<MessageEventArgs>(IMSession_MessageReceived);
            imSession.Closed -= new EventHandler(IMSession_Closed);
        }


        UserStatus StrToStatus(string statusString)
        {

            switch (statusString)
            {
                case "online":
                    return UserStatus.Online;
                case "hidden":
                    return UserStatus.Invisible;
                case "away":
                    return UserStatus.Away;
                case "idle":
                    return UserStatus.Idle;
                case "busy":
                    return UserStatus.Busy;
                case "brb":
                    return UserStatus.BeRightBack;
                case "lunch":
                    return UserStatus.Lunch;
                case "phone":
                    return UserStatus.Phone;
                default:
                    return UserStatus.Offline;
            }

        }

        UserList StrToList(string listString)
        {

            switch (listString)
            {
                case "fl":
                    return Msgr.UserLists.ForwardList;
                case "bl":
                    return Msgr.UserLists.BlockList;
                case "al":
                    return Msgr.UserLists.AllowList;
                case "rl":
                    return Msgr.UserLists.ReverseList;
                case "pl":
                    return Msgr.UserLists.PendingList;
                default:
                    return null;
            }

        }

    }


}
