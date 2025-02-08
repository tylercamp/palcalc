using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Xbox
{
    public class XboxSaveMonitor
    {
        public event Action Updated;

        public void Notify() => Updated?.Invoke();
    }

    public class XboxFolderMonitor
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

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            foreach (var monitor in saveMonitorsById.Values)
                monitor.Notify();
        }

        public XboxSaveMonitor GetSaveMonitor(string saveId)
        {
            if (saveMonitorsById.ContainsKey(saveId)) return saveMonitorsById[saveId];

            var res = new XboxSaveMonitor();
            saveMonitorsById.Add(saveId, res);
            return res;
        }
    }
}
