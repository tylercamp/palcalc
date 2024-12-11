# Pal Calc Translations

Pal Calc supports translations for languages used in the game. Pal and Passive Skill names are taken directly from the game files. Other text is for Pal Calc itself.

Each text in Pal Calc has an entry in the `LocalizationCodes.resx` file. Each entry has a name (called the "Localization Code" or `LC`) and sometimes a list of parameters (for dynamic text). Translations for each `LC` are stored in `.resx` files in the [Localizations](./Localizations/) folder.

## For Translators

### 1. Setup Visual Studio

You will need Visual Studio to edit the files and run the program.

1. Download and install [Visual Studio](https://visualstudio.microsoft.com/vs/community/).
2. During installation, enable the ".NET desktop development" option.

### 2. Open Pal Calc Project

1. [Download the Pal Calc code.](https://github.com/tylercamp/palcalc/archive/refs/heads/main.zip)
2. Extract the ZIP file.
3. Double-click on the "PalCalc.sln" file.
4. Expand the "PalCalc.UI" entry. (Don't double-click on it, click the arrow next to it.)
5. Expand the "Localization" entry.

All changes will be made in this "Localization" folder.

### 3. Running Pal Calc

1. Right-click the "PalCalc.UI" entry
2. Click "Set as Startup Project"
3. Click the green button at the top of the window, which says "PalCalc.UI"

This will create and run the Pal Calc program.

Running with Visual Studio will also open a "Translation Debug" window. The Translation Debug window has a tab for each language with translation errors. It lists all errors, their `LC`, and sample English text.

### 4. Edit Translation Files

1. Find the `.resx` file for your language in the [Localizations](./Localizations/) folder (e.g., `Localizations/de.resx` for German).
2. Double-click the file to open the editor.
3. Add entries for any missing LCs. Note: These cannot be edited while the program is running.
4. Ensure every entry in `LocalizationCodes.resx` has a matching entry in your language’s `.resx` file.
5. Use `Localizations/en.resx` as a reference for the original English text.

### 5. Dynamic Text

Some LCs include "format parameters" to modify the final text. For example:

- `LC_LOC_COORD_PALBOX` in `LocalizationCodes.resx` contains: "Tab | X | Y".
- The English translation is `Palbox, tab {Tab} at ({X},{Y})`.

`{Tab}`, `{X}`, and `{Y}` in the translation will be replaced with appropriate text when Pal Calc is running. If any parameters are missing or incorrect, the English text will be used instead. Errors will appear in the Translation Debug window.

### 6. Tips

- Use the Translation Debug window to find problems with your translations.
- To see how text is used, change `DEBUG_DISABLE_TRANSLATIONS = false` to `true` in `PalCalc.UI/Localization/Translator.cs`. This will show the LC instead of normal text.
- Add newlines in a `.resx` file by pressing `Ctrl` + `Enter`.

### 7. Uploading Your Translations

(If you know how to use git, open a normal PR.)

Create a new [Issue](https://github.com/tylercamp/palcalc/issues) on GitHub and upload your `.resx` file. Please include a list of changes.

If you want credit, include your name for Pal Calc's "About" window.

## For Developers

The code generation for `LocalizationCodes.resx` is custom (creates an `enum` instead of a class with static getter methods) and uses the [ResXResourceManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) extension. **If you're not planning to add new text, you don't need this extension.** The `LocalizationCodes.Designer.cs` file stored in this repo should be up to date with the latest content.

If you do make changes to `LocalizationCodes.resx`, they are applied when the project is built, or you can manually expand the `LocalizationCodes.resx` file in the solution explorer, right-click the `LocalizationCodes.Designer.tt` sub-entry, and choose "Run Custom Tool". (This can take a few moments on the first run for some reason.)

You can set `DEBUG_DISABLE_TRANSLATIONS = true` in `Translator.cs` to force all localized text to display as a representation of the localization method for that text; i.e., properly localized text will have an obvious debug-like value, and any text that hasn't been localized will appear normal.

### Localization Types

The `Translator` is responsible for loading and resolving localizations, but `ILocalizableText` and `ILocalizedText` are the interfaces for getting some actual text.

`ILocalizableText` represents some abstract text which _can_ be localized. Most of these expose a `Bind` method for instantiating that text (as a `ILocalizedText`) and mainly exists for applying parameters to format strings:

- `StoredLocalizableText` is used for translations in `.resx` files. Its `Bind` accepts a dictionary of named parameters which match the required parameters listed in the `LocalizationCodes.resx` file. Parameters will generally be converted with `ToString`, though `ILocalizedText` parameter values will have their underlying text used instead.
- `DerivedLocalizableText` is used for all other cases where text needs to change if the language is updated. This is mainly for pal names (`PalViewModel.NameLocalizer`), passive skill names (`TraitViewModel.NameLocalizer`), and making lists (`Translator.Join`).
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
