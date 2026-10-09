using Cwseo.NINA.LiveFocus.Models;
using NINA.Astrometry;

internal static class TargetCatalogueChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var utc = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        var catalogue = new NinaFocusTargetCatalogue();
        check(NinaFocusTargetCatalogue.NormalizeCatalogueQuery("  ngc 007000 ") == "NGC7000" &&
            NinaFocusTargetCatalogue.NormalizeCatalogueQuery("IC 01805") == "IC1805" &&
            NinaFocusTargetCatalogue.NormalizeCatalogueQuery("Andromeda Galaxy") == "Andromeda Galaxy",
            "Catalogue IDs accept spaces/leading zeroes while common names stay intact");
        var dsos = await catalogue.SearchAsync(FocusTargetKind.DeepSky, "M 31", default);
        var m31 = dsos.Single(t => t.Aliases.Any(a => a.Replace(" ", "") == "M31"));
        check(m31.Kind == FocusTargetKind.DeepSky && !string.IsNullOrWhiteSpace(m31.Id) &&
            m31.Coordinates.RADegrees is > 10 and < 11 && m31.Coordinates.Dec is > 41 and < 42,
            "Installed NINA Sky Atlas resolves M31 aliases to its J2000 position");
        var byName = await catalogue.SearchAsync(FocusTargetKind.DeepSky, "Andromeda", default);
        check(byName.Any(t => t.Id == m31.Id), "Deep-sky common-name search resolves the same target identity");
        var faint = await catalogue.SearchAsync(FocusTargetKind.DeepSky, "M57", default);
        check(faint.Any(t => t.Magnitude > 4), "Deep-sky catalogue queries do not apply the bright-star magnitude limit");
        var all = await catalogue.SearchAsync(FocusTargetKind.DeepSky, "", default);
        check(all.Count is > 0 and <= NinaFocusTargetCatalogue.DeepSkyLimit, "An empty DSO search offers a bounded catalogue list");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        bool cancelledCorrectly = false;
        try { await catalogue.SearchAsync(FocusTargetKind.DeepSky, "M31", cancelled.Token); }
        catch (OperationCanceledException) { cancelledCorrectly = true; }
        check(cancelledCorrectly, "Cancelled catalogue searches do not return stale results");

        var bodies = await catalogue.SearchAsync(FocusTargetKind.SolarSystem, "", default);
        check(bodies.Count == 8 && bodies.Select(t => t.Id).Distinct().Count() == 8 &&
            bodies.All(t => t.SolarBody != NOVAS.Body.Earth && t.SolarBody != NOVAS.Body.Sun),
            "Solar picker contains the Moon and seven observable planets");
        check(bodies.Single(t => FocusTargetPlanner.Matches(t, "달")).SolarBody == NOVAS.Body.Moon &&
            bodies.Single(t => FocusTargetPlanner.Matches(t, "jUpItEr")).SolarBody == NOVAS.Body.Jupiter &&
            bodies.Single(t => FocusTargetPlanner.Matches(t, "목성")).SolarBody == NOVAS.Body.Jupiter,
            "Solar-system search accepts Korean and English names case-insensitively");
        foreach (var body in bodies)
        {
            var target = FocusTargetPlanner.At(body, 37, 128, 100, utc);
            check(target.Coordinates.Epoch == Epoch.JNOW && target.Coordinates.RA is >= 0 and < 24 &&
                target.Coordinates.Dec is >= -90 and <= 90 && double.IsFinite(target.Altitude) &&
                double.IsFinite(target.Azimuth) && !target.Display.Contains("NaN") && !target.Display.Contains("mag"),
                body.Name + " has finite current topocentric coordinates and no fabricated magnitude");
        }
        var moon = bodies.Single(t => t.SolarBody == NOVAS.Body.Moon);
        var here = FocusTargetPlanner.At(moon, 37, 128, 100, utc);
        var opposite = FocusTargetPlanner.At(moon, -37, -52, 100, utc);
        double parallax = Math.Abs((here.Coordinates - opposite.Coordinates).Distance.ArcSeconds);
        check(parallax > 1000 && parallax < 10000, "Lunar coordinates include observer-dependent parallax");
        var later = FocusTargetPlanner.At(moon, 37, 128, 100, utc.AddHours(1));
        check(Math.Abs((here.Coordinates - later.Coordinates).Distance.ArcSeconds) > 500,
            "Moon coordinates move with UTC instead of using a fixed catalogue position");
        var jupiter = bodies.Single(t => t.SolarBody == NOVAS.Body.Jupiter);
        check(Math.Abs((FocusTargetPlanner.At(jupiter, 37, 128, 100, utc).Coordinates -
            FocusTargetPlanner.At(jupiter, 37, 128, 100, utc.AddDays(1)).Coordinates).Distance.ArcSeconds) > 10,
            "Planet coordinates refresh as their ephemeris changes");
        check(FocusTargetPlanner.IsAboveHorizon(new() { Kind = FocusTargetKind.DeepSky, Altitude = 20 }, 45, 0) &&
            !FocusTargetPlanner.IsAboveHorizon(new() { Kind = FocusTargetKind.Stars, Altitude = 20 }, 45, 0) &&
            !FocusTargetPlanner.IsAboveHorizon(new() { Kind = FocusTargetKind.SolarSystem, Altitude = 9 }, 45, 5),
            "Star altitude limits stay category-specific; every category respects horizon clearance");
        bool invalidTime = false, invalidSite = false, invalidBody = false;
        try { FocusTargetPlanner.At(moon, 37, 128, 100, DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)); } catch (ArgumentException) { invalidTime = true; }
        try { FocusTargetPlanner.At(moon, 91, 128, 100, utc); } catch (ArgumentException) { invalidSite = true; }
        try { FocusTargetPlanner.SolarCoordinates(NOVAS.Body.Sun, 37, 128, 100, utc); } catch (ArgumentException) { invalidBody = true; }
        check(invalidTime && invalidSite && invalidBody, "Ambiguous time, invalid site and unsupported bodies fail before movement");
    }
}
