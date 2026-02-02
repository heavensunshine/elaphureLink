using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Toolkit.Mvvm;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;

using elaphureLink.Wpf.Pages;
using elaphureLink.Wpf.Core;
using elaphureLink.Wpf.Messenger;
using elaphureLink.Wpf.Core.Services;

namespace elaphureLink.Wpf.ViewModel
{
    public class HomePageViewModel : ObservableRecipient
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private ISettingsService _SettingsService;

        public HomePageViewModel(ISettingsService settingsService)
        {
            // design time
            ScanDevicesCommand = new AsyncRelayCommand(ScanDevicesAsync);

            _SettingsService = settingsService;

            _deviceAddress = _SettingsService.GetValue<string>(nameof(deviceAddress));
            _driverPath = _SettingsService.GetValue<string>("keilPathInstallation");
            _EnableVendorCommand = _SettingsService.GetValue<bool>(nameof(EnableVendorCommand));

            InstallDriverCommand = new AsyncRelayCommand(InstallDriverAsync);
            StartProxyCommand = new AsyncRelayCommand(StartProxyAsync);
            InstallDriverButtonCommand = new AsyncRelayCommand(ShowInstallPathDialogAsync);
            StopProxyCommand = new AsyncRelayCommand(StopProxyAsync);
            ChangeProxyConfigCommand = new AsyncRelayCommand(ChangeProxyConfigAsync);

            // Message
            WeakReferenceMessenger.Default.Register<ProxyStatusRequestMessage>(this, (r, m) =>
            {
                m.Reply(IsProxyStart);
            });

            WeakReferenceMessenger.Default.Register<ProxyStatusChangedMessage>(this, (r, m) =>
            {
                _SettingsService.SetValue("isProxyRunning", m.Value);
                if (m.Value == false)
                    IsProxyStart = m.Value;
            });
        }

        /// <summary>
        /// Command and Task
        /// </summary>

        public IAsyncRelayCommand InstallDriverButtonCommand { get; }
        public IAsyncRelayCommand InstallDriverCommand { get; }
        public IAsyncRelayCommand StartProxyCommand { get; }
        public IAsyncRelayCommand StopProxyCommand { get; }
        public IAsyncRelayCommand ChangeProxyConfigCommand { get; }

        private async Task ShowInstallPathDialogAsync()
        {
            InstallPathConfirmDialog dialog = new InstallPathConfirmDialog();
            var result = await ContentDialogMaker.CreateContentDialogAsync(dialog, false);

            var locationDialog = new Microsoft.Win32.OpenFileDialog();
            locationDialog.FileName = "TOOLS"; // Default file name
            locationDialog.Filter = "Keil INI file|TOOLS.INI"; // Filter files by extension
            locationDialog.Title = "Select Keil installation directory";

            while (result != ModernWpf.Controls.ContentDialogResult.Primary)
            {
                if (result == ModernWpf.Controls.ContentDialogResult.None)
                {
                    // may be another dialog open...
                    return;
                }

                // change installation path
                if ((bool)locationDialog.ShowDialog())
                {
                    string path = System.IO.Path.GetDirectoryName(locationDialog.FileName);
                    driverPath = path;
                }
                dialog = new InstallPathConfirmDialog();
                result = await ContentDialogMaker.CreateContentDialogAsync(dialog, false);
            }

            // install driver
            await InstallDriverCommand.ExecuteAsync(driverPath);
        }
        private async Task ScanDevicesAsync()
        {
            if (IsScanning)
                return;

            try
            {
                IsScanning = true;
                DiscoveredDevices.Clear();
                SelectedDevice = null;

                // === UDP 广播扫描 ===
                var devices = await UdpDiscovery.ScanDevicesAsync(timeoutMs: 800);

                foreach (var d in devices)
                    DiscoveredDevices.Add(d);

                OnPropertyChanged(nameof(HasDiscoveredDevices));

                // 只发现一个设备时，自动选中
                if (DiscoveredDevices.Count == 1)
                    SelectedDevice = DiscoveredDevices[0];
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Device discovery failed");
            }
            finally
            {
                IsScanning = false;
                OnPropertyChanged(nameof(HasDiscoveredDevices));
            }
        }


        private async Task InstallDriverAsync()
        {
            await elaphureLink.Wpf.Core.Driver.DriverService.InstallDriver(driverPath);
        }

        private async Task StartProxyAsync()
        {
            await elaphureLink.Wpf.Core.elaphureLinkCore.StartProxyAsync(deviceAddress);
        }

        private async Task StopProxyAsync()
        {
            await elaphureLink.Wpf.Core.elaphureLinkCore.StopProxyAsync();
        }

        private async Task ChangeProxyConfigAsync()
        {
            await elaphureLink.Wpf.Core.elaphureLinkCore.ChangeProxyConfigAsync(
                EnableVendorCommand
            );
        }

        /// <summary>
        /// Members
        /// </summary>
        // =======================
        // Device Discovery
        // =======================
        public IAsyncRelayCommand ScanDevicesCommand { get; }

        public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; }
            = new ObservableCollection<DiscoveredDevice>();

        private DiscoveredDevice _selectedDevice;
        public DiscoveredDevice SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                SetProperty(ref _selectedDevice, value);
                if (value != null)
                {
                    deviceAddress = value.Ip;
                }
            }
        }


        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            private set => SetProperty(ref _isScanning, value);
        }

        public bool HasDiscoveredDevices => DiscoveredDevices.Count > 0 && !IsScanning;


        private string _driverPath;
        public string driverPath
        {
            get
            {
                if (string.IsNullOrEmpty(_driverPath))
                {
                    return elaphureLink.Wpf.Core.Driver.DriverService.getKeilInstallPath();
                }

                return _driverPath;
            }
            set
            {
                SetProperty(ref _driverPath, value);
                _SettingsService.SetValue("keilPathInstallation", value);
            }
        }

        public bool isDriverPathValid
        {
            get { return !string.IsNullOrEmpty(driverPath); }
        }

        private string _deviceAddress;

        public string deviceAddress
        {
            get => _deviceAddress;
            set
            {
                SetProperty(ref _deviceAddress, value);

                _SettingsService.SetValue(nameof(deviceAddress), value);
            }
        }

        private bool _isProxyStart = false;
        public bool IsProxyStart
        {
            get => _isProxyStart;
            set
            {
                SetProperty(ref _isProxyStart, value);

                if (value)
                {
                    StartProxyCommand.ExecuteAsync(null);
                }
                else
                {

                    if (_SettingsService.GetValue<bool>("isProxyRunning"))
                    {
                        StopProxyCommand.ExecuteAsync(null);
                    }
                }
            }
        }

        private bool _EnableVendorCommand;
        public bool EnableVendorCommand
        {
            get => _EnableVendorCommand;
            set
            {
                SetProperty(ref _EnableVendorCommand, value);
                _SettingsService.SetValue(nameof(EnableVendorCommand), value);
                ChangeProxyConfigCommand.ExecuteAsync(null);
            }
        }

        //private bool IsChecked_ = false;

        //public bool IsChecked
        //{
        //    get => IsChecked_;
        //    set => SetProperty(ref IsChecked_, value);
        //}
 
        //private bool IsProxyStarting_ = false;
        //private bool _showHomeProgressStr = false;
    }
}
