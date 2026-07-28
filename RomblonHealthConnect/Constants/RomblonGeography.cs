namespace RomblonHealthConnect.Constants;

/// <summary>
/// Fixed geography of the province. Used to populate pickers and to sanity-check
/// coordinates entered when registering a facility.
/// </summary>
public static class RomblonGeography
{
    /// <summary>The 17 municipalities of Romblon, alphabetical.</summary>
    public static readonly IReadOnlyList<string> Municipalities =
    [
        "Alcantara",
        "Banton",
        "Cajidiocan",
        "Calatrava",
        "Concepcion",
        "Corcuera",
        "Ferrol",
        "Looc",
        "Magdiwang",
        "Odiongan",
        "Romblon",
        "San Agustin",
        "San Andres",
        "San Fernando",
        "San Jose",
        "Santa Fe",
        "Santa Maria"
    ];

    /// <summary>
    /// Approximate municipality centres, used to position the map picker when a
    /// municipality is chosen. Values are OpenStreetMap centroids.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (double Latitude, double Longitude)> MunicipalityCentres =
        new Dictionary<string, (double, double)>
        {
            ["Alcantara"] = (12.2582, 122.0543),
            ["Banton"] = (12.9331, 122.0900),
            ["Cajidiocan"] = (12.3687, 122.6865),
            ["Calatrava"] = (12.6383, 122.2422),
            ["Concepcion"] = (13.0672, 121.8700),
            ["Corcuera"] = (12.7841, 122.0475),
            ["Ferrol"] = (12.3167, 122.0333),
            ["Looc"] = (12.2602, 121.9840),
            ["Magdiwang"] = (12.4913, 122.5147),
            ["Odiongan"] = (12.3998, 121.9878),
            ["Romblon"] = (12.5771, 122.2711),
            ["San Agustin"] = (12.5669, 122.1336),
            ["San Andres"] = (12.5198, 122.0098),
            ["San Fernando"] = (12.3028, 122.6006),
            ["San Jose"] = (12.0333, 121.9333),
            ["Santa Fe"] = (12.1543, 121.9954),
            ["Santa Maria"] = (12.3500, 122.1667)
        };

    /// <summary>
    /// Bounding box of the province with a small margin. Coordinates outside this
    /// are almost certainly a data-entry mistake.
    /// </summary>
    public const double MinLatitude = 11.85;
    public const double MaxLatitude = 13.20;
    public const double MinLongitude = 121.60;
    public const double MaxLongitude = 122.95;

    public static bool IsWithinProvince(double latitude, double longitude) =>
        latitude >= MinLatitude && latitude <= MaxLatitude
        && longitude >= MinLongitude && longitude <= MaxLongitude;
}
