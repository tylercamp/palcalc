using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public interface ITranslationError
    {
        public TranslationLocale Locale { get; }
        string Message { get; }
    }

    public class MissingTranslationError(TranslationLocale locale, LocalizationCodes code, string referenceText) : ITranslationError
    {
        public TranslationLocale Locale => locale;
        public LocalizationCodes Code => code;
        public string ReferenceText => referenceText;

        public string Message { get; } = $"Missing translation for '{code}' in '{locale}'";
    }

    public class MissingArgumentError(TranslationLocale locale, LocalizationCodes code, List<string> missingArgs, string referenceText) : ITranslationError
    {
        public TranslationLocale Locale => locale;
        public LocalizationCodes Code => code;
        public List<string> MissingArgs => missingArgs;
        public string ReferenceText => referenceText;

        public string Message { get; } = $"Missing argument(s) for '{code}' in '{locale}': {string.Join(", ", missingArgs)}";
    }

    public class UnexpectedArgumentError(TranslationLocale locale, LocalizationCodes code, List<string> extraArgs, string referenceText) : ITranslationError
    {
        public TranslationLocale Locale => locale;
        public LocalizationCodes Code => code;
        public List<string> ExtraArgs => extraArgs;
        public string ReferenceText => referenceText;

        public string Message { get; } = $"Unexpected argument(s) for '{code}' in '{locale}': {string.Join(", ", extraArgs)}";
    }

    public class UnexpectedTranslationError(TranslationLocale locale, string code) : ITranslationError
    {
        public TranslationLocale Locale => locale;
        public string Code => code;

        public string Message { get; } = $"Unexpected translation code '{code}' in '{locale}'";
    }

    public static partial class Translator
    {
        public delegate void TranslationErrorCallback(ITranslationError error);
        public static event TranslationErrorCallback OnTranslationError;
    }
}
