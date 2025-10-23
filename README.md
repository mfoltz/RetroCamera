## Table of Contents

- [Sponsors](#sponsors)
- [Features](#features)
- [Configuration](#configuration)
- [Development](#development)
  - [Local build setup](#local-build-setup)
  - [Manual build](#manual-build)
- [Credits](#credits)

## Sponsor this project

[![patreon](https://i.imgur.com/u6aAqeL.png)](https://www.patreon.com/join/4865914)  [![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/zfolmt)

## Sponsors

Jairon O.; Odjit; Jera; Kokuren TCG and Gaming Shop; Rexxn; Eduardo G.; DirtyMike; Imperivm Draconis; Geoffrey D.; SirSaia; Robin C.; Colin F.; Jade K.; Jorge L.; Adrian L.;

## Features

Yes, this has action mode (right bracket default, can rebind). Streamlined ModernCamera to the essentials with a few extra goodies (also a fancy news panel at the main menu :D). Not considered compatible with controllers/gamepads at this time; will explore that in the future. If anything is missing you were fond of open to feedback!
(Note: original contributions to this project are licensed under CC BY-NC 4.0, with other portions derived from third-party code licensed under MIT)

- **Camera Enhancements:**  Generally increased range of camera motion with specific first-person and third-person modes. Includes options for adjustable FOV, over-the-shoulder offsets, pitch/zoom locking, and aiming offsets. Supports forward aiming in action mode with optional crosshair visibility.
- **Additional Features:** Toggle HUD visibility; toggle batform fog visibility (this also hides clouds and their shadows on the ground); Complete journal quests via keybind; configurable action wheel for using commands;
- **Command Wheel**: Add your label and raw command strings to the config file, enable the wheel in the menu options for RetroCamera, and use right alt key (default, can rebind) to use them on the fly!
- **Configuration:** Configuration for keybinds and options done at the in-game menu with rebinding support. Current keybinds: toggle mod functioning, toggle action mode, toggle HUD, and toggle batform fog; complete journal quest;

## Development

### Local build setup

1. Clone this repository and install the proprietary V Rising managed assemblies that the project references. Copy `ProjectM.dll` (and any other required DLLs) into `third_party/` or set the `VRISING_REFERENCE_ARCHIVE` environment variable to a zip that contains them before running the setup script.
2. Run `./scripts/init.sh` to install the .NET SDK (if missing), restore NuGet dependencies, download `VRising.GameData.dll`, and invoke a Release build with the appropriate `dotnet build` command. The script maintains a repo-local `.dotnet` folder so contributors without a global installation can still compile the mod.
3. Rerun `./scripts/init.sh` after supplying the proprietary assemblies. The script will warn and skip the final build until `ProjectM.dll` is present. Once the dependencies are available the build will complete successfully.

### Manual build

- To build manually after dependencies are prepared, run `dotnet build RetroCamera.csproj --configuration Release`.
- Visual Studio users can open `RetroCamera.csproj`, select the **Release** configuration, and build the project directly.

## Credits

- The modding Discord logo and RetroCamera logo were both made by [@Odjit](https://github.com/Odjit), a very talented artist who also authors the Kindred mods! ([Kindred](https://thunderstore.io/c/v-rising/p/odjit/))
- [ModernCamera](https://github.com/v-rising/ModernCamera) by [@Dimentox](https://github.com/dimentox) serves as the foundation this mod and the versions below were built upon; a fantasic, much-needed addition to the game that tremendously improved the player experience and serves as a valuable open-source reference for client modding.
- [ModernCamera.fix_mouse_look](https://github.com/aequis/ModernCamera/tree/fix_mouse_look) by [@aequis](https://github.com/aequis) was a solid interim between the refactoring arrived at here and the original ModernCamera. 
- [ModernCameraFix](https://github.com/panthernet/ModernCameraFix) by [@panthernet](https://github.com/panthernet) is the most recently updated version of the original ModernCamera, making use of a continued Silkworm.
- [Silkworm](https://github.com/iZastic/vrising-silkworm) by [@iZastic](https://github.com/iZastic) Menu option implementation almost all from Silkworm (most likely incorporating into Bloodstone with keybinds #soon), with rebinding handled by a coroutine of mine.
- [Bloodstone](https://github.com/decaprime/Bloodstone) by [@decaprime](https://github.com/decaprime) Keybind implementation mostly informed by Bloodstone (plan on updating that aspect of Bloodstone back to functioning #soonTM) although I think some Silkworm made it in? Was extremely hard to keep track of at the time which was a large motivation for refactoring.
- [RemoveVignette](https://github.com/iZastic/vrising-removevignette) by [@iZastic](https://github.com/iZastic) Original implementation, slightly modified in RetroCamera to work via menu toggle.
- [IntroSkip](https://github.com/iZastic/vrising-introskip) by [@iZastic](https://github.com/iZastic) Original implementation, slightly modified in RetroCamera to work via menu toggle.
