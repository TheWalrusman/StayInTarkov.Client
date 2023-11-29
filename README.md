
<div align=center style="text-align: center">
<h1 style="text-align: center">StayInTarkov.Client</h1>

An Escape From Tarkov BepInEx module designed to be used with the [SIT.Server-Aki-Mod](SIT.Aki-Server-Mod) with the ultimate goal of "Offline" Coop
</div>

---

<div align=center>

![GitHub all releases](https://img.shields.io/github/downloads/stayintarkov/StayInTarkov.Client/total) ![GitHub release (latest by date)](https://img.shields.io/github/downloads/stayintarkov/StayInTarkov.Client/latest/total)

[English](README.md) **|** [简体中文](README_CN.md) **|** [Deutsch](README_DE.md) **|** [Português-Brasil](README_PO.md) **|** [日本語](README_JA.md) **|** [한국어-Korean](README_KO.md) **|** [Français](README_FR.md)
</div>

---

## State of Stay In Tarkov

* Stay In Tarkov is in active development by the SIT team
* Pull Requests and Contributions will always be accepted (if they work!)

--- 

## About

The Stay in Tarkov project was born due to Battlestate Games' (BSG) reluctance to create the pure PvE version of Escape from Tarkov. 
The project's aim is simple, create a Cooperation PvE experience that retains progression. 
If BSG decide to create the ability to do this on live OR I receive a DCMA request, this project will be shut down immediately.

## Disclaimer

* You must buy the game to use this. You can obtain it here. [https://www.escapefromtarkov.com](https://www.escapefromtarkov.com). 
* This is by no means designed for cheats (this project was made because cheats have destroyed the Live experience)
* This is by no means designed for illegally downloading the game (and has blocks for people that do!)
* This is purely for educational purposes. I am using this to learn Unity and TCP/UDP/Web Socket Networking and I learnt a lot from BattleState Games \o/.
* I am not affiliated with BSG or others (on Reddit or Discord) claiming to be working on a project. Do NOT contact SPTarkov subreddit or Discord about this project.
* This project is not affiliated with SPTarkov (SPT-Aki) but uses its excellent Server.
* This project is not affiliated with any other Escape from Tarkov emulator.
* This project comes "as-is". It either works for you or it doesn't.

## Support

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/N4N2IQ7YJ)
* The Ko-Fi link is buying Paulov a coffee.
* Pull Requests are encouraged. Thanks to all contributors!
* Please do not hand over money expecting help or a solution. 
* This is a hobby, for fun, project. Please don't treat it seriously. 
* Paulov: I know this is a semi-broken attempt but will try to fix as best I can. SIT Contributors: "We can do it!"
* [SIT Discord](https://discord.gg/f4CN4n3nP2) is available. The community have teamed to help each other out and create community servers.


## SPT-AKI Requirement
* Stay in Tarkov requires the [latest AKI Server](https://dev.sp-tarkov.com/SPT-AKI/Server) to facilitate coop connections. You can learn about SPT-Aki [here](https://www.sp-tarkov.com/).
* You should install the SIT SPT-Aki Server Mod according to the instructions in it's [README.MD](https://github.com/stayintarkov/SIT.Aki-Server-Mod/blob/master/README.md)
* __**NOTE:**__ The SIT Client Mod should **NOT** be installed onto the SPT-Aki *Client*- it should be installed onto it's own copy of Tarkov. See the Installation>Client section of this readme for further info.

## [Wiki](https://github.com/stayintarkov/StayInTarkov.Client/blob/master/wiki/Home.md)
**The Wiki is has been constructed by various contributors. All instructions are also kept within the source in the wiki directory.**
  - ### [Setup Manuals](https://github.com/stayintarkov/StayInTarkov.Client/wiki/Guides-English)
  - ### [FAQs](https://github.com/stayintarkov/StayInTarkov.Client/wiki/FAQs-English)


## Installation

### Overview
SIT is comprised of 2 major pieces and a launcher
- The [SIT SPT-Aki Server Mod](https://github.com/stayintarkov/SIT.Aki-Server-Mod)
- The SIT Client Module (this repo!), installed onto an instance of Tarkov
- [SIT Manager](https://github.com/stayintarkov/SIT.Manager) or [SIT Launcher Classic](https://github.com/stayintarkov/SIT.Launcher.Classic)
  - You should use the SIT Manager. The classic launcher is only mentioned to clarify for existing classic launcher users.

It's recommended to create the following directory structure to store your SIT Installation. This structure will be referred to for the following sections.
```
SIT/
├── server/      # SPT-Aki Server Mod
├── game/        # EFT Client
└── launcher/    # SIT Manager or Classic Launcher
```

### Server Install
- Follow the instructions in the [SIT SPT-Aki Server Mod repo](https://github.com/stayintarkov/SIT.Aki-Server-Mod) to install and configure the server into the `SIT/server` folder.
- Exactly *one* person needs to run the server for Coop. This person will need to port forward, or your group will have to connect via Hamachi or some other VPN solution. If you don't know how to do these things, you might see if someone in the SIT discord is willing to help.
  - In vanilla SPT, you're probably used to running your own local server, and then launching your client which connects to that server under the hood. With SIT, one person will run the modded server and everyone else will connect to that server over the internet.

### Launcher Install
- Follow the instructions in the SIT Manager repo. Install into the `SIT/launcher` folder.

### Client Install
- **Everyone** must install the SIT Client Mod. You can install it using SIT Manager, or manually if desired.
- **IF YOU USE SPT ALREADY**: Do **NOT** install the SIT Client mod onto your existing SPT install. The SIT Client Mod is currently not compatible with the SPT-Aki client, so it needs to be installed on it's own copy of Tarkov.

#### SIT Manager Method
- Copy the contents of your live EFT installation into the currently-empty `SIT/game` folder
  - If you installed tarkov to the default location, it will be under `C:\Battlestate Games\EFT`.
- Launch `SIT/launcher/SIT.Manager.exe`
- Point the manager to the target game directory
  - Open `Settings` in the bottom left
  - Set `Install Path` to `X:\<Full_Path_To>\SIT\game`, or use the Change button and select the `SIT/game` folder 
    - Replace `X` and `<Full_Path_To>` with the path to the folder.
  - Close Settings
- Open the `Tools` menu on the left, and select `+ Install SIT`
- Select the desired SIT version (choose the latest if you don't know what you're doing)
- Click `Install`  

<details>
 <summary>Manual install method</summary>
Note that these are the same steps the SIT Manager performs. If you don't have any reason to, you should probably just use the SIT manager- it's so much quicker and easier. (Seriously, we're not hiding anything from you here. These steps are literally just a plain-english description of [the manager code](https://github.com/stayintarkov/SIT.Manager/blob/master/SIT.Manager/Classes/Utils.cs#L613))

- Copy the contents of your live EFT installation into the currently-empty `SIT/game` folder
  - If you installed tarkov to the default location, it will be under `C:\Battlestate Games\EFT`.
- Create the folowing directories in `SIT/game`:
  - `SITLauncher/`
  - `SITLauncher/CoreFiles/`
  - `SITLauncher/Backup/Corefiles/`
- Download the desired version of the client-mod from this repo's [releases page](https://github.com/stayintarkov/StayInTarkov.Client/releases) to `SIT/game/SITLauncher/CoreFiles` (choose the latest if you don't know what you're doing)
  - Create the folder `SIT/game/SITLauncher/CoreFiles/StayInTarkov-Release/`
  - Extract the contents of the release archive into that folder
- Clean up your `SIT/game` directory
  - Delete the following files & directories:
    - `BattleEye/` \*
    - `EscapeFromTarkov_BE.exe` \*
    - `cache/`
    - `ConsistencyInfo`
    - `Uninstall.exe`
    - `Logs/`
  - \* In case of concern, note that this is not a method that can be used to cheat in live tarkov. SPT (and SIT, by extension) don't use the BattleEye executables/files because the SPT-Aki server does not run battleeye.
    Please be careful, and don't delete these files from your live directory. At best you'll brick your install & won't be able to connect to live servers. At worst you'll trigger a BattleEye detection and get your Account/IP/HWID marked for doing.
- Downgrade your copied tarkov if necessary
  - If your live tarkov's version isn't the same as the SIT version you chose in step 3, you need to downgrade.
    - Your live tarkov's version is the 5-part number in the bottom right of the BSG launcher.
  - SIT does not maintain the tools to downgrade tarkov. You can find instructions on downgrading tarkov [here](https://hub.sp-tarkov.com/doc/entry/49-a-comprehensive-step-by-step-guide-to-installing-spt-aki-properly/)
    - Follow steps 7, 8, 9. Use any folder for the "DowngradePatchers" folder, and use the `SIT/game` folder for the "SPTARKOV" folder.
	- If you run into issues here, SIT does not maintain the DowngradePatcher. You can contact the SPT devs about it, but understand that they won't provide support for anything else than the patcher- Do **NOT** ask them for help with other SIT topics, they *will not* help you.
      - That said, if whatever issue you have is legitimate and not just a simple error, the SIT team has *probably* already noticed & reported it. The SIT Manager uses the Downgrade Patcher too.
- Install BepInEx v5.4.22
  - Download [the archive](https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip)
  - Extract the contents to `SIT/game`
    - Your `SIT/game` folder should now contain a `BepInEx` folder, a `doorstop_config.ini` file, a `changelog.txt` file, and a `winhttp.dll` file.
  - Make the `SIT/game/BepInEx/plugins` folder
- Install SIT Client DLLs
  - Assembly-CSharp.dll
    - Make a backup of your original `SIT/game/EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll` to `SIT/game/SITLauncher/Backup/CoreFiles/`
    - Replace the original dll with `SIT/game/SITLauncher/CoreFiles/StayInTarkov-Release/Assembly-CSharp.dll` 
  - Copy `SIT/game/SITLauncher/CoreFiles/StayInTarkov-Release/StayInTarkov.dll` to `SIT/game/BepInEx/plugins/`
- Install Aki Client DLLs
  - Download the latest SPT-AKI release from the [SPT releases page](https://dev.sp-tarkov.com/SPT-AKI/Stable-releases/releases)
  - Extract two files `EscapeFromTarkov_Data/Managed/Aki.Common.dll` and `EscapeFromTarkov_Data/Managed/Aki.Reflection.dll` from the release, into `SIT/game/EscapeFromTarkov_Data/Managed/`
And with any luck, you're done. 
</details>

### Playing
- **Server Host Only**: Have the server-host start up the modded server (port forwarded / tunneled via VPN or Hamachi to the rest of your group)
  - Make sure you configured your IP address(es) per the servermod repo instructions!
- **Everyone**: Start up the SIT Manager, enter the host's IP & port, and click play!
  - Anyone can start a raid lobby, just select the location/time/insurance, click Host Raid, configure the exact number of players & desired settings, and start it up. Everyone else will see the lobby pop up after you start, and will join then. (The game won't start till everyone's loaded, just like regular tarkov)


## FAQ

### Can Coop use BSG's Coop code?
No. BSG server code is hidden from the client for obvious reasons. So BSG's implementation of Coop use the same online servers as PvPvE. We don't see this, so we cannot use this.

### Are Aki BepInEx (Client mods) Modules supported?
The following Aki Modules are supported.
- aki-core
- Aki.Common
- Aki.Reflection
- Do SPT-AKI Client mods work? This is dependant on how well written the patches are. If they directly target GCLASSXXX or PUBLIC/PRIVATE then they will likely fail.

### Why don't you use Aki Module DLLs?
SPT-Aki DLLs are written specifically for their own Deobfuscation technique and Paulov's own technique is not working well with Aki Modules at this moment in time.
So I ported many of SPT-Aki features into this module. The end-goal would be to rely on SPT-Aki and for this to be solely focused on SIT only features.

## How to compile? 
[Compiling Document](COMPILE.md)

## Thanks List
- SPT-Aki team (Credits provided on each code file used and much love to their Dev team for their support)
- SPT-Aki Modding Community
- DrakiaXYZ ([BigBrain](https://github.com/DrakiaXYZ/SPT-BigBrain) & [Waypoints](https://github.com/DrakiaXYZ/SPT-Waypoints) are integrated with this project)
- SIT team and it's original contributors

## License

- DrakiaXYZ projects contain the MIT License
- 99% of the original core and single-player functionality completed by SPT-Aki teams. There are licenses pertaining to them within this source.
- None of my own work is Licensed. This is solely a just for fun project. I don't care what you do with it.
