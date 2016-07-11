using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Uninett.MsPerfAgent.Service
{
    public partial class msPerfAgentService : ServiceBase
    {
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        public msPerfAgentService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            this.StartService(args);
        }

        protected override void OnStop()
        {
            this.StopService();
        }

        public void StartService(string[] args)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;

            Task.Run(() =>
            {
                msPerfAgent msPerfAgentInstance = new msPerfAgent();
                msPerfAgentInstance.startService(cancellationToken);
            }, cancellationToken);
        }

        public void StopService()
        {
            cancellationTokenSource.Cancel();
        }
    }
}
