using System.Collections.Generic;
using Unity.Services.Multiplayer;

namespace Unite.Multiplayer
{
    public static class OpenWorldLobbyRules
    {
        public const string SessionType = "UniteSocial";
        public const string StyleProperty = "LobbyStyle";
        public const string Style = "OpenWorld";
        public const int Capacity = 16;
        public static string Version => SessionCompatibility.Current + ":open-world-v5";

        public static List<FilterOption> BrowserFilters() => new List<FilterOption>
        {
            new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
            new FilterOption(FilterField.IsLocked, "0", FilterOperation.Equal),
            new FilterOption(FilterField.StringIndex1, Version, FilterOperation.Equal),
            new FilterOption(FilterField.StringIndex2, Style, FilterOperation.Equal)
        };

        public static bool IsCompatible(IReadOnlyDictionary<string, SessionProperty> properties) =>
            properties != null && properties.TryGetValue(SessionCompatibility.PropertyName, out var version) &&
            version?.Value == Version && properties.TryGetValue(StyleProperty, out var style) && style?.Value == Style;
    }
}
