using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessengerLibrary.MSNP;
using System.Reactive;
using System.Reactive.Linq;
using MessengerLibrary.IO;
using System.Reactive.Threading.Tasks;

namespace MessengerLibrary
{

    public class User
    {

        protected MessengerClient client;
        Dictionary<UserProperty, string> properties = new Dictionary<UserProperty, string>();
        internal string guid;

        internal User(MessengerClient client, string loginName)
            : this(client, loginName, loginName)
        {
        }

        internal User(MessengerClient client, string loginName, string nickname)
        {
            this.client = client;
            LoginName = loginName;
            Nickname = nickname;
        }

        public string LoginName { get; private set; }
        public string Nickname { get; internal set; }
        public UserStatus Status { get; internal set; }
        public MSNObject DisplayPicture { get; internal set; }
        public UserCapabilities Capabilities { get; internal set; }

        public string GetProperty(UserProperty property)
        {
            return GetPropertyInner(property);
        }

        internal string GetPropertyInner(UserProperty property)
        {
            if (properties.ContainsKey(property))
                return properties[property];

            return String.Empty;
        }

        internal void SetPropertyInner(UserProperty property, string value)
        {

            if (value == String.Empty && properties.ContainsKey(property))
                properties.Remove(property);

            else if (properties.ContainsKey(property))
                properties[property] = value;

            else
                properties.Add(property, value);

        }

        internal void UpdateNicknameFromIM(string nickname)
        {
            if (!client.UserLists.ForwardList.Users.Contains(this))
                Nickname = nickname;
        }

        public override string ToString()
        {
            return LoginName;
        }

        internal static string PropertyToString(UserProperty property)
        {
            switch (property)
            {
                case UserProperty.HomePhone:
                    return "PHH";
                case UserProperty.WorkPhone:
                    return "PHW";
                case UserProperty.MobilePhone:
                    return "PHM";
                case UserProperty.AuthorizedMobile:
                    return "MOB";
                case UserProperty.MobileDevice:
                    return "MBE";
                case UserProperty.MSNDirectDevice:
                    return "WWE";
                case UserProperty.HasBlog:
                    return "HSB";
                default:
                    throw new NotSupportedException();
            }
        }

        internal static UserProperty StringToProperty(string property)
        {
            switch (property)
            {
                case "PHH":
                    return UserProperty.HomePhone;
                case "PHW":
                    return UserProperty.WorkPhone;
                case "PHM":
                    return UserProperty.MobilePhone;
                case "MOB":
                    return UserProperty.AuthorizedMobile;
                case "MBE":
                    return UserProperty.MobileDevice;
                case "WWE":
                    return UserProperty.MSNDirectDevice;
                case "HSB":
                    return UserProperty.HasBlog;
                default:
                    throw new NotSupportedException();
            }
        }

        internal static string StatusToString(UserStatus status)
        {
            switch (status)
            {
                case UserStatus.Online:
                    return "NLN";
                case UserStatus.Away:
                    return "AWY";
                case UserStatus.Busy:
                    return "BSY";
                case UserStatus.BeRightBack:
                    return "BRB";
                case UserStatus.Invisible:
                    return "HDN";
                case UserStatus.Lunch:
                    return "LUN";
                case UserStatus.Phone:
                    return "PHN";
                case UserStatus.Idle:
                    return "IDL";
                default:
                    throw new NotSupportedException();
            }
        }

        internal static UserStatus StringToStatus(string status)
        {
            switch (status)
            {
                case "NLN":
                    return UserStatus.Online;
                case "AWY":
                    return UserStatus.Away;
                case "BSY":
                    return UserStatus.Busy;
                case "BRB":
                    return UserStatus.BeRightBack;
                case "HDN":
                    return UserStatus.Invisible;
                case "LUN":
                    return UserStatus.Lunch;
                case "PHN":
                    return UserStatus.Phone;
                case "IDL":
                    return UserStatus.Idle;
                case "FLN":
                    return UserStatus.Offline;
                default:
                    throw new NotSupportedException();
            }
        }

