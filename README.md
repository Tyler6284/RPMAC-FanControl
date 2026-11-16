# FanControl.RPMac

Fan Control plugin that exposes **Apple SMC fan sensors, temperature sensors, and fan controls** from [golirt1/RPMAC](https://github.com/golirt1/RPMAC)/`smccore` logic so they can be used inside [Rem0o/FanControl](https://github.com/Rem0o/FanControl.Releases).

This lets you use Fan Control's curves, mixing, UI, and profiles while reading/controlling Intel Mac fans through the SMC (Boot Camp).

## Vibe Code Statement
Unfortunately yes, this was made entirely with AI, but you know what? No one was bothering to make it themselves! At least it works! Remember to run as administrator! If anyone can figure out how to make it not necessary to run as admin, be my guest.

Otherwise, don't complain to me, I don't know much about coding.

I did give it access directly to LibreHardwareMonitor and the FanControl.Releases.Wiki so it wouldn't hallucinate too much.

## Tested Hardware
##### MacPro4,1 (Early 2009) running Windows 11 IoT Enterprise LTSC in UEFI mode.
MacPro4,1 firmware was upgraded to 5,1 and modified with EnableGOP.ffs injection for UEFI GOP.
##### Intel(R) Xeon(R) W3520 (8) @ 2.66 GHz
##### Intel(R) Arc(TM) A750 Graphics
##### 32GB DDR3 1066 ECC
##### Samsung SSD 870 EVO 1TB

## What it provides

- **Fan sensors (RPM):** actual speed for each SMC fan (`F<n>Ac`)
- **Temperature sensors:** enumerated SMC temperature keys (`T*`) with readable names when known
- **Control sensors (0–100%):** per-fan control mapped linearly to the fan's SMC min/max RPM
  - `Set(value)` -> forces manual mode and sets target RPM
  - `Reset()` -> returns fan to automatic (`F<n>Md` / `FS!` handling via RPMAC core)
  - Controls are only exposed when `Smc.WritesAllowed` is true (validated Apple SMC)

Sensors/controls use stable IDs like `RPMac/Fan/0/RPM`, `RPMac/Fan/0/Control`, `RPMac/Temp/TC0P`.

## Requirements

- **Intel Mac running Windows (Boot Camp)** with accessible Apple SMC
- **Fan Control** (installed/extracted) and **Administrator** privileges
- `FanControl.Plugins.dll` (from the Fan Control release zip) to compile
- `inpout32.dll` next to the plugin DLL for legacy I/O-port SMC access
- For **T2 Macs (2018–2020)**: `AppleT2Smc.bin` (PawnIO module) next to the plugin DLL and PawnIO driver installed (see RPMAC README/T2 notes)

> On non-Apple hardware or if SMC validation fails, the plugin stays read-only/safe and may expose no controls (matching RPMAC safety behavior).

## Build

No Visual Studio required (uses .NET Framework `csc.exe` shipped with Windows).

1. Ensure `FanControl.Plugins.dll` is present at `RPMAC\plugin\FanControl.Plugins.dll`
   - extracted from [FanControl.zip](https://github.com/Rem0o/FanControl.Releases)
2. From this folder run:
   ```bat
   build.bat
   ```
3. Output: `build\FanControl.RPMac.dll` (+ copied `inpout32.dll`, `AppleT2Smc.bin` if available)

The build compiles `..\src\gui\Smc.cs` (RPMAC SMC core) together with the plugin so no separate RPMAC runtime is needed.

## Install

1. Close Fan Control.
2. Copy **all files** from `FanControl.RPMac\build\` into Fan Control's `Plugins\` folder:
   - `FanControl.RPMac.dll`
   - `inpout32.dll`
   - `AppleT2Smc.bin` (for T2 Macs)
3. Start **Fan Control as Administrator**.
4. Open *Sensors* / *Refresh detection* in Fan Control; RPMac sensors/controls should appear (names prefixed with `RPMac`).

To uninstall, remove those files from `Plugins\`.

## Notes / behavior

- Plugin calls `Smc.Validate()` on initialize and respects `WritesAllowed` (won't write to non-Apple/invalid SMC).
- On `Close`/refresh it resets any controlled fans to **automatic** and cleans up SMC/PawnIO resources.
- Control value is a **percentage of the fan's reported min/max RPM**; actual achievable RPM depends on hardware/SMC.
- SMC access is serialized (global mutex in core) to reduce contention with other SMC tools, but avoid running multiple SMC writers simultaneously.
- This plugin focuses on fan/temp/control integration; RPMAC GUI features (curves/overlay/presets/logging) remain in the RPMAC app.

## License

RPMAC-derived code in this plugin is **GPL-2.0-only** (see `../LICENSE`, `../NOTICE`). Ensure compatibility with Fan Control's plugin distribution terms for your use case.
