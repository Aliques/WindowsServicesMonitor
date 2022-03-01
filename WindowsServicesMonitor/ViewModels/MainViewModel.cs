using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WindowsServicesMonitor.Core;
using WindowsServicesMonitor.Core.Interfaces;
using WindowsServicesMonitor.Core.Common.Extentions;
using System.ServiceProcess;
using WindowsServicesMonitor.Core.Entities;

namespace WindowsServicesMonitor.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        #region Privates
        /// <summary>
        /// the window this view model controls
        /// </summary>
        private Window mWindow;

        /// <summary>
        /// the margin drop shadow around window
        /// </summary>
        private int mOuterMarginSize = 0;

        /// <summary>
        /// Corner radius of window
        /// </summary>
        private int mWindowRadius = 3;

        public string searchBoxText;

        /// <summary>
        /// Cancelation token for canceling autoupdate task
        /// </summary>
        private static CancellationTokenSource autoUpdateCancelToken;

        #endregion

        #region Propeties
        public int ResizeBorder { get; set; } = 2;
        public int TitleHeight { get; set; } = 20;
        public Visibility LoadBarVisibility { get; set; } = Visibility.Collapsed;
        /// <summary>
        /// Size of the resize border around the window
        /// </summary>
        public Thickness ResizeBorderThikness { get { return new Thickness(ResizeBorder + OuterMarginSize); } }

        public Visibility CloseMenuButtonVisibility { get; set; }
        public Thickness OuterMarginSizeThikness { get { return new Thickness(OuterMarginSize); } }


        public int OuterMarginSize
        {
            get { return mWindow.WindowState == WindowState.Maximized ? 0 : mOuterMarginSize; }
            set { mOuterMarginSize = value; }
        }

        public int WindowRadius
        {
            get { return mWindow.WindowState == WindowState.Maximized ? 0 : mWindowRadius; }
            set { mWindowRadius = value; }
        }

        public CornerRadius WindowCornerRadius { get { return new CornerRadius(WindowRadius); } }

        public string StartOrStopIconPath { get; set; } = @"Assets/Icons/icons8_play_disabled.ico";

        public ISystemInteractionService<IEnumerable<Service>> Systeminteractionservice { get; }

        public Service SelectedSetvice { get; set; }
        public string SelectedServiceButtonTitle { get; set; } = "Start";
        public string SearchBoxText { get { return searchBoxText; } set
            {
                searchBoxText = value;
                Search(searchBoxText);
            }
        }
        
        public ObservableCollection<Service> Services { get; set; }

        public ObservableCollection<Service> AllServices { get; set; }

        /// <summary>
        /// Source for UI service-list
        /// </summary>
        public ICollectionView ServicesItemCollectionView
        {
            get { return CollectionViewSource.GetDefaultView(Services); }
        }

        public bool AutoUpdate { get; set; } = true;
        public int ServiceListRefreshDelay { get; set; } = 5000;

        #endregion

        public MainViewModel(Window window)
        {
            mWindow = window;
            CloseMenuButtonVisibility = Visibility.Collapsed;
            mWindow.StateChanged += (sender, e) =>
            {
                OnPropertyChanged(nameof(ResizeBorderThikness));
                OnPropertyChanged(nameof(OuterMarginSize));
                OnPropertyChanged(nameof(OuterMarginSizeThikness));
                OnPropertyChanged(nameof(WindowRadius));
                OnPropertyChanged(nameof(WindowCornerRadius));
            };
            Systeminteractionservice = IoC.Get<ISystemInteractionService<IEnumerable<Service>>>();
            
            MinimizeWindowCommand = new RelayCommand(() => mWindow.WindowState = WindowState.Minimized);
            MaximizeWindowCommand = new RelayCommand(() => mWindow.WindowState ^= WindowState.Maximized);
            WindowLoadedCommand = new RelayCommand(()=> WindowLoaded());
            CloseWindowCommand = new RelayCommand(() => { mWindow.Close(); });
            ManualRefreshServicesListCommand = new RelayCommand(async() => await Task.Run(() => ManualUpdateAllServices()));
            ServiceSelectionChangedCommand = new RelayParameterizedCommand((service) => ServiceSelected(service));
            RunOrStopServiceCommand = new RelayCommand(() => ServiceStartOrStoping());
            SearchBoxGetFocusCommand = new RelayCommand(() => SearchTextBoxGetFocus());
            SearchBoxLostFocusCommand = new RelayCommand(async() => await SearchTextBoxLostFocus());
            ClearSearchCommand = new RelayCommand(()=> ClearSearch());
            var resizer = new WindowResizer(mWindow);
        }

        private void WindowLoaded()
        {
            Task.Run(() => InitialServicesList());
        }

        /// <summary>
        /// Не знаю, можно ли в главном потоке выполнять (проблем не возникало)
        /// </summary>
        private void ServiceStartOrStoping()
        {
            if (SelectedSetvice != null)
            {
                try
                {
                    var service = SelectedSetvice.Controller;

                    if (service.Status == ServiceControllerStatus.Running && !service.CanStop)
                    {
                        MessageBox.Show("Can't start this service!", "Warning!", MessageBoxButton.OK);
                        return;
                    }

                    if (service.Status == ServiceControllerStatus.Running && service.CanStop)
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        StartOrStopIconPath = @"Assets/Icons/icons8_play.ico";
                        SelectedServiceButtonTitle = "Start";
                    }

                    else if (service.Status == ServiceControllerStatus.Stopped)
                    {
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        StartOrStopIconPath = @"Assets/Icons/icons8_stop.ico";
                        SelectedServiceButtonTitle = "Stop";
                    }
                }
                catch(InvalidOperationException e)
                { MessageBox.Show(e.ToString(), "Warning!", MessageBoxButton.OK); }
            }
            else
            {
                MessageBox.Show("Select service!", "Warning!", MessageBoxButton.OK);
            }
        }

        private void ServiceSelected(object selectedService)
        {
            if (selectedService == null)
            { return; }

            var selectedArrau = ((IList)selectedService);

            if (selectedArrau.Count < 1)
            {
                return;
            }

            SelectedSetvice = selectedArrau[0] as Service;

            switch (SelectedSetvice.Controller.Status)
            {
                case ServiceControllerStatus.Stopped:
                    StartOrStopIconPath = @"Assets/Icons/icons8_play.ico";
                    SelectedServiceButtonTitle = "Start";
                    break;
                case ServiceControllerStatus.Running:
                    StartOrStopIconPath = @"Assets/Icons/icons8_stop.ico";
                    SelectedServiceButtonTitle = "Stop";
                    break;
                default:
                    break;
            }
        }

        private async Task InitialServicesList()
        {
            LoadBarVisibility = Visibility.Visible;
            autoUpdateCancelToken = new CancellationTokenSource();

            Task getServices = Task.Run(() => 
            {
                AllServices = new ObservableCollection<Service>(Systeminteractionservice.GetSystemServices());
                Services = AllServices;
            });
            
            getServices.Wait();
            LoadBarVisibility = Visibility.Collapsed;

            await Task.Run(() => UpdateAllServicesTask(ServiceListRefreshDelay), autoUpdateCancelToken.Token);
        }

        private async Task UpdateAllServicesTask(int delay)
        {
            try
            {
                if (Services != null)
                {
                    while (AutoUpdate)
                    {
                        autoUpdateCancelToken.Token.ThrowIfCancellationRequested();

                        if (!string.IsNullOrEmpty(SearchBoxText))
                        {
                            Services = new ObservableCollection<Service>(
                                Systeminteractionservice.UpdateAllServices(Services
                                .Where(o => o.Controller.DisplayName
                                .Contains(searchBoxText, StringComparison.OrdinalIgnoreCase))));
                        }
                        else
                        {
                            Services = new ObservableCollection<Service>(Systeminteractionservice
                                .UpdateAllServices(AllServices));
                            
                        }

                        CollectionViewSource.GetDefaultView(Services).Refresh();
                        await Task.Delay(delay);
                    }
                }
            }
            catch(OperationCanceledException) {}
        }

        private void ManualUpdateAllServices()
        {
            if (Services != null)
            {
                Services = new ObservableCollection<Service>(Systeminteractionservice
                    .UpdateAllServices(Services));
                CollectionViewSource.GetDefaultView(Services).Refresh();
            }
        }

        private void Search(string text)
        {
            if(Services == null)
            {
                return;
            }
            ServicesItemCollectionView.Filter = o => 
            { 
                var item = (Service)o;

                return item.Controller.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase);
            };
        }

        private void ClearSearch()
        {
            SearchBoxText = string.Empty;
            Services = AllServices;
        }

        private void SearchTextBoxGetFocus()
        {
            autoUpdateCancelToken.Cancel();
        }

        private async Task SearchTextBoxLostFocus()
        {
            autoUpdateCancelToken = new CancellationTokenSource();

            await Task.Run(() => UpdateAllServicesTask(ServiceListRefreshDelay));
        }

        #region Commands
       
        public ICommand MinimizeWindowCommand { get; set; }
        public ICommand MaximizeWindowCommand { get; set; }
        public ICommand CloseWindowCommand { get; set; }
        public ICommand ManualRefreshServicesListCommand { get; set; }
        public ICommand RunOrStopServiceCommand { get; set; }
        public ICommand ServiceSelectionChangedCommand { get; set; }
        public ICommand ClearSearchCommand { get; set; }
        public ICommand SearchCommand { get; set; }
        public ICommand SearchBoxGetFocusCommand { get; set; }
        public ICommand SearchBoxLostFocusCommand { get; set; }
        public ICommand WindowLoadedCommand { get; set; }
        #endregion
    }


}
