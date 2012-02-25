using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Proxy
{
    public interface IProxy
    {

        void LaunchProxy();
        
        Protocol GetProtocol();

        IPEndPoint GetSource();

        IPEndPoint GetTarget();

    }
}
