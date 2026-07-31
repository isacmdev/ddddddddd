// <copyright file="StringsAdapter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Resources;

/// <summary>
/// T26 PR #12 — typed accessor for the XAML-visible localized copy that
/// drives the onboarding flow.
/// <para>
/// Backed by the canonical WinUI resource layout under
/// <c>Strings/{es,en-US}/Strings.resw</c> (design.md §10, ADR-007).
/// The .resw files are embedded as manifest resources by
/// <c>ControlParental.App.UI.csproj</c> and read at runtime through
/// <see cref="ResXResourceReader"/>, which understands the .resw XML
/// schema (identical to .resx).
/// </para>
/// <para>
/// Locale chain: the requested culture (when it matches a manifest
/// resource) → <c>es</c> (project default) → <c>en-US</c> (ADR-007
/// fallback) → key name (T25 fallback contract). The lookup is
/// thread-safe via a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// cache keyed on the resolved culture name.
/// </para>
/// <para>
/// Pattern: a single <see cref="Instance"/> is referenced by each
/// page's <c>Strings</c> property so
/// <c>{x:Bind Strings.&lt;Key&gt;, Mode=OneTime}</c> compiles cleanly.
/// WinUI 3 <c>{x:Bind}</c> does not bind directly to static members;
/// using an instance keeps the binding graph static-type checked.
/// </para>
/// </summary>
public sealed class StringsAdapter
{
    /// <summary>
    /// Manifest resource name for the Spanish (default) catalog.
    /// </summary>
    private const string EsManifestResourceName = "ControlParental.App.UI.Strings.es.Strings.resw";

    /// <summary>
    /// Manifest resource name for the English (fallback) catalog.
    /// </summary>
    private const string EnUsManifestResourceName = "ControlParental.App.UI.Strings.en-US.Strings.resw";

    /// <summary>
    /// Project default locale (Rioplatense Spanish — see ADR-007).
    /// </summary>
    private const string DefaultCultureName = "es";

    /// <summary>
    /// Fallback locale when a key is missing in the requested or
    /// default catalog.
    /// </summary>
    private const string FallbackCultureName = "en-US";

    /// <summary>
    /// Cache of resolved culture → string lookup. Populated lazily on
    /// first access to keep cold-start cost low.
    /// </summary>
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new();

    /// <summary>
    /// Gets the singleton adapter shared across all pages.
    /// </summary>
    public static StringsAdapter Instance { get; } = new();

    private StringsAdapter()
    {
    }

    // ----- T25 / Consent -----

    /// <summary>
    /// Gets title for the data disclosure dialog.
    /// </summary>
    public string DisclosureTitle => GetString("DisclosureTitle");

    /// <summary>
    /// Gets body text for the data disclosure dialog.
    /// </summary>
    public string DisclosureBody => GetString("DisclosureBody");

    /// <summary>
    /// Gets title for the transparency detail dialog.
    /// </summary>
    public string TransparencyTitle => GetString("TransparencyTitle");

    /// <summary>
    /// Gets body text explaining what is monitored.
    /// </summary>
    public string TransparencyBody => GetString("TransparencyBody");

    /// <summary>
    /// Gets label for the accept button.
    /// </summary>
    public string AcceptButton => GetString("AcceptButton");

    /// <summary>
    /// Gets label for the view-transparency details button.
    /// </summary>
    public string ViewTransparencyButton => GetString("ViewTransparencyButton");

    /// <summary>
    /// Gets short copy surfaced when the consent grant IPC fails.
    /// </summary>
    public string ConsentRecordFailure => GetString("ConsentRecordFailure");

    /// <summary>
    /// Gets copy surfaced when the persistence IPC returns an unexpected failure.
    /// </summary>
    public string ConsentPersistFailedShort => GetString("ConsentPersistFailedShort");

    // ----- T24 / Pairing -----

    /// <summary>
    /// Gets title for the pairing step.
    /// </summary>
    public string PairYourDeviceTitle => GetString("PairYourDeviceTitle");

    /// <summary>
    /// Gets body description for the pairing step.
    /// </summary>
    public string PairingDescription => GetString("PairingDescription");

    /// <summary>
    /// Gets prompt above the age-band selector.
    /// </summary>
    public string PairingAgeBandPrompt => GetString("PairingAgeBandPrompt");

    /// <summary>
    /// Gets placeholder for the age-band combo box.
    /// </summary>
    public string PairingAgeBandPlaceholder => GetString("PairingAgeBandPlaceholder");

