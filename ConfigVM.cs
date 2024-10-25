using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class ConfigVM
    {
        public string FilePath { get; set; }
        public int HeartbeatTimeout { get; set; }
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public int MaxMissedHeartbeats { get; set; }
        public int RetryCount { get; set; }

    }
}