        public static readonly string NoPhoneNumber = String.Empty;

        public static readonly string MobileDeviceEnabled = "Y";

        public static readonly string MobileDeviceDisabled = "N";

        public static readonly string AuthorizedMobileEnabled = "Y";

        public static readonly string AuthorizedMobileDisabled = "N";

        public static readonly string MSNDirectDeviceEnabled = "2";

        public static readonly string MSNDirectDeviceDisabled = "0";

    }

    public class LocalUser : User
    {

        internal LocalUser(MessengerClient client, string loginName)
            : base(client, loginName)
        {
        }

        public async Task ChangeNicknameAsync(string nickname)
        {

            if (String.IsNullOrEmpty(nickname))
                throw new ArgumentNullException("A nickname must be specified.");

            if (Encoding.UTF8.GetByteCount(nickname) > 387)
                throw new ArgumentException("The specified nickname is too long.");

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (nickname == Nickname)
                    throw new ArgumentException("This nickname is already set.");

                Command cmd = new LocalPropertyCommand("MFN", nickname);
                await client.responseTracker.GetResponseAsync<LocalPropertyCommand>(cmd, client.defaultTimeout);

                string prevNickname = Nickname;
                Nickname = nickname;

                client.OnUserNicknameChanged(new UserNicknameEventArgs(this, nickname, prevNickname, false));


            }

            catch (ServerErrorException ex)
            {
                if (ex.ServerError == ServerError.NicknameChangeIllegal)
                    throw new ArgumentException("Illegal nickname.");

                if (ex.ServerError == ServerError.ChangingTooRapidly)
                    throw new ChangingTooRapidlyException();

                throw;

            }
            finally
            {
                client.@lock.ReaderRelease();
            }


        }

        public async Task ChangeStatusAsync(UserStatus status)
        {

            if (status == UserStatus.Offline)
                throw new ArgumentException("Cannot change status to offline.");

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (status == Status)
                    throw new ArgumentException("This status is already set.");

                Command cmd = new ChangeStatusCommand(StatusToString(status), (uint)Capabilities, DisplayPicture.ToString());
                await client.responseTracker.GetResponseAsync<ChangeStatusCommand>(cmd, client.defaultTimeout);

                UserStatus prevStatus = Status;
                Status = status;

                client.OnUserStatusChanged(new UserStatusEventArgs(this, status, prevStatus, false));

            }
            catch (ServerErrorException ex)
            {
                if (ex.ServerError == ServerError.ChangingTooRapidly)
                    throw new ChangingTooRapidlyException();

                throw;
            }

