using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FNCInspiraServinte
{
    public class TokenValidator : ITokenValidator
    {
        public bool IsValid(string token)
        {
            return (!string.IsNullOrEmpty(token)) ? TokenBuilder.StaticToken == token : false;
        }
    }
}