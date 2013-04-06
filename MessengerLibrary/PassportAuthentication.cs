using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MessengerLibrary.Authentication
{

    public class PassportAuthentication 
    {

        public async Task<string> GetToken(string loginName, string password, string authTicket)
        {

            try
            {

                HttpWebRequest redirectRequest = HttpWebRequest.Create("https://nexus.passport.com/rdr/pprdr.asp") as HttpWebRequest;
                HttpWebResponse redirectResponse = await redirectRequest.GetResponseAsync() as HttpWebResponse;

                //splits a string like this into a dict
                //something=blue,somethingelse=green,somethingelse2=orange

                Dictionary<string, string> passportURLs = null;

                using (redirectResponse)
                    passportURLs = redirectResponse.Headers["PassportURLs"]
                        .Split(',')
                        .Select(x => x.Split('='))
                        .ToDictionary(x => x[0], x => x[1]);

                string daRealmUrl = passportURLs["DARealm"];
                string daLoginUrl = passportURLs["DALogin"];
                string daRegUrl = passportURLs["DAReg"];
                string propertiesUrl = passportURLs["Properties"];
                string privacyUrl = passportURLs["Privacy"];
                string generalRedirUrl = passportURLs["GeneralRedir"];
                string HelpUrl = passportURLs["Help"];
                string configVersion = passportURLs["ConfigVersion"];

                string authHeader = String.Format("Passport1.4 OrgVerb=GET,OrgURL={0},sign-in={1},pwd={2},{3}", Uri.EscapeDataString("http://messenger.msn.com"), loginName, password, authTicket);

                HttpWebRequest tokenRequest = (HttpWebRequest)HttpWebRequest.Create("https://" + daLoginUrl);
                //_tokenRequest.AllowAutoRedirect = false;
                tokenRequest.Headers.Add("Authorization", authHeader);

                HttpWebResponse tokenResponse = (HttpWebResponse)await tokenRequest.GetResponseAsync();

                string authToken = null;

                //a little hacky, takes 'the target' from below string
                //something=blue,somethingelse='the target',somethingelse2=orange
                using (tokenResponse)
                    authToken = tokenResponse.Headers["Authentication-Info"].Split('\'')[1];

                return authToken;


            }
            catch (WebException ex)
            {
                throw new AuthenticationErrorException(ex);
            }


        }

    }

    public class AuthenticationErrorException : Exception
    {
        internal AuthenticationErrorException(WebException innerException)
            : base("Authentication failed: " + innerException.Message, innerException)
        {
        }
    }

}














