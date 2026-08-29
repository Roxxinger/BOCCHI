using BOCCHI.Common.Config;
using Ocelot.Lifecycle;
using Ocelot.Services.Translation;

namespace BOCCHI;

public class TranslationLoader(ITranslationRepository translations, UIConfig config) : IOnStart, IOnUpdate
{
    private string? activeLanguage;

    public void OnStart()
    {
        EnsureLanguageLoaded(config.Language.TranslationCode());
        ApplyConfiguredLanguage();
    }

    public void Update()
    {
        ApplyConfiguredLanguage();
    }

    private void ApplyConfiguredLanguage()
    {
        string language = config.Language.TranslationCode();
        EnsureLanguageLoaded(language);
        if (language == activeLanguage && translations.CurrentLanguage == language)
        {
            return;
        }

        translations.SetLanguage(language);
        activeLanguage = language;
    }

    private void EnsureLanguageLoaded(string language)
    {
        if (translations.AvailableLanguages.Contains(language))
        {
            return;
        }

        translations.LoadFromDirectory("Translations", language);
    }
}
