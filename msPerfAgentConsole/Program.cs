using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Uninett.MsPerfAgent;

namespace Uninett.MsPerfAgent.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            msPerfAgent msPerfAgentInstance = new msPerfAgent();
            msPerfAgentInstance.startConsole();
        }
    }
}
