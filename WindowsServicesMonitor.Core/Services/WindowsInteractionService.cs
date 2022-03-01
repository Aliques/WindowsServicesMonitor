using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Windows;
using WindowsServicesMonitor.Core.Entities;
using WindowsServicesMonitor.Core.Interfaces;

namespace WindowsServicesMonitor.Core.Services
{
    public class WindowsInteractionService : ISystemInteractionService<IEnumerable<Service>>
    {
        public IEnumerable<Service> GetSystemServices()
        {
            foreach (var service in ServiceController.GetServices())
            {
                yield return new Service(service)
                {
                    Account = GetProcessOwner(service.ServiceName)
                };
            }
        }

        public IEnumerable<Service> UpdateAllServices(IEnumerable<Service> services)
        {
            foreach (var service in services)
            {
                service.Controller.Refresh();
            }
            return services;
        }

        private string GetProcessOwner(string serviceName)
        {
            var getOptions = new ObjectGetOptions(null, TimeSpan.MaxValue, true);

            var svcObj = new ManagementObject( new ManagementPath($"Win32_Service.Name='{serviceName}'"), getOptions);
            var processId = (uint)svcObj["ProcessID"];
            var searcher = new ManagementObjectSearcher(new SelectQuery($"SELECT * FROM Win32_Process WHERE ProcessID = '{processId}'"));
            var processObj = searcher.Get().Cast<ManagementObject>().First();
            var props = processObj.Properties.Cast<PropertyData>().ToDictionary(x => x.Name, x => x.Value);
            string[] outArgs = new string[] { string.Empty, string.Empty };
            var returnVal = (UInt32)processObj.InvokeMethod("GetOwner", outArgs);
            if (returnVal == 0)
            {
                var userName = outArgs[1] + "\\" + outArgs[0];
                return userName;
            }

            return "NO OWNER";
        }
    }
}