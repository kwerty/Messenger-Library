using System;
using MessengerLibrary.Connections;

namespace MessengerLibrary
{


    public class ServerErrorException : Exception
    {

        internal ServerErrorException(int errorCode)
            : base("Server returned an error")
        {
            ErrorCode = errorCode;
            ServerError = (ServerError)errorCode;
        }

        public ServerError ServerError { get; private set; }
        public int ErrorCode { get; private set; }


    }

    public class ProtocolNotAcceptedException : Exception
    {
        internal ProtocolNotAcceptedException()
            : base("This protocol is no longer accepted.")
        {
        }
    }

    public class NotLoggedInException : Exception
    {
        internal NotLoggedInException()
            : base("Not logged in.")
        {
        }
    }

    public class UserAlreadyExistsException : Exception
    {
        internal UserAlreadyExistsException(string message)
            : base(message)
        {
        }
    }

    public class NoParticpantsException : Exception
    {
        internal NoParticpantsException(string message)
            : base(message)
        {
        }
    }

    public class UserNotOnlineException : Exception
    {
        internal UserNotOnlineException()
            : base("User is not online.")
        {
        }
    }


    public class MessageDeliveryFailedException : Exception
    {
        internal MessageDeliveryFailedException()
            : base("This message could not be delivered.")
        {
        }
    }


    public class ChangingTooRapidlyException : Exception
    {
        internal ChangingTooRapidlyException()
            : base("Changing too rapidly")
        {
        }
    }

}