    /// <summary>
    /// Gets display copy for the "7-12" age band.
    /// </summary>
    public string AgeBand712 => GetString("Common.AgeBand_7_12");

    /// <summary>
    /// Gets display copy for the "13-16" age band.
    /// </summary>
    public string AgeBand1316 => GetString("Common.AgeBand_13_16");

    /// <summary>
    /// Gets display copy for the "17-18" age band.
    /// </summary>
    public string AgeBand1718 => GetString("Common.AgeBand_17_18");

    /// <summary>
    /// Gets label for the pairing-code entry.
    /// </summary>
    public string PairingCodePrompt => GetString("PairingCodePrompt");

    /// <summary>
    /// Gets action label for the pairing submit button.
    /// </summary>
    public string PairingAction => GetString("PairingAction");

    /// <summary>
    /// Gets label for the global back button.
    /// </summary>
    public string BackButton => GetString("BackButton");

    // ----- T37 / Account -----

    /// <summary>
    /// Gets title for the account step.
    /// </summary>
    public string AccountTitle => GetString("AccountTitle");

    /// <summary>
    /// Gets description copy for the account step.
    /// </summary>
    public string AccountDescription => GetString("AccountDescription");

    /// <summary>
    /// Gets copy surfaced when account creation requires parental elevation.
    /// </summary>
    public string AccountElevationRequired => GetString("AccountElevationRequired");

    /// <summary>
    /// Gets label for the username field.
    /// </summary>
    public string AccountUsernameLabel => GetString("AccountUsernameLabel");

    /// <summary>
    /// Gets placeholder for the username field.
    /// </summary>
    public string AccountUsernamePlaceholder => GetString("AccountUsernamePlaceholder");

    /// <summary>
    /// Gets label for the password field.
    /// </summary>
    public string AccountPasswordLabel => GetString("AccountPasswordLabel");

    /// <summary>
    /// Gets header for the existing-accounts list.
    /// </summary>
    public string AccountExistingHeader => GetString("AccountExistingHeader");

    /// <summary>
    /// Gets action for selecting an existing account.
    /// </summary>
    public string AccountUseAction => GetString("AccountUseAction");

    /// <summary>
    /// Gets button copy to open the create-account form.
    /// </summary>
    public string AccountCreateNew => GetString("AccountCreateNew");

    /// <summary>
    /// Gets button copy to submit the create-account form.
    /// </summary>
    public string AccountCreate => GetString("AccountCreate");

    /// <summary>
    /// Gets button copy to open the convert-account form.
    /// </summary>
    public string AccountConvertExisting => GetString("AccountConvertExisting");

    /// <summary>
    /// Gets button copy to submit the convert-account form.
    /// </summary>
    public string AccountConvert => GetString("AccountConvert");

    /// <summary>
    /// Gets cancel action for the account forms.
    /// </summary>
    public string AccountCancel => GetString("AccountCancel");

    /// <summary>
    /// Gets description copy for the convert-account form.
    /// </summary>
    public string AccountConvertDescription => GetString("AccountConvertDescription");

    // ----- T26 / Demo -----

    /// <summary>
    /// Gets title for the demo step.
    /// </summary>
    public string DemoTitle => GetString("DemoTitle");

    /// <summary>
    /// Gets description copy for the demo step.
    /// </summary>
    public string DemoDescription => GetString("DemoDescription");

    /// <summary>
    /// Gets units label below the demo countdown.
    /// </summary>
    public string DemoSeconds => GetString("DemoSeconds");

    /// <summary>
    /// Gets status copy while the demo is in flight.
    /// </summary>
    public string DemoInProgress => GetString("DemoInProgress");

    /// <summary>
    /// Gets status copy when the demo completes.
    /// </summary>
    public string DemoCompleted => GetString("DemoCompleted");

    /// <summary>
    /// Gets action label for the run-demo button.
    /// </summary>
    public string DemoRunAction => GetString("DemoRunAction");

    /// <summary>
    /// Gets title overlay text used by the in-app demo fallback.
    /// </summary>
    public string ProtectionActiveOverlay => GetString("ProtectionActiveOverlay");

    // ----- T26 / Managed -----

    /// <summary>
    /// Gets title for the managed step.
    /// </summary>
    public string ManagedTitle => GetString("ManagedTitle");

    /// <summary>
    /// Gets description copy for the managed step.
    /// </summary>
    public string ManagedDescription => GetString("ManagedDescription");

