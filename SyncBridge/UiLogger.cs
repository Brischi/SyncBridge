using SyncBridge.ApplicationLayer;
using SyncBridge.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SyncBridge
{
    public class UiLogger : ILogger
    {
        public ObservableCollection<string> Entries { get; } = new();

        public void Info(string message) => Add($"{message}");
        public void Warn(string message) => Add($"{message}");
        public void Error(string message, Exception? ex = null)
            => Add($"{message}{(ex != null ? " -> " + ex.Message : "")}");

        private void Add(string text)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
            Application.Current.Dispatcher.BeginInvoke(() => Entries.Add(line));
        }
    }
}
