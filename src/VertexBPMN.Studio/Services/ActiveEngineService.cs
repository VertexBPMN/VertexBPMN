using System.ComponentModel;
using System.Net;

namespace VertexBPMN.Studio.Services
{
    public sealed class ActiveEngineService(IHttpClientFactory? httpClientFactory = null) : INotifyPropertyChanged, IDisposable
    {
        private string _activeEngineId = "engine1";
        private bool _isConnected = false;
        private DateTime _lastConnectionCheck = DateTime.MinValue;
        private string _connectionStatus = "Not checked";
        private readonly SemaphoreSlim _checkGate = new(1, 1);
        public string ConnectionStatus { get => _connectionStatus; private set => SetProperty(ref _connectionStatus, value); }
        public bool IsChecking { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? OnChange;
        public event Action<string>? OnEngineChanged;

        public string ActiveEngineId
        {
            get => _activeEngineId;
            set
            {
                if (SetProperty(ref _activeEngineId, value))
                {
                    OnEngineChanged?.Invoke(value);
                }
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set => SetProperty(ref _isConnected, value);
        }

        public DateTime LastConnectionCheck
        {
            get => _lastConnectionCheck;
            private set => SetProperty(ref _lastConnectionCheck, value);
        }

        public async Task CheckConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (!await _checkGate.WaitAsync(0, cancellationToken)) return;
            try
            {
                IsChecking = true;
                OnChange?.Invoke();
                if (httpClientFactory is null)
                { IsConnected = false; ConnectionStatus = "Disconnected"; return; }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var client = httpClientFactory.CreateClient("VertexBPMN.Api");
                using var response = await client.GetAsync("/api/ready", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                IsConnected = response.IsSuccessStatusCode;
                ConnectionStatus = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "Unauthorized" : IsConnected ? "Connected" : "Not ready";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { IsConnected = false; ConnectionStatus = "Timed out"; }
            catch (HttpRequestException)
            { IsConnected = false; ConnectionStatus = "Disconnected"; }
            catch (OidcSessionExpiredException)
            { IsConnected = false; ConnectionStatus = "Unauthorized"; }
            finally
            {
                LastConnectionCheck = DateTime.UtcNow;
                IsChecking = false;
                _checkGate.Release();
                OnChange?.Invoke();
            }
        }

        private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            OnChange?.Invoke();
            return true;
        }

        public void Dispose()
        {
            OnChange = null;
            OnEngineChanged = null;
            PropertyChanged = null;
        }
    }
}
