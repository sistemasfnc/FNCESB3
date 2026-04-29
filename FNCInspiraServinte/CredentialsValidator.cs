using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;

namespace FNCInspiraServinte
{
    public class CredentialsValidator : ICredentials
    {
        public bool IsValid(Credentials creds)
        {
            return (creds.User == ConfigurationManager.AppSettings["ServiceUser"] && 
                Hash.Get(creds.Password, Hash.HashType.SHA256) == Hash.Get(ConfigurationManager.AppSettings["ServicePassword"], Hash.HashType.SHA256));
        }
    }
}