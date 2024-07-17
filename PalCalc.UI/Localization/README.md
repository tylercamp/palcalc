# Pal Calc Translations

Pal Calc supports translations for languages currently offered by the game. Pal and Trait names have been collected directly from the game files. Any remaining translation is for Pal Calc itself.

Each text in Pal Calc has an entry in the `LocalizationCodes.resx` file, where each entry has a name (which is referenced in code and translation files) and an optional list of parameters (for text which is created dynamically.) The name of the entry is called the "Localization Code" (`LC`), and the translations for each `LC` is stored in `.resx` files in the [Localizations](./Localizations) folder.

## For Translators

All editing should be done with [Visual Studio](https://visualstudio.microsoft.com/vs/community/). During installation, make sure you enable the ".NET desktop development" option. Once installed, download the code for Pal Calc and double-click on the "PalCalc.sln" file. Expand the "PalCalc.UI" entry, and expand the "Localization" entry within that. All of your work will be done in here.

Running Pal Calc with Visual Studio will open the app as well as a "Translation Debug" window. It contains a tab for each language with translation errors, listing all of the errors, their code/`LC`, and some sample English text for reference.

Find the `.resx` file in the [Localizations](./Localizations/) folder for your language (such as `Localizations/de.resx` for German) and add an entry for any missing LCs. (Note: These cannot be edited while the program is running.) Every entry in `LocalizationCodes.resx` should have a matching entry in the `.resx` for your language.

Look at the `Localizations/en.resx` file for the original English text and as an example of how to fill out these forms.

### Dynamic Text

Text for most LCs are simple and shown as-is, but some LCs include "format parameters" which are used to modify the final text. For example, the LC `LC_LOC_COORD_PALBOX` in `LocalizationCodes.resx` contains: "Tab | X | Y". Each of these (`Tab`, `X`, `Y`) must appear in the translation in your `.resx` file.

The English translation for `LC_LOC_COORD_PALBOX` is `Palbox, tab {Tab} at ({X},{Y})`. Any text in a translation like `{X}` will be replaced with the appropriate text by Pal Calc. **If the translation for an LC is missing any parameters, or has unexpected parameters, it will not be used and the English translation will be used instead.** These problems will appear in the Translation Debug window when you run the program.

### Tips

- Use the Translation Debug window, which is opened automatically when you run Pal Calc with Visual Studio, to find any problems with your translations.
- If you're not sure how some text is used, you can change the file `PalCalc.UI/Localization/Translator.cs` and change `DEBUG_DISABLE_TRANSLATIONS = false` to `true`. When you run Pal Calc with this setting, the LC for _most_ text will be shown instead of the normal text.
- You can add newlines/line breaks in a `.resx` file by pressing `Ctrl` and `Enter`.

### Uploading Your Translations

(If you're a programmer who knows how to use git, just open a normal PR.)

When you're done with your translation, you can create a new [Issue](TODO) on GitHub and upload your `.resx` file. Please include a list of changes and, if you'd like credit for your work, your name that should be included in Pal Calc's "About" window.

## For Developers

The code generation for `LocalizationCodes.resx` is custom (creates an `enum` instead of a class with static getter methods) and uses the [ResXResourceManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) extension. **If you're not planning to add new text, you don't need this extension.** The `LocalizationCodes.Designer.cs` file stored in this repo should be up to date with the latest content.

If you do make changes to `LocalizationCodes.resx`, they are applied when the project is built, or you can manually expand the `LocalizationCodes.resx` file in the solution explorer, right-click the `LocalizationCodes.Designer.tt` sub-entry, and choose "Run Custom Tool". (This can take a few moments on the first run for some reason.)

You can set `DEBUG_DISABLE_TRANSLATIONS = true` in `Translator.cs` to force all localized text to display as a representation of the localization method for that text; i.e., properly localized text will have an obvious debug-like value, and any text that hasn't been localized will appear normal.

### Localization Types

The `Translator` is responsible for loading and resolving localizations, but `ILocalizableText` and `ILocalizedText` are the interfaces for getting some actual text.

`ILocalizableText` represents some abstract text which _can_ be localized. Most of these expose a `Bind` method for instantiating that text (as a `ILocalizedText`) and mainly exists for applying parameters to format strings:

- `StoredLocalizableText` is used for translations in `.resx` files. Its `Bind` accepts a dictionary of named parameters which match the required parameters listed in the `LocalizationCodes.resx` file. Parameters will generally be converted with `ToString`, though `ILocalizedText` parameter values will have their underlying text used instead.
- `DerivedLocalizableText` is used for all other cases where text needs to change if the language is updated. This is mainly for pal names (`PalViewModel.NameLocalizer`), trait names (`TraitViewModel.NameLocalizer`), and making lists (`Translator.Join`).
- `HardCodedText` (an `ILocalizedText`) is used for text which is constant and never changes regardless of language. This should only be used for e.g. player/guild names, or for debug text.

When using manual translations with `LocalizationCodes`, use the `Bind` extension methods which can be called directly on enums, e.g. `LocalizationCodes.LC_FOO.Bind(...)`. This syntax and its overloads are much easier to use:

```cs
// direct usage (verbose, avoid)
Translator.Translations[LocalizationCodes.LC_RESULT_LIST_TITLE_COUNT].Bind(
    new()
    {
        { "NumResults", items.Count }
    }
);

// simpler (call directly on enum)
LocalizationCodes.LC_RESULT_LIST_TITLE_COUNT.Bind(
    new()
    {
        { "NumResults", items.Count }
    }
);

// even simpler (avoid dictionary syntax)
LocalizationCodes.LC_RESULT_LIST_TITLE_COUNT.Bind(
    new { NumResults = items.Count }
);

// even simplerer (no need for named params if there's only one param)
LocalizationCodes.LC_RESULT_LIST_TITLE_COUNT.Bind(items.Count);
```

### Displaying Translated Text

An `ILocalizedText` instance must be used to get localized text, and any text displayed in the UI must use `ILocalizedText`. It implements `INotifyPropertyChanged` for automatic UI updates when the language changes.

Unformatted text added directly to XAML can be translated by adding `xmlns:itl="clr-namespace:PalCalc.UI.Localization"` to the outer control and using e.g. `Text="{itl:LocalizedText Code=LC_FOO_BAR}"` for content.

Text exposed by view-models **should be exposed as standard getter properties (`{ get; ... }`) and not getter expressions (`X => ...`).** Changes to the current language at runtime are applied to `ILocalizedText` via the `ILocalizableText` that produced them, and this tracking uses `WeakReference` to avoid memory leaks. Binding to an expression property will not prevent the result from being GC'd, so any `ILocalizedText` should be stored directly as a property value to prevent early GC. Text exposed by view-models which are bound via XAML should be used as `{Binding SomeProp.Value}`. Using `{Binding SomeProp}` will incorrectly call `ToString`, which has been set in `ILocalizedText` to throw an exception.

(99% of the app properly responds to language changes at runtime, though `AutoCompleteComboBox` still needs some updates to handle this.)

Text involving format parameters are expected to be fully recreated (i.e. a new `Bind` call) when any of the format args change.