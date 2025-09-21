using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace VertexBPMN.Studio.Services
{
    public sealed class ActiveEngineService : INotifyPropertyChanged, IDisposable
    {
        private string _activeEngineId = "engine1";
        private string _currentUserRole = "Admin";
        private bool _isConnected = false;
        private DateTime _lastConnectionCheck = DateTime.MinValue;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? OnChange;
        public event Action<string>? OnEngineChanged;
        public event Action<string>? OnUserRoleChanged;

        public string ActiveEngineId
        {
            get => _activeEngineId;
            set
            {
                if (SetProperty(ref _activeEngineId, value))
                {
                    OnEngineChanged?.Invoke(value);
                    _ = CheckConnectionAsync(); // Fire and forget
                }
            }
        }

        public string CurrentUserRole
        {
            get => _currentUserRole;
            set
            {
                if (SetProperty(ref _currentUserRole, value))
                {
                    OnUserRoleChanged?.Invoke(value);
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

        private async Task CheckConnectionAsync()
        {
            try
            {
                IsConnected = !string.IsNullOrEmpty(_activeEngineId);
                LastConnectionCheck = DateTime.Now;
            }
            catch
            {
                IsConnected = false;
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
            OnUserRoleChanged = null;
            PropertyChanged = null;
        }
    }
}
