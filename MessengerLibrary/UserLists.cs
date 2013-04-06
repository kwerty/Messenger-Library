using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessengerLibrary.MSNP;

namespace MessengerLibrary
{


    public abstract class UserList
    {

        MessengerClient client;
        internal string listCode;

        public Users Users { get; private set; }

        internal UserList(MessengerClient client, string listCode)
        {
            this.client = client;
            this.listCode = listCode;

            Users = new Users();
        }

        public virtual async Task AddUserAsync(User user)
        {

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (Users.Contains(user))
                    throw new InvalidOperationException("User already exists in list.");

                Command cmd = new AddContactCommand(this.listCode, user.LoginName, this is ForwardList ? user.Nickname ?? user.LoginName : null);
                AddContactCommand response = await client.responseTracker.GetResponseAsync<AddContactCommand>(cmd, client.defaultTimeout);

                if (this is ForwardList)
                    user.guid = response.Guid;

                Users.AddUserInner(user);

                client.OnUserAddedToList(new UserListUserEventArgs(user, this, false));

            }

            finally
            {
                client.@lock.ReaderRelease();
            }


        }

        public virtual async Task RemoveUserAsync(User user)
        {

            await client.@lock.ReaderLockAsync();

            try
            {

                if (client.closed)
                    throw new ObjectDisposedException(client.GetType().Name);

                if (!client.IsLoggedIn)
                    throw new NotLoggedInException();

                if (!Users.Contains(user))
                    throw new InvalidOperationException("User does not exist in list.");

                if (this is ForwardList && client.Groups.Where(g => g.Users.Contains(user)).Count() > 0)
                    throw new InvalidOperationException("Remove user from all groups first.");

                Command cmd = new RemoveContactCommand(this.listCode, this is ForwardList ? user.guid : user.LoginName);
                await client.responseTracker.GetResponseAsync<RemoveContactCommand>(cmd, client.defaultTimeout);

                if (this is ForwardList)
                    user.guid = null;

                Users.RemoveUserInner(user);

                client.OnUserRemovedFromList(new UserListUserEventArgs(user, this, false));

            }

            finally
            {
                client.@lock.ReaderRelease();
            }

        }

    }

    public class ForwardList : UserList
    {
        internal ForwardList(MessengerClient client)
            : base(client, "FL") 
        { }

        public override string ToString()
        {
            return "Forward list";
        }
    }

    public class AllowList : UserList
    {
        internal AllowList(MessengerClient client)
            : base(client, "AL")
        { }

        public override string ToString()
        {
            return "Allow list";
        }
    }

    public class BlockList : UserList
    {
        internal BlockList(MessengerClient client)
            : base(client, "BL")
        { }

        public override string ToString()
        {
            return "Block list";
        }
    }

    public class ReverseList : UserList
    {
        internal ReverseList(MessengerClient client)
            : base(client, "RL") 
        { }

        public override string ToString()
        {
            return "Reverse list";
        }
    }

    public class PendingList : UserList
    {
        internal PendingList(MessengerClient client)
            : base(client, "PL") 
        { }

        public override Task AddUserAsync(User user)
        {
            throw new InvalidOperationException();
        }

        public override Task RemoveUserAsync(User user)
        {
            throw new InvalidOperationException();
        }

        public override string ToString()
        {
            return "Pending list";
        }
    }

    public class UserLists : ReadOnlyCollection<UserList>
    {

        internal UserLists(MessengerClient client)
            : base(new List<UserList>())
        {

            AllowList = new AllowList(client);
            BlockList = new BlockList(client);
            ForwardList = new ForwardList(client);
            ReverseList = new ReverseList(client);
            PendingList = new PendingList(client);

            Items.Add(AllowList);
            Items.Add(BlockList);
            Items.Add(ForwardList);
            Items.Add(ReverseList);
            Items.Add(PendingList);

        }

        public ForwardList ForwardList { get; private set; }
        public BlockList BlockList { get; private set; }
        public ReverseList ReverseList { get; private set; }
        public AllowList AllowList { get; private set; }
        public PendingList PendingList { get; private set; }

    }

}
