using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Audio;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace BluetoothAudioPlayback
{
    public class RemoteAudioDeviceModel
    {
        public DeviceInformation DeviceInformation { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public AudioPlaybackConnection AudioPlaybackConnection { get; set; }
    }

    public class MainPageModel
    {
        public ObservableCollection<RemoteAudioDeviceModel> RemoteAudioDevices { get; set; } = new ObservableCollection<RemoteAudioDeviceModel>();
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MainPageModel ViewModel { get; set; } = new MainPageModel();

        DeviceWatcher deviceWatcher;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void MainGrid_Loaded(object sender, RoutedEventArgs e)
        {
            this.deviceWatcher = DeviceInformation.CreateWatcher(AudioPlaybackConnection.GetDeviceSelector());
            this.deviceWatcher.Added += DeviceWatcher_Added;
            this.deviceWatcher.Removed += DeviceWatcher_Removed;
            this.deviceWatcher.Start();
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var thumbnailBitmapImage = new BitmapImage();
                using (var deviceThumbnail = await deviceInfo.GetThumbnailAsync())
                {
                    thumbnailBitmapImage.DecodePixelHeight = 64;
                    await thumbnailBitmapImage.SetSourceAsync(deviceThumbnail);
                }

                var device = new RemoteAudioDeviceModel()
                {
                    DeviceInformation = deviceInfo,
                    Thumbnail = thumbnailBitmapImage
                };
                this.ViewModel.RemoteAudioDevices.Add(device);
            });
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var removedDevice = this.ViewModel.RemoteAudioDevices.FirstOrDefault(device => device.DeviceInformation.Id == deviceInfoUpdate.Id);
                if (removedDevice is null) return;

                var listViewItem = this.DeviceListView.ContainerFromItem(removedDevice);
                var enableToggleSwitch = VisualTreeHelper.GetChild(
                                                VisualTreeHelper.GetChild(
                                                    VisualTreeHelper.GetChild(listViewItem, 0),
                                                0),
                                            2) as ToggleSwitch;
                enableToggleSwitch.IsOn = false;

                removedDevice.AudioPlaybackConnection?.Dispose();
                this.ViewModel.RemoteAudioDevices.Remove(removedDevice);
            });
        }

        private async void AudioPlaybackConnection_StateChanged(AudioPlaybackConnection sender, object args)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var device = this.ViewModel.RemoteAudioDevices.FirstOrDefault(iDevice => iDevice.AudioPlaybackConnection.DeviceId == sender.DeviceId);
                var listViewItem = this.DeviceListView.ContainerFromItem(device);
                var connectToggleSwitch = VisualTreeHelper.GetChild(
                                                VisualTreeHelper.GetChild(
                                                    VisualTreeHelper.GetChild(listViewItem, 0),
                                                0),
                                            3) as ToggleSwitch;

                if (sender.State == AudioPlaybackConnectionState.Opened)
                {
                    connectToggleSwitch.IsOn = true;
                }
                else if (sender.State == AudioPlaybackConnectionState.Closed)
                {
                    connectToggleSwitch.IsOn = false;
                }
            });
        }

        private ToggleSwitch GetEnableToggleSwitch(ToggleSwitch enableToggleSwitch)
        {
            return VisualTreeHelper.GetChild(VisualTreeHelper.GetParent(enableToggleSwitch), 2) as ToggleSwitch;
        }

        private ToggleSwitch GetConnectToggleSwitch(ToggleSwitch enableToggleSwitch)
        {
            return VisualTreeHelper.GetChild(VisualTreeHelper.GetParent(enableToggleSwitch), 3) as ToggleSwitch;
        }

        private async void EnableToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            var connectToggleSwitch = this.GetConnectToggleSwitch(toggleSwitch);
            var selectedDevice = toggleSwitch.DataContext as RemoteAudioDeviceModel;

            if (toggleSwitch.IsOn)
            {
                var createdConnection = AudioPlaybackConnection.TryCreateFromId(selectedDevice.DeviceInformation.Id);
                if (createdConnection is null)
                {
                    toggleSwitch.IsOn = false;
                }
                else
                {
                    selectedDevice.AudioPlaybackConnection = createdConnection;
                    selectedDevice.AudioPlaybackConnection.StateChanged += AudioPlaybackConnection_StateChanged;
                    await selectedDevice.AudioPlaybackConnection.StartAsync();
                }

                connectToggleSwitch.IsEnabled = true;
            }
            else
            {
                selectedDevice.AudioPlaybackConnection?.Dispose();
                selectedDevice.AudioPlaybackConnection = null;

                connectToggleSwitch.IsOn = false;
                connectToggleSwitch.IsEnabled = false;
            }
        }

        private async void ConnectToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            var enableToggleSwitch = this.GetEnableToggleSwitch(toggleSwitch);
            if (!enableToggleSwitch.IsOn) return;

            var selectedDevice = toggleSwitch.DataContext as RemoteAudioDeviceModel;

            if (toggleSwitch.IsOn)
            {
                if (selectedDevice.AudioPlaybackConnection.State == AudioPlaybackConnectionState.Opened)
                {
                    return;
                }

                await selectedDevice.AudioPlaybackConnection.OpenAsync();
            }
            else
            {
                // C# does not support AudioPlaybackConnection.Close() method, but can use Dispose() instead.
                selectedDevice.AudioPlaybackConnection.Dispose();
                selectedDevice.AudioPlaybackConnection = AudioPlaybackConnection.TryCreateFromId(selectedDevice.DeviceInformation.Id);
                selectedDevice.AudioPlaybackConnection.StateChanged += AudioPlaybackConnection_StateChanged;
                await selectedDevice.AudioPlaybackConnection.StartAsync();
            }
        }
    }
}
