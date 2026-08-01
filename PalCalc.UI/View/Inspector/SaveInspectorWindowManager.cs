using PalCalc.SaveReader;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace PalCalc.UI.View.Inspector
{
    internal static class SaveInspectorWindowManager
    {
        // TODO - Fold modeless-window ownership into the save/session service during the broader refactor.
        private static readonly object windowsLock = new();
        private static readonly Dictionary<SaveIdentity, List<WeakReference<SaveInspectorWindow>>> windows = new();

        public static void Register(ISaveGame save, SaveInspectorWindow window)
        {
            var identity = SaveIdentity.From(save);
            lock (windowsLock)
            {
                if (!windows.TryGetValue(identity, out var instances))
                {
                    instances = [];
                    windows.Add(identity, instances);
                }

                instances.Add(new WeakReference<SaveInspectorWindow>(window));
            }

            window.Closed += (_, _) => Remove(identity, window);
        }

        public static void CloseAll(ISaveGame save)
        {
            List<SaveInspectorWindow> current;
            var identity = SaveIdentity.From(save);
            lock (windowsLock)
            {
                current = windows.GetValueOrDefault(identity, [])
                    .Select(reference => reference.TryGetTarget(out var window) ? window : null)
                    .Where(window => window != null)
                    .ToList();
                windows.Remove(identity);
            }

            foreach (var window in current)
            {
                if (window.Dispatcher.CheckAccess())
                    window.Close();
                else
                    window.Dispatcher.BeginInvoke(window.Close);
            }
        }

        private static void Remove(SaveIdentity identity, SaveInspectorWindow window)
        {
            lock (windowsLock)
            {
                if (!windows.TryGetValue(identity, out var instances)) return;

                instances.RemoveAll(reference => !reference.TryGetTarget(out var target) || target == window);
                if (instances.Count == 0)
                    windows.Remove(identity);
            }
        }
    }
}
