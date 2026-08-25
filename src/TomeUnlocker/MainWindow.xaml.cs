using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using TomeUnlocker.Classes;
using TomeUnlocker.Models;

namespace TomeUnlocker
{
    public partial class MainWindow : Window
    {
        #region Instance

        public static MainWindow Instance { get; private set; }

        public Options Options { get; set; } = new Options();

        private const string EL_GATO = "pack://application:,,,/Resources/cat.png";

        private int _matchCount;
        private int _challengeCount;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            VersionText.Text = $"v{GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";

            SetLoadingOverlayImage(EL_GATO);

            Proxy.OnApiKeyCaptured += OnApiKeyCaptured;
            Proxy.OnTomeActivated += OnTomeActivated;
            Proxy.OnTomeCleared += OnTomeCleared;
            Proxy.OnMatchCompleted += OnMatchCompleted;
            Proxy.OnProxyStarted += OnProxyStarted;
            Proxy.OnProxyStopped += OnProxyStopped;
            Proxy.OnLog += OnProxyLog;

            Loaded += MainWindow_Loaded;
        }

        #endregion

        #region Initialization

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AppendLog("=== TOME UNLOCKER ===");
            AppendLog($"Started at: {DateTime.Now}");
            AppendLog("=====================");

            AppendLog("MainWindow_Loaded: starting initialization sequence");
            await Task.Delay(500);
            AppendLog("MainWindow_Loaded: showing loading overlay");
            await ShowLoadingOverlay("Starting Tome Unlocker...");

            try
            {
                AppendLog("MainWindow_Loaded: checking SSL certificate");
                Proxy.CheckCertificate();
                await Task.Delay(300);

                AppendLog("MainWindow_Loaded: starting FiddlerCore proxy");
                await UpdateLoadingStatus("Starting proxy...");
                Proxy.StartProxy();

                AppendLog("MainWindow_Loaded: proxy started, waiting 1s before hiding overlay");
                await Task.Delay(1000);
                AppendLog("MainWindow_Loaded: hiding loading overlay");
                await HideLoadingOverlay();

                AppendLog("MainWindow_Loaded: initialization complete - waiting for DBD traffic");
            }
            catch (Exception ex)
            {
                AppendLog($"MainWindow_Loaded FATAL: {ex.Message}");
                await UpdateLoadingStatus($"Failed: {ex.Message}");
                await Task.Delay(3000);
                await HideLoadingOverlay();
            }
        }

        #endregion

        #region Event Handlers

        private void OnApiKeyCaptured(string apiKey, string platform)
        {
            Dispatcher.Invoke(() =>
            {
                ApiKeyBox.Text = apiKey;
                ApiKeyBox.Opacity = 1;
                PlatformText.Text = platform?.ToUpperInvariant() ?? "UNKNOWN";
            });
        }

        private void OnTomeActivated()
        {
            Dispatcher.Invoke(() =>
            {
                TomeActiveStatus.Text = "Tome Active";
                TomeActiveStatus.Foreground = (Brush)FindResource("GoodBrush");
                TomeDot.Fill = (Brush)FindResource("GoodBrush");
            });
        }

        private void OnTomeCleared()
        {
            Dispatcher.Invoke(() =>
            {
                _challengeCount++;
                ChallengeCountText.Text = _challengeCount.ToString();

                TomeActiveStatus.Text = "Tome Inactive";
                TomeActiveStatus.Foreground = (Brush)FindResource("BadBrush");
                TomeDot.Fill = (Brush)FindResource("BadBrush");
            });
        }

        private void OnMatchCompleted(string matchId)
        {
            Dispatcher.Invoke(() =>
            {
                _matchCount++;
                MatchCountText.Text = _matchCount.ToString();
                MatchIdDisplay.Text = matchId?.Length > 10 ? matchId[..10] + "..." : matchId ?? "--";
            });
        }

        private void OnProxyStarted(int port)
        {
            Dispatcher.Invoke(() =>
            {
                SetProxyStatus(true, port);
            });
        }

        private void OnProxyStopped()
        {
            Dispatcher.Invoke(() => SetProxyStatus(false, 0));
        }

        private void SetProxyStatus(bool running, int port)
        {
            if (running)
            {
                ProxyStatus.Text = $"Proxy: {port}";
                ProxyStatus.Foreground = (Brush)FindResource("GoodBrush");
                ProxyDot.Fill = (Brush)FindResource("GoodBrush");
            }
            else
            {
                ProxyStatus.Text = "Proxy Stopped";
                ProxyStatus.Foreground = (Brush)FindResource("BadBrush");
                ProxyDot.Fill = (Brush)FindResource("BadBrush");
            }
        }

        private void OnProxyLog(string message)
        {
            Dispatcher.Invoke(() => AppendLog(message));
        }

        #endregion

        #region UI

        private void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var line = $"[{timestamp}] {message}";
            LogBox.AppendText((LogBox.Text.Length > 0 ? Environment.NewLine : "") + line);
            LogScrollView.ScrollToBottom();
        }

        public async Task UpdateLoadingStatus(string statusText)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var fadeOut = new DoubleAnimation
                {
                    From = 1, To = 0,
                    Duration = TimeSpan.FromSeconds(0.25),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (s, e) =>
                {
                    LoadingStatusText.Text = statusText;
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0, To = 1,
                        Duration = TimeSpan.FromSeconds(0.35),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    LoadingStatusText.BeginAnimation(OpacityProperty, fadeIn);
                };
                LoadingStatusText.BeginAnimation(OpacityProperty, fadeOut);
            });
        }

        public async Task ShowLoadingOverlay(string text = "Initializing...")
        {
            await Dispatcher.InvokeAsync(() =>
            {
                LoadingStatusText.Text = text;
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingOverlay.Opacity = 0;

                var fadeIn = new DoubleAnimation
                {
                    From = 0, To = 1,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                LoadingOverlay.BeginAnimation(OpacityProperty, fadeIn);
            });
        }

        public async Task HideLoadingOverlay()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var fadeOut = new DoubleAnimation
                {
                    From = 1, To = 0,
                    Duration = TimeSpan.FromSeconds(0.5),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                fadeOut.Completed += (s, e) => LoadingOverlay.Visibility = Visibility.Collapsed;
                LoadingOverlay.BeginAnimation(OpacityProperty, fadeOut);
            });
        }

        public void SetLoadingOverlayImage(string imagePath)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (imagePath.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        LoadingOverlayImage.ImageSource = bitmap;
                        LoadingImageBorder.Opacity = 1;
                    }
                    else if (File.Exists(imagePath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        LoadingOverlayImage.ImageSource = bitmap;
                        LoadingImageBorder.Opacity = 1;
                    }
                });
            }
            catch { }
        }

        #endregion

        #region Window Events

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Proxy.StopProxy();
            this.Close();
        }

        private void CopyApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ApiKeyBox.Text) && ApiKeyBox.Text != "Start the Game")
            {
                Clipboard.SetText(ApiKeyBox.Text);
            }
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(LogBox.Text))
            {
                Clipboard.SetText(LogBox.Text);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Proxy.StopProxy();
            base.OnClosed(e);
            App.ShutdownApplication(0);
        }

        #endregion
    }
}
