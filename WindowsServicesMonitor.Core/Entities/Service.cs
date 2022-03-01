using System.ComponentModel;
using System.ServiceProcess;

namespace WindowsServicesMonitor.Core.Entities
{
    public class Service
    {
        public ServiceController Controller { get; set; }
        public Service(ServiceController controller)
        {
            Controller = controller;
        }

        public string Account { get; set; }
    }
}