            finally
            {
                client.@lock.ReaderRelease();
            }


        }

        public async Task ChangeCapabilitiesAsync(UserCapabilities capabilities)
        {

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (capabilities == Capabilities)
                    throw new ArgumentException("Those capabilities are already set.");

                Command cmd = new ChangeStatusCommand(StatusToString(Status), (uint)capabilities, DisplayPicture != MSNObject.Empty ? DisplayPicture.ToString() : "0");
                await client.responseTracker.GetResponseAsync<ChangeStatusCommand>(cmd, client.defaultTimeout);

                Capabilities = capabilities;

                client.OnUserCapabilitiesChanged(new UserCapabilitiesEventArgs(this, capabilities, false));

            }
            catch (ServerErrorException ex)
            {
                if (ex.ServerError == ServerError.ChangingTooRapidly)
                    throw new ChangingTooRapidlyException();

                throw;
            }
            finally
            {
                client.@lock.ReaderRelease();
            }
        }

        public async Task ChangeDisplayPictureAsync(MSNObject displayPicture)
        {
            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (displayPicture == DisplayPicture)
                    throw new ArgumentException("That display picture is already set.");

                Command cmd = new ChangeStatusCommand(StatusToString(Status), (uint)Capabilities, displayPicture != MSNObject.Empty ? displayPicture.ToString() : "0");
                await client.responseTracker.GetResponseAsync<ChangeStatusCommand>(cmd, client.defaultTimeout);

                DisplayPicture = displayPicture;

                client.OnUserDisplayPictureChanged(new UserDisplayPictureEventArgs(this, displayPicture, false));

            }
            catch (ServerErrorException ex)
            {
                if (ex.ServerError == ServerError.ChangingTooRapidly)
                    throw new ChangingTooRapidlyException();

                throw;
            }
            finally
            {
                client.@lock.ReaderRelease();
            }
        }

        public async Task BroadcastAsync(string message)
        {

            if (message.Length >= 999)
                throw new ArgumentException("The specified message was too long.");

            if (String.IsNullOrEmpty(message))
                throw new ArgumentNullException("A message must be specified.");

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                Command cmd = new SendBroadcastCommand(message);
                await client.responseTracker.GetResponseAsync<SendBroadcastCommand>(cmd, client.defaultTimeout);

            }

            finally
            {
                client.@lock.ReaderRelease();
            }

        }

        public async Task SetPropertyAsync(UserProperty property, string value)
        {

            if (new UserProperty[] { UserProperty.HomePhone, UserProperty.WorkPhone, UserProperty.MobilePhone }.Contains(property))
            {
                if (value == null)
                    throw new ArgumentNullException("Property value must be specified.");

                if (Encoding.UTF8.GetByteCount(value) > 95)
                    throw new ArgumentException("Property value too long.", "value");
            }

            if (property == UserProperty.HasBlog)
                throw new InvalidOperationException("Property cannot be set by the user.");

            if (property == UserProperty.MobileDevice &&
                (value != User.MobileDeviceEnabled && value != User.MobileDeviceDisabled))
                throw new ArgumentException("Invalid property value.", "value");

            if (property == UserProperty.AuthorizedMobile && value == User.AuthorizedMobileEnabled &&
                (client.LocalUser.GetPropertyInner(UserProperty.MobileDevice) != User.MobileDeviceEnabled))
                throw new InvalidOperationException("Mobile device must be enabled first.");

            if (property == UserProperty.AuthorizedMobile &&
                (value != User.AuthorizedMobileEnabled && value != User.AuthorizedMobileDisabled))
                throw new ArgumentException("Invalid property value.");

            if (property == UserProperty.MSNDirectDevice &&
                (value != User.MSNDirectDeviceEnabled && value != User.MSNDirectDeviceDisabled))
                throw new ArgumentException("Invalid property value.");

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (value == GetPropertyInner(property))
                    throw new ArgumentException("Property value already set.");

                Command cmd = new LocalPropertyCommand(User.PropertyToString(property), value);
                await client.responseTracker.GetResponseAsync<LocalPropertyCommand>(cmd, client.defaultTimeout);

                client.LocalUser.SetPropertyInner(property, value);

                client.OnUserPropertyChanged(new UserPropertyEventArgs(this, property, value, false));

            }
            finally
            {
                client.@lock.ReaderRelease();
            }

        }




    }

    public struct MSNObject
    {

        public static readonly MSNObject Empty;

        string data;

        MSNObject(string s)
        {
            data = s;
        }

        public override bool Equals(Object obj)
        {
            return obj is MSNObject && this == (MSNObject)obj;
        }

        public override int GetHashCode()
        {
            return data.GetHashCode();
        }

        public static bool operator ==(MSNObject x, MSNObject y)
        {
            return x.data == y.data;
        }

        public static bool operator !=(MSNObject x, MSNObject y)
        {
            return !(x == y);
        }

        public override string ToString()
        {
            return data;
        }

        public static MSNObject Parse(string s)
        {
            return new MSNObject(s);
        }

    }

    public class Users : ReadOnlyCollection<User>
    {

        internal Users()
            : base(new List<User>())
        {
        }

        internal void AddUserInner(User user)
        {
            Items.Add(user);
        }

        internal void RemoveUserInner(User user)
        {
            Items.Remove(user);
        }

    }


}
