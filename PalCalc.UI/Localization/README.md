# Pal Calc Translations

Pal Calc supports translations for languages currently offered by the game. Pal and Trait names have been collected directly from the game files. Any remaining translation is for Pal Calc itself.

Each text in Pal Calc has an entry in the `LocalizationCodes.resx` file, where each entry has a name (which is referenced in code and translation files) and an optional list of parameters (for text which is created dynamically.) The name of the entry is called the "Localization Code" (`LC`), and the translations for each `LC` is stored in `.resx` files in the [Localizations](./Localizations) folder.

### For Translators

All editing should be done with Visual Studio.

Running Pal Calc with Visual Studio will open the app as well as a "Translation Debug" window. It contains a tab for each language with translation errors, listing all of the errors, their code/`LC`, and some sample English text for reference.

Find the `.resx` file in the [Localizations](./Localizations/) folder for your language and add an entry for any missing LCs. (These cannot be edited while the program is running.)

TODO parameters, examples, how to add to public release

### For Developers

The code generation for `LocalizationCodes.resx` is custom and uses the [ResXResourceManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) extension. **If you're not planning to add new text, you don't need this extension.** The `LocalizationCodes.Designer.cs` file stored in this repo should be up to date with the latest content.

If you do make changes, they are applied when the project is built, or you can manually expand the `LocalizationCodes.resx` file in the solution explorer, right-click the `LocalizationCodes.Designer.tt` sub-entry, and choose "Run Custom Tool". (This can take a few moments on the first run for some reason.)

Each LC is represented by an `ILocalizableText` implementation which is responsible for creating instances of `ILocalizedText` which provide displayable text. `ILocalizableText` is responsible for tracking each instance and applying any updates to `Translator.CurrentLocale`. `ILocalizedText` is observable and its `Value` property will raise an event when the language changes.

Any text provided by a view-model should be stored as an `ILocalizedText` property, and the XAML binding must reference its `Value` sub-property. Static text in XAML can import the `Localization` namespace and use e.g. `Text="{itl:LocalizedText Code=LC_FOO_BAR}"`.

`ILocalizableText` tracks each `ILocalizedText` using a `WeakReference`, which can be invalidated if the `ILocalizedText` is exposed from the view-model with a getter-expression (e.g. `ILocalizedText Label => ...`). Any `ILocalizedText` should be properly stored in a property to avoid early accidental GC.

An `ILocalizedText` should generally be made with `LocalizationCodes.LC_SOME_CODE.Bind()`. Parameterized text can be made with `.Bind(value)` for text with a single parameter, and `.Bind(new { Foo = 1, Bar = "baz" })` for text with multiple parameters. Property names must match those defined in `LocalizationCodes.resx`. You're expected to create a new `ILocalizedText` (with another call to `.Bind`) if there's any change in format parameters. Constant text which does not require translation (uncommon, typically for dev purposes) can use `new HardCodedText(..)`.