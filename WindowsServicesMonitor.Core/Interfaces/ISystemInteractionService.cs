namespace WindowsServicesMonitor.Core.Interfaces
{
    public interface ISystemInteractionService<TServiceEntity>
    {
        TServiceEntity GetSystemServices();
        TServiceEntity UpdateAllServices(TServiceEntity services);
    }
}
