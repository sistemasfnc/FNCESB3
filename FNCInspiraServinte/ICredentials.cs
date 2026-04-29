using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCInspiraServinte
{
    public interface ICredentials
    {
        bool IsValid(Credentials creds);
    }
}
