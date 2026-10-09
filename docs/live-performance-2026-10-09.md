# Live pipeline measurement — 2026-10-09

## Connected-camera observations

QHY600M through NINA 3.2.0.9001's native driver, on an Intel Core i3-N305 Windows PC. Readout mode was **2CMS-0**, USB Traffic **0**, exposure **250 ms**, and Bahtinov overlay off. The user reported approximately 5400 ms/frame with a crop-body-sized ROI. The live VM's read-only process snapshot confirmed the following values without changing camera or motor settings:

| ROI | Stream receive | Host conversion | ROI copy | Analysis | Preview/graph | Displayed frame interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 5744 × 4256 | 5345 ms | 0 ms (rounded) | 33 ms | 43 ms | 139 ms | 5426 ms |
| 1024 × 1024 | 1329 ms | 0 ms (rounded) | 1 ms | 3 ms | 10 ms | 1331 ms |

These are snapshots of completed frames, not averages. Receive runs ahead of analysis/display through a one-frame latest-value channel, so stage durations do not sum to the displayed interval. It includes the host live-view download, sensor/SDK waiting and allocation, not just USB transfer.

Managed stack sampling also showed the long receive intervals waiting in `QhySdk.GetQHYCCDLiveFrame`. Sampling includes waiting threads and must not be read as CPU utilization or a precise frame count. The timing snapshots are the primary evidence for the stage breakdown.

The receive stage dominates at both sizes. ROI copy/render optimization cannot by itself remove the approximately five-second camera receive interval.

### Confirmed USB connection limit

Windows PnP identified the camera as `QHY600U3G20-20221128`, below a parent described as **USB2.0 HUB**, port 4. A read-only `IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX_V2` query to that hub returned supported protocols `3` (USB 1.1/2.0) and flags `2`: **SuperSpeed capable, but not operating at SuperSpeed**. This confirms a USB2 connection for the camera, despite the PC having a USB3 host controller. The query opened only the hub for metadata inspection; it did not open the camera SDK, reset a port or change settings.

The user identified the intermediate device as a Gemini PowerBox & Hub Mini V2. Moving the hub's PC cable to another PC port still left the camera on the USB2 branch. A separate read-only query for the parent hub's connection also returned flags `2`, and no SuperSpeed hub branch was enumerated. This identifies the hub-to-PC connection as a segment requiring investigation; it does not establish whether the hub, cable or contact is faulty.

After the camera was connected directly, its port query returned supported protocols `4` (USB3) and flags `3`: **SuperSpeed capable and operating at SuperSpeed**. A subsequent snapshot of the stopped preview retained this last completed frame:

| Delivered ROI | Exposure | Readout | Receive | Convert | Crop | Analyze | Preview/graph |
| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 9556 × 6368 (60.85 MP) | 250 ms | 2CMS-0 / index 8 | 861 ms | 0 ms | 0 ms | 4 ms | 107 ms |

The frame size differs from the USB2 measurements, so this is a recorded full-frame example, not a same-ROI comparison or an average frame rate. It confirms the changed USB link and a much shorter receive time even at the larger size. The proportion of the original delay caused by USB versus sensor/driver readout has not been isolated. A PhotoGraphic DSO 16BIT comparison remains unmeasured. SharpCap was not measured with matching readout mode, bit depth, exposure and ROI, so there is no measured application-to-application FPS comparison.

## Processing change and synthetic benchmark

The normal live path now retains the host `ushort[]` and its ROI origin/stride. Only the central HFR window is expanded to doubles. Display normalization uses the same sampled black/white/midtone estimates and a 65536-entry lookup table. Full ROI resolution and metric pixel scale are retained. The frozen grayscale bitmap is shared with NINA Image.

Bahtinov analysis and diagnostic recording still expand the full ROI when enabled. Other callers retain the existing double-array stream contract unless opting into the raw path. Camera SDK ownership, enumerator drain, cancellation and restoration are unchanged.

The local `--preview-perf` harness used synthetic noise/star pixels, warmed each stage, and reported medians. NINA was running, so these numbers represent processing costs under current PC load, not an isolated laboratory benchmark or measured camera frame rates:

| Size | Double expansion (removed) | Existing double stretch | Raw LUT stretch | Removed allocation/frame |
| --- | ---: | ---: | ---: | ---: |
| 1024 × 1024 | 2.23 ms | 7.17 ms | 2.57 ms | 8 MiB |
| 2048 × 2048 | 7.95 ms | 28.30 ms | 5.85 ms | 32 MiB |
| 4096 × 4096 | 33.05 ms | 100.24 ms | 20.46 ms | 128 MiB |
| 5744 × 4256 | 47.42 ms | 147.53 ms | 33.03 ms | 186.51 MiB |
| 9576 × 6388 (synthetic) | 151.86 ms | 372.29 ms | 59.72 ms | 466.70 MiB |

The raw-preview processing change was subsequently observed on the physical camera at 1024 × 1024, 250 ms and 2CMS-0: receive 1337 ms, convert/crop 0 ms, analyze 4 ms and preview/graph 7 ms while still on the USB2 path. The later direct-USB3 example above uses this path too. Full traces and process snapshots stay local; no camera frames or dumps are committed. Automatic diagnostic recording remains off by default.

## Verification and sources

Checks compare the new lookup output with the existing stretch for all 65536 input values, multiple stretch levels, cropped rectangular frames with nonzero origins/padded stride, and exact raw HFR window coordinates. Integration checks cover stream cleanup, software ROI fallback, ongoing focuser movement, ZWO ASI capability handling, cached raw-frame stretch adjustments and main-viewer output. WPF checks cover the connection indicator and compact layout from 350 × 500 to 950 × 700.

- [NINA QHY driver: StartLiveView / DownloadLiveView](https://github.com/isbeorn/nina/blob/develop/NINA.Equipment/Equipment/MyCamera/QHYCamera.cs): 16-bit live output and SDK frame polling.
- [NINA CameraVM: LiveView / ApplyReadoutModeForSequence](https://github.com/isbeorn/nina/blob/develop/NINA.WPF.Base/ViewModel/Equipment/Camera/CameraVM.cs): snapshot readout selection and live acquisition. These links reference develop source; the measurement used the installed release stated above.
- [QHY readout modes](https://www.qhyccd.com/whats-multiple-readout-modes-and-2cms-mode/): 2CMS uses additional sampling to reduce read noise. This alone does not establish the cause of the observed delay.
- [QHY600 specifications and USB Traffic](https://www.qhyccd.com/astronomical-camera-qhy600/): readout performance and USB Traffic affect frame rate; specifications are not a measurement of this installation.
- [Microsoft CLRMD snapshot guidance](https://github.com/microsoft/clrmd/blob/main/doc/GettingStarted.md): running-process inspection used a read-only snapshot, not live unsuspended heap reads.
- [Microsoft USB port speed query](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ni-usbioctl-ioctl_usb_get_node_connection_information_ex_v2): distinguishes device capability from the currently negotiated SuperSpeed connection.