    /// <summary>
    /// Gets header for the current-level card.
    /// </summary>
    public string ManagedCurrentLevelHeader => GetString("ManagedCurrentLevelHeader");

    /// <summary>
    /// Gets title of the activate-managed card.
    /// </summary>
    public string ManagedActivateCardTitle => GetString("ManagedActivateCardTitle");

    /// <summary>
    /// Gets description copy of the activate-managed card.
    /// </summary>
    public string ManagedActivateDescription => GetString("ManagedActivateDescription");

    /// <summary>
    /// Gets action copy for the activate button.
    /// </summary>
    public string ManagedActivateAction => GetString("ManagedActivateAction");

    /// <summary>
    /// Gets title copy shown when MANAGED is unavailable on the running edition.
    /// </summary>
    public string ManagedNotAvailableInEdition => GetString("ManagedNotAvailableInEdition");

    /// <summary>
    /// Gets description copy shown when MANAGED is unavailable on the running edition.
    /// </summary>
    public string ManagedNotAvailableDescription => GetString("ManagedNotAvailableDescription");

    /// <summary>
    /// Gets action copy for the finish button shown on Home editions.
    /// </summary>
    public string ManagedFinishOnboarding => GetString("ManagedFinishOnboarding");

    // ----- T26 / Progress -----

    /// <summary>
    /// Gets progress label shown while the Service has not yet answered
    /// <c>GetEnforcementLevel</c>.
    /// </summary>
    public string ProgressUnknown => GetString("ProgressUnknown");

    /// <summary>
    /// Returns the progress label formatted with the current count and total.
    /// </summary>
    /// <param name="count">Steps currently passing their checks.</param>
    /// <param name="total">Total checks the monitor tracks.</param>
    /// <returns></returns>
    public string ProgressLabelFor(int count, int total)
    {
        var format = GetString("ProgressLabelFormat");
        return string.Format(CultureInfo.CurrentCulture, format, count, total);
    }

    // ----- Lookup core -----

    /// <summary>
    /// Resolves a resource key through the locale chain
    /// (requested → default → fallback → key name).
    /// </summary>
    /// <param name="name">Resource key.</param>
    /// <returns>Localized value or the key name if no catalog defines it.</returns>
    private static string GetString(string name)
    {
        var cultureName = ResolveCultureName();
        var catalog = GetOrLoadCatalog(cultureName);
        if (catalog.TryGetValue(name, out var value))
        {
            return value;
        }

        catalog = GetOrLoadCatalog(FallbackCultureName);
        if (catalog.TryGetValue(name, out value))
        {
            return value;
        }

        return name;
    }

    /// <summary>
    /// Returns the project default culture name when the current UI
    /// culture has no embedded catalog, otherwise the current culture
    /// name (forwards to the requested catalog).
    /// </summary>
    private static string ResolveCultureName()
    {
        var current = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrEmpty(current) || current == DefaultCultureName)
        {
            return DefaultCultureName;
        }

        return ManifestResourceExists(ManifestResourceNameFor(current))
            ? current
            : DefaultCultureName;
    }

    /// <summary>
    /// Returns the manifest resource name for the given culture, or
    /// <c>null</c> if we don't ship a catalog for that culture.
    /// </summary>
    private static string? ManifestResourceNameFor(string cultureName)
    {
        return cultureName switch
        {
            "es" => EsManifestResourceName,
            "en-US" => EnUsManifestResourceName,
            _ => null,
        };
    }

    private static bool ManifestResourceExists(string? resourceName)
    {
        if (resourceName is null)
        {
            return false;
        }

        var assembly = typeof(StringsAdapter).Assembly;
        var resources = assembly.GetManifestResourceNames();
        foreach (var resource in resources)
        {
            if (string.Equals(resource, resourceName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Loads and caches the embedded catalog for the given culture.
    /// </summary>
    private static IReadOnlyDictionary<string, string> GetOrLoadCatalog(string cultureName)
    {
        return Cache.GetOrAdd(cultureName, LoadCatalog);
    }

    private static IReadOnlyDictionary<string, string> LoadCatalog(string cultureName)
    {
        var resourceName = ManifestResourceNameFor(cultureName);
        if (resourceName is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var assembly = typeof(StringsAdapter).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            // Catalog file missing — fall back to an empty dict so the
            // caller can still hit the next link in the chain.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        using var reader = new ResXResourceReader(stream);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in reader)
        {
            if (entry.Key is string key && entry.Value is string text)
            {
                result[key] = text;
            }
        }

        return result;
    }
}
