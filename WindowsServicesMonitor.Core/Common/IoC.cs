using Ninject;
using System.Collections.Generic;
using System.ServiceProcess;
using WindowsServicesMonitor.Core.Entities;
using WindowsServicesMonitor.Core.Interfaces;
using WindowsServicesMonitor.Core.Services;

namespace WindowsServicesMonitor.Core
{
    public static class IoC
    {
        public static IKernel Kernel { get; private set; } = new StandardKernel();

        public static T Get<T>()
        {
            return Kernel.Get<T>();
        }
        public static void Setup()
        {
            BindViewModels();
        }

        private static void BindViewModels()
        {
            Kernel.Bind<ISystemInteractionService<IEnumerable<Service>>>().To<WindowsInteractionService>();
        }
    }
}
