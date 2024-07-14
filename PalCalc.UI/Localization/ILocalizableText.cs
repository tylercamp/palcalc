using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    // note: LocalizableText weakly tracks any LocalizedText which may need to
    //       be updated when the locale changes. a LocalizedText should be
    //       assigned to a property and _not_ the result of a getter expression,
    //       which would cause that property to be collected early

    public abstract class ILocalizableText
    {
        protected List<WeakReference<ILocalizedText>> instances = [];

        // assumes `instances` is already locked
        protected void PruneInstances()
        {
            var emptyRefs = instances.Where(i =>
            {
                ILocalizedText lt;
                return !i.TryGetTarget(out lt);
            }).ToList();

            if (emptyRefs.Any())
                instances.RemoveAll(emptyRefs.Contains);
        }

        protected void Track(ILocalizedText localized)
        {
            lock(instances)
            {
                PruneInstances();
                instances.Add(new WeakReference<ILocalizedText>(localized));
            }
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
                            //i.Locale = locale;
                            if (i.TryGetTarget(out ILocalizedText lt))
                                lt.Locale = locale;
                        }
                    }
                }
            }
        }
    }
}
