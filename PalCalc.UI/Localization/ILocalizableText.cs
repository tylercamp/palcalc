using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public abstract class ILocalizableText
    {
        protected List<WeakReference<StoredLocalizedText>> instances = [];

        // assumes `instances` is already locked
        protected void PruneInstances()
        {
            var emptyRefs = instances.Where(i =>
            {
                StoredLocalizedText lt;
                return !i.TryGetTarget(out lt);
            }).ToList();

            instances.RemoveAll(emptyRefs.Contains);
        }

        private TranslationLocale locale;
        public TranslationLocale Locale
        {
            get => locale;
            set
            {
                if (locale != value)
                {
                    lock (instances)
                    {
                        locale = value;

                        PruneInstances();
                        foreach (var i in instances)
                        {
                            if (i.TryGetTarget(out StoredLocalizedText lt))
                                lt.Locale = locale;
                        }
                    }
                }
            }
        }
    }
}
