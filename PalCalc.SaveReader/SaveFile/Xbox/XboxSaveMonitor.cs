using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Xbox
{
    public class XboxSaveMonitor(string saveId)
    {
        public string SaveId => saveId;

        public event Action Updated;

        public void Notify() => Updated?.Invoke();
    }

    public class XboxFolderMonitor : IDisposable
    {
        private Dictionary<string, XboxSaveMonitor> saveMonitorsById;

        FileSystemWatcher watcher;

        public XboxFolderMonitor(string basePath)
        {
            saveMonitorsById = [];

            watcher = new FileSystemWatcher(basePath);
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Renamed += Watcher_Changed;
            watcher.Deleted += Watcher_Changed;

            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        public event Action Updated;

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            Updated?.Invoke();

            foreach (var monitor in saveMonitorsById.Values)
                monitor.Notify();
        }

        public XboxSaveMonitor GetSaveMonitor(string saveId)
        {
            if (saveMonitorsById.ContainsKey(saveId)) return saveMonitorsById[saveId];

            var res = new XboxSaveMonitor(saveId);
            saveMonitorsById.Add(saveId, res);
            return res;
        }

        public void Dispose() => watcher?.Dispose();
    }
}
