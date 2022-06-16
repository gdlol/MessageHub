using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.Localization;

namespace MessageHub.Windows.TrayIcon.Localization;

public class StringLocalizer : IStringLocalizer
{
    private readonly ImmutableDictionary<string, string> mapping;

    public StringLocalizer(IReadOnlyDictionary<string, string> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        this.mapping = mapping.ToImmutableDictionary();
    }

    private string? TryGetString(string name)
    {
        if (mapping.TryGetValue(name, out string? value))
        {
            return value;
        }
        return null;
    }

    public LocalizedString this[string name]
    {
        get
        {
            string? value = TryGetString(name);
            return new LocalizedString(name, value ?? name, value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            string? value = TryGetString(name);
            if (value is null)
            {
                return new LocalizedString(name, string.Format(name, arguments), true);
            }
            else
            {
                return new LocalizedString(name, string.Format(value, arguments), false);
            }
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        foreach (var (name, value) in new Strings())
        {
            string? alternativeValue = TryGetString(name);
            if (alternativeValue is null)
            {
                yield return new LocalizedString(name, value, true);
            }
            else
            {
                yield return new LocalizedString(name, alternativeValue, false);
            }
        }
    }
}
