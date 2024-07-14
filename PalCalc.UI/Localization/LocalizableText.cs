using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public class LocalizableText
    {
        private List<WeakReference<LocalizedText>> instances = [];

        // assumes `instances` is already locked
        private void PruneInstances()
        {
            var emptyRefs = instances.Where(i =>
            {
                LocalizedText lt;
                return !i.TryGetTarget(out lt);
            }).ToList();

            instances.RemoveAll(emptyRefs.Contains);
        }

        public LocalizableText(string id)
        {
            Id = id;

            Parameters = id.Split('|').Skip(1).Select(p => p.Trim()).ToList();
        }

        public string Id { get; }
        public List<string> Parameters { get; }

        private TranslationLocale locale;
        public TranslationLocale Locale
        {
            get => locale;
            set
            {
                if (locale != value)
                {
                    lock(instances)
                    {
                        locale = value;

                        PruneInstances();
                        foreach (var i in instances)
                        {
                            if (i.TryGetTarget(out LocalizedText lt))
                                lt.Locale = locale;
                        }
                    }
                }
            }
        }

        // TODO
        public string BaseLocalizedText => Translator.Localizations[Locale][Id];

        public LocalizedText Bind() => Bind([]);

        public LocalizedText Bind(Dictionary<string, object> formatArgs)
        {
            // TODO - validate format args
            var res = new LocalizedText(this, formatArgs) { Locale = Locale };
            lock(instances)
            {
                PruneInstances();
                instances.Add(new WeakReference<LocalizedText>(res));
            }
            return res;
        }
    }
}
