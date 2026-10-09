# Live Focus for N.I.N.A.

<img src="Images/live-focus-featured-v1.png" alt="Live Focus: play triangle, focus ring and star" width="256" />

A NINA plugin for moving the focuser while watching a live star image.

![Live Focus layout](docs/live-focus-ui.png)

Example layout rendered with synthetic test data. In a narrow dock, the image fills the width; use **Graphs** to show measurements below it. Wider docks show the preview and plots side by side.

## Features

- Absolute focuser position, adjustable In / Out steps and cancellation.
- Bright-star selection from NINA's catalogue, filtered by altitude and the profile horizon.
- GOTO with NINA's configured plate solver to center the selected star. Guiding is stopped before slewing; restart it when ready to image.
- Live ROI preview, local HFR history and the star intensity profile. On streaming cameras, preview continues during focuser movement.
- Mouse ROI selection: click a star, drag the box to move it, or drag an edge/corner to resize. Its size is shown outside the yellow box.
- An explicit **Auto ROI** button; starting live preview preserves the chosen ROI.
- Exposure slider in 50 ms increments, optional Bahtinov mask overlay and an image-footer stretch slider. Stretch changes only the display, never raw measurements.
- Optional diagnostic FITS/JSON recording and detailed timing logs. Recording is **off by default**; normal errors and warnings are still logged.

Autofocus, curve fitting, spike autofocus, sequencer instructions and lens configuration are outside this plugin's scope. Use NINA's autofocus or the original Manual Focuser plugin for those features. The Bahtinov overlay is a manual focusing aid.

## Requirements and camera support

- Windows x64, NINA **3.2.0.9001 or newer**, and the .NET 8 Windows desktop runtime used by NINA.
- Connected focuser for motor controls; connected telescope and camera with a configured plate solver for centered GOTO.
- QHY, ToupTek and ZWO ASI native drivers, plus cameras reporting NINA live-view capability, use the host's streaming interface. ASCOM drivers that lack live view use successive single exposures.
- QHY600M's **3x3 bin mode** falls back to single exposures; other readout modes are allowed. Camera-specific ROI alignment is applied automatically.

Streaming uses NINA's existing camera connection, with no extra SDK handles. Stop waits for the camera reader and any motor movement before releasing the capture reservation. Some native SDK downloads cannot be interrupted immediately. Exact frame rate and available ROI sizes depend on the driver and camera.

## Build and install

```powershell
dotnet build LiveFocus.csproj -c Release
```

Output: `bin\Release\net8.0-windows\Cwseo.NINA.LiveFocus.dll`.

To deploy the build, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\InstallPlugin.ps1
```

The script builds Release and verifies the copied DLL hash. With NINA closed, it copies the DLL and license to:

```text
%LOCALAPPDATA%\NINA\Plugins\3.0.0\Live Focus\
```

If NINA is running, it stages the update under `%LOCALAPPDATA%\NINA\PluginStaging\3.0.0\Live Focus\`; restart NINA to apply it. Staging is also refreshed when deploying with NINA closed so an older pending update cannot overwrite the new build. Use `-SkipBuild` to deploy the existing Release output.

NINA supplies its own libraries; do not copy every dependency from the build directory into the plugin folder. Reopen NINA and add the **Live Focus** dockable panel. Its separate DLL, plugin ID and settings namespace allow coexistence with Manual Focuser.

## Using it

1. Connect the camera and focuser. Optionally choose a focus star and use GOTO. Remove a Bahtinov mask for plate solving.
2. Choose **Edit ROI** to edit the full image, then **Done**, or use **Auto ROI** near the target star.
3. Set exposure and press the video icon to start. Use In / Out to adjust focus while viewing the star and local HFR.
4. Adjust Stretch if the preview is too bright. Stop preview before changing exposure or ROI.

Streaming and diagnostics settings are in NINA's plugin options. Diagnostics, when enabled before an operation, are saved under `%LOCALAPPDATA%\NINA\LiveFocus\FocusDiagnostics`. Turning recording off stops further records; existing records remain available.

## Verification

The console harnesses use synthetic frames and fake equipment mediators; they never connect or send commands to physical equipment. Catalogue/centering harnesses require a local NINA installation and its initialized catalogue/native astrometry libraries.

```powershell
dotnet run --project Tools/FocusCenteringChecks -c Release
dotnet run --project Tools/FocusCenteringChecks -c Release -- --live-move
dotnet run --project Tools/FocusCenteringChecks -c Release -- --live-move --asi
dotnet run --project Tools/FocusCenteringChecks -c Release -- --auto-roi
dotnet run --project Tools/FocusCenteringChecks -c Release -- --preview-display
dotnet run --project Tools/FocusCenteringChecks -c Release -- --focuser
dotnet run --project Tools/FocusStreamChecks -c Release
dotnet run --project Tools/RoiInteractionChecks -c Release
dotnet run --project Tools/FocusChecks -c Release
```

The UI harness renders the actual dockable at narrow and wide sizes without opening NINA. These checks verify workflow, cleanup and rendering; physical camera throughput and optical performance still need field testing.

## Origin and license

Derived from [squallseo/Nina.ManualFocuser, spike_analysis](https://github.com/squallseo/Nina.ManualFocuser/tree/spike_analysis) at commit `9e9bbddffd6181389c988c2752a74eeb19e7bd2e`, extracting focuser controls, focus-star GOTO and live preview into an independent plugin. Original copyrights and the MPL-2.0 license are retained. See [LICENSE.txt](LICENSE.txt).

Plugin ID: `ae6b70d2-e99d-4931-8d7c-3e89b6aa27b4`.
