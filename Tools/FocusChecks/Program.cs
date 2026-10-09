using Cwseo.NINA.LiveFocus.Models;
using NINA.Astrometry;

// NINA initializes its context in OnModelCreating. Give it an empty migration
// directory so this read-only harness never supplies migration scripts.
string cataloguePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINA", "NINA.sqlite");
if (!File.Exists(cataloguePath)) throw new FileNotFoundException("Run NINA once to initialize its catalogue.", cataloguePath);
Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Database", "Migration"));

int passed = 0;
void Check(bool value, string description) {
    if (!value) throw new Exception(description);
    Console.WriteLine("PASS " + description); passed++;
}
void Reject(Action action, string description) {
    bool rejected = false;
    try { action(); } catch (ArgumentException) { rejected = true; }
    Check(rejected, description);
}
var utc = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
var pole = new Coordinates(0, 90, Epoch.J2000, Coordinates.RAType.Degrees);
Reject(() => FocusStarPlanner.Calculate("pole", pole, 1, 91, 0, 0, utc), "Invalid latitude rejected");
Reject(() => FocusStarPlanner.Calculate("pole", pole, 1, 0, double.NaN, 0, utc), "Invalid longitude rejected");
Reject(() => FocusStarPlanner.Calculate("pole", pole, 1, 0, 0, 0, DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)), "Ambiguous local time rejected");
var at37 = FocusStarPlanner.Calculate("pole", pole, 1, 37, 128, 0, utc);
Console.WriteLine($"Pole altitude: {at37.Altitude:F3}°");
Check(Math.Abs(at37.Altitude - 37) < 0.5, "Celestial pole altitude agrees with observer latitude");
var equatorial = new Coordinates(0, 0, Epoch.J2000, Coordinates.RAType.Degrees);
var east = FocusStarPlanner.Calculate("test", equatorial, 1, 0, 0, 0, utc);
var west = FocusStarPlanner.Calculate("test", equatorial, 1, 0, 180, 0, utc);
Check(Math.Abs(east.Altitude + west.Altitude) < 0.5, "Opposite longitudes give complementary equatorial altitudes");
var later = FocusStarPlanner.Calculate("test", equatorial, 1, 0, 0, 0, utc.AddHours(12));
Check(Math.Abs(later.Altitude + east.Altitude) < 1, "UTC changes visibility over half a sidereal rotation");
Check(!FocusStarPlanner.IsAboveHorizon(new() { Altitude = 44 }, 45, 0), "Low star excluded");
Check(!FocusStarPlanner.IsAboveHorizon(new() { Altitude = 60 }, 45, 58), "Custom horizon clearance respected");
Check(FocusStarPlanner.IsAboveHorizon(new() { Altitude = 65 }, 45, 58), "Star above custom horizon clearance accepted");
var candidates = new FocusStarSuggestion[] {
    new() { Name = "negative", Magnitude = -1.44, Altitude = 70 },
    new() { Name = "boundary", Magnitude = 2, Altitude = 45 },
    new() { Name = "faint", Magnitude = 2.1, Altitude = 80 },
    new() { Name = "low", Magnitude = 1, Altitude = 44 },
    new() { Name = "obstructed", Magnitude = 1, Altitude = 60, Azimuth = 90 },
    new() { Name = "unknown", Magnitude = double.NaN, Altitude = 80 },
    new() { Name = "no altitude", Magnitude = 1, Altitude = double.NaN },
    null
};
var visible = FocusStarPlanner.SelectVisibleStars(candidates, 45, 2, az => az == 90 ? 58 : 0);
Check(visible.Select(s => s.Name).SequenceEqual(new[] { "negative", "boundary" }),
    "Magnitude and altitude boundaries include bright negative magnitudes and respect horizon clearance");
Check(FocusStarPlanner.SelectVisibleStars(candidates, 45, 3, az => az == 90 ? 58 : 0).Any(s => s.Name == "faint"),
    "Raising the magnitude limit includes fainter stars");
Check(FocusStarPlanner.SelectVisibleStars(candidates, 45, -1, _ => 0).Select(s => s.Name).SequenceEqual(new[] { "negative" }),
    "Lowering the magnitude limit selects only brighter stars");
Check(FocusStarPlanner.SelectVisibleStars(candidates, 45, double.NaN, _ => 0).Count == 0,
    "Invalid magnitude limits do not admit stars");
var many = Enumerable.Range(0, 35).Select(i => new FocusStarSuggestion { Name = $"star-{i}", Magnitude = i / 10.0, Altitude = 60 }).ToArray();
visible = FocusStarPlanner.SelectVisibleStars(many.Reverse(), 45, 4, _ => 0);
Check(visible.Count == 35 && visible.Select(s => s.Magnitude).SequenceEqual(many.Select(s => s.Magnitude)),
    "All matching stars are retained beyond 20, with brightness ordering at equal altitude");
var stars = await new DatabaseInteraction().GetBrightStars();
Check(stars.Count > 0, "Installed NINA bright-star catalogue is readable");
var suggestions = stars.Select(s => FocusStarPlanner.Calculate(s.Name, s.Coordinates, s.Magnitude, 37.708818, 128.443974, 1000, utc)).ToList();
Check(suggestions.All(s => double.IsFinite(s.Altitude) && double.IsFinite(s.Azimuth)), "Every catalogue star transforms to finite horizontal coordinates");
await TargetCatalogueChecks.Run(Check);
Console.WriteLine($"{passed} checks passed; no telescope or camera commands were sent.");
