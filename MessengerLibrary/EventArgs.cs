using System;
using MessengerLibrary.Connections;

namespace MessengerLibrary
{

    public class ServerNotificationEventArgs : EventArgs
    {

        internal ServerNotificationEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }

    }

    public class PrivacySettingEventArgs : EventArgs
    {

        internal PrivacySettingEventArgs(PrivacySetting privacySetting, string value, bool loginEvent)
        {
            PrivacySetting = privacySetting;
            Value = value;
            LoginEvent = LoginEvent;
        }

        public PrivacySetting PrivacySetting { get; private set; }
        public string Value { get; private set; }
        public bool LoginEvent { get; private set; }
    }

    public class LoggedOutEventArgs : EventArgs
    {

        internal LoggedOutEventArgs(LogoutReason reason, ConnectionErrorException connectionError)
        {
            Reason = reason;
            ConnectionError = connectionError;
        }

        public LogoutReason Reason { get; private set; }
        public ConnectionErrorException ConnectionError { get; private set; }

    }

    public class UserBroadcastEventArgs : EventArgs
    {

        internal UserBroadcastEventArgs(User user, string message)
        {
            User = user;
            Message = message;
        }

        public User User { get; private set; }
        public string Message { get; private set; }

    }

    public class UserNicknameEventArgs : EventArgs
    {

        internal UserNicknameEventArgs(User user, string nickname, string previousNickname, bool loginEvent)
        {
            User = user;
            Nickname = nickname;
            PreviousNickname = previousNickname;
            LoginEvent = LoginEvent;
        }

        public User User { get; private set; }
        public string Nickname { get; private set; }
        public string PreviousNickname { get; private set; }
        public bool LoginEvent { get; private set; }
    }

    public class UserStatusEventArgs : EventArgs
    {

        internal UserStatusEventArgs(User user, UserStatus userStatus, UserStatus previousStatus, bool loginEvent)
        {
            User = user;
            Status = userStatus;
            PreviousStatus = previousStatus;
            LoginEvent = loginEvent;
        }

        public User User { get; private set; }
        public UserStatus Status { get; private set; }
        public UserStatus PreviousStatus { get; private set; }
        public bool LoginEvent { get; private set; }

    }

    public class UserDisplayPictureEventArgs : EventArgs
    {

        internal UserDisplayPictureEventArgs(User user, MSNObject displayPicture, bool loginEvent)
        {
            User = user;
            DisplayPicture = displayPicture;
            LoginEvent = LoginEvent;
        }

        public User User { get; private set; }
        public MSNObject DisplayPicture { get; private set; }
        public bool LoginEvent { get; private set; }
    }

    public class UserCapabilitiesEventArgs : EventArgs
    {

        internal UserCapabilitiesEventArgs(User user, UserCapabilities capabilities, bool loginEvent)
        {
            User = user;
            Capabilities = capabilities;
            LoginEvent = LoginEvent;
        }

        public User User { get; private set; }
        public UserCapabilities Capabilities { get; private set; }
        public bool LoginEvent { get; private set; }
    }

    public class UserPropertyEventArgs : EventArgs
    {

        internal UserPropertyEventArgs(User user, UserProperty property, string value, bool loginEvent)
        {
            User = user;
            Property = property;
            Value = value;
            LoginEvent = LoginEvent;
        }

        public User User { get; private set; }
        public UserProperty Property { get; private set; }
        public string Value { get; private set; }
        public bool LoginEvent { get; private set; }

    }

    public class IMSessionEventArgs : EventArgs
    {

        internal IMSessionEventArgs(IMSession imSession, IMSessionInvitation invitation)
        {
            IMSession = imSession;
            Invitation = invitation;
        }

        public IMSession IMSession { get; private set; }
        public IMSessionInvitation Invitation { get; private set; }

    }

    public class InvitationEventArgs : EventArgs
    {

        internal InvitationEventArgs(IMSessionInvitation invitation)
        {
            Invitation = invitation;
        }

        public IMSessionInvitation Invitation { get; private set; }

    }

    public class UserListUserEventArgs : EventArgs
    {

        internal UserListUserEventArgs(User user, UserList userList, bool loginEvent)
        {
            User = user;
            UserList = userList;
            LoginEvent = LoginEvent;
        }

        public User User { get; private set; }
        public UserList UserList { get; private set; }
        public bool LoginEvent { get; private set; }
    }

    public class GroupUserEventArgs : EventArgs
    {

        internal GroupUserEventArgs(User user, Group group, bool loginEvent)
        {
            User = user;
            Group = group;
            LoginEvent = loginEvent;
        }

        public User User { get; private set; }
        public Group Group { get; private set; }
        public bool LoginEvent { get; private set; }
    }

    public class GroupNameEventArgs : EventArgs
    {

        internal GroupNameEventArgs(Group group, string name, string previousName, bool loginEvent)
        {
            Group = group;
            Name = name;
            PreviousName = previousName;
            LoginEvent = LoginEvent;
        }

        public Group Group { get; private set; }
        public string Name { get; private set; }
        public string PreviousName { get; private set; }
        public bool LoginEvent { get; private set; }
    }

    public class GroupEventArgs : EventArgs
    {

        internal GroupEventArgs(Group group, bool loginEvent)
        {
            Group = group;
            LoginEvent = LoginEvent;
        }

        public Group Group { get; private set; }
        public bool LoginEvent { get; private set; }

    }

    public class UserEventArgs : EventArgs
    {

        internal UserEventArgs(User user)
        {
            User = user;
        }

        public User User { get; private set; }

    }

    public class MessageEventArgs : EventArgs
    {

        internal MessageEventArgs(User user, Message message)
        {
            Sender = user;
            Message = message;
        }

        internal MessageEventArgs(string senderLoginName, string senderNickname, Message message, bool loginEvent)
        {
            SenderLoginName = senderLoginName;
            SenderNickname = senderNickname;
            Message = message;
            LoginEvent = loginEvent;
        }

        public User Sender { get; private set; }
        public Message Message { get; private set; }
        public string SenderLoginName { get; private set; }
        public string SenderNickname { get; private set; }
        public bool LoginEvent { get; private set; }
    }

}
