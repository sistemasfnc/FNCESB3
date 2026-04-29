using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Authentication;

namespace FNCInspiraServinte
{
    public class TokenBuilder : ITokenBuilder
    {
        internal static string StaticToken;

        public string Build(Credentials creds)
        {
            if (new CredentialsValidator().IsValid(creds))
            {
                var time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
                var key = Guid.NewGuid().ToByteArray();
                StaticToken = Convert.ToBase64String(time.Concat(key).ToArray());
                return StaticToken;
            }                
            throw new AuthenticationException();
        }
    }
}