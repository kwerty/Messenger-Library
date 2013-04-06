using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessengerLibrary.MSNP;

namespace MessengerLibrary
{

    public class Group
    {

        MessengerClient client;

        internal Group(MessengerClient client, string guid, string name)
        {
            this.client = client;
            Guid = guid;
            Name = name;
            Users = new Users();
        }

        public string Guid { get; internal set; }

        public string Name { get; internal set; }

        public Users Users { get; private set; }

        public async Task ChangeNameAsync(string name)
        {

            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("A name must be specified.");

            if (Encoding.UTF8.GetByteCount(name) > 61)
                throw new ArgumentException("The name specified was too long.");

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (!client.Groups.Contains(this))
                    throw new InvalidOperationException("This group is no longer in use.");

                if (name == Name)
                    throw new ArgumentException("This name is already set.");

                if (client.Groups.Where(g => name == g.Name).Count() > 1)
                    throw new ArgumentException("A group with this name already exists.");

                Command cmd = new RenameGroupCommand(this.Guid, name);
                await client.responseTracker.GetResponseAsync<RenameGroupCommand>(cmd, client.defaultTimeout);

                string prevName = Name;
                Name = name;

                client.OnGroupNameChanged(new GroupNameEventArgs(this, Name, prevName, false));

            }

            finally
            {
                client.@lock.ReaderRelease();
            }




        }

        public async Task AddUserAsync(User user)
        {

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (!client.Groups.Contains(this))
                    throw new InvalidOperationException("This group is no longer in use");

                if (!client.UserLists.ForwardList.Users.Contains(user))
                    throw new InvalidOperationException("User must be added to the forward list first.");

                if (Users.Contains(user))
                    throw new InvalidOperationException("User already exists in group.");

                Command cmd = new AddContactCommand(client.UserLists.ForwardList.listCode, user.guid, null, Guid);
                await client.responseTracker.GetResponseAsync<AddContactCommand>(cmd, client.defaultTimeout);

                Users.AddUserInner(user);

                client.OnUserAddedToGroup(new GroupUserEventArgs(user, this, false));

            }

            finally
            {
                client.@lock.ReaderRelease();
            }




        }

        public async Task RemoveUserAsync(User user)
        {

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (!client.Groups.Contains(this))
                    throw new InvalidOperationException("This group is no longer in use.");

                if (!Users.Contains(user))
                    throw new InvalidOperationException("User does not exist in group.");

                Command cmd = new RemoveContactCommand(client.UserLists.ForwardList.listCode, user.guid, Guid);
                await client.responseTracker.GetResponseAsync<RemoveContactCommand>(cmd, client.defaultTimeout);

                Users.RemoveUserInner(user);

                client.OnUserRemovedFromGroup(new GroupUserEventArgs(user, this, false));

            }

            finally
            {
                client.@lock.ReaderRelease();
            }

        }

        public override string ToString()
        {
            return Name;
        }

    }

    public class Groups : ReadOnlyCollection<Group>
    {

        internal Groups()
            : base(new List<Group>())
        { }

        internal void AddGroup(Group group)
        {
            Items.Add(group);
        }

        internal void RemoveGroup(Group group)
        {
            Items.Remove(group);
        }

    }


}
