# PSX XMB Manager
PSX XMB Manager is able to install PS2 homebrew and games on the internal HDD of the PSX DVR (DESR).</br>
Installed homebrew and games will be displayed on the XMB where you can start them like on the PS3.</br>
The latest (v2+) release features also a backup manager for PS2 games on PC & installed games on the PSX HDD.

<img width="591" alt="Untitled" src="https://github.com/SvenGDK/PSX-XMB-Manager/assets/84620/82feae61-3cf9-44f2-b6f3-65d1789f9a80">

## Notes
- Requires FMCB installed on your memory card and Open PS2 Loader (to load games)
  - Guide: https://www.youtube.com/watch?v=9SU594F0pYc
- "XMB Tools" will be available on a later release.
- It is not recommended to abort an installation within the first 3%, this could corrupt your HDD. The same goes for the last percentages of the installation.
- Only connect your PSX's HDD locally if you know how to and never initialize it on Windows !

## Drivers
NBD (REQUIRED)
- Please do not use the very latest NDB driver as it can cause issues with PSX XMB Manager.
  - Use the same build like in the screenshot or 0.5, or lower.
- To connect to the NBD server of your PSX you will first need an NBD client and driver on your PC:
  - The Ceph MSI installer bundles a signed version of the WNBD driver. </br>
  It can be downloaded from here: https://cloudbase.it/ceph-for-windows/
  - Install the client and reboot (required)
  - Guide: https://www.youtube.com/watch?v=FuPfzTY-Tps

Dokany
- Required in case you want to mount & modify game partitions on the PSX HDD.
- https://github.com/dokan-dev/dokany/wiki/Installation

## Creating a new project
- Go to Projects -> New -> You can choose here between a game or homebrew (app) project
- Another window will open and ask for some details, please note:
  - Project name: REQUIRED
  - Save project at: REQUIRED
  - Game/Homebrew title: REQUIRED
  - You can load a game ID directly from the ISO file, just browse the ISO file before
- Save the project, now proceed to "Edit ressources"
  - Option 1: You can load your own cover art and screenshots by clicking on the picture boxes
  - Option 2: You can load all art from https://psxdatacenter.com/ by clicking on "Load from PSXDatacenter" </br>
  This works only if the game ID is in their database.
  - When done, hit "Save resources".

## Preparing a project
- Select your created project from the list and simply hit "Prepare project"
- Note: You can also delete the "EXECUTE.KELF" file inside the "Tools" folder to automatically download the latest OPL-Launcher.

## Installing a project
- Launch Open PS2 Loader and the NBD server
- Connect to your PSX
- Select a prepared project from the list and hit "Install on PSX"
- After confirming with "yes" the installation process will begin
  - Note: If you install a game over NBD, this process can take up to 1-2 hours depending on game size. </br>
  Homebrew should be installed in some minutes.
- Guides:
  - Homebrew: https://www.youtube.com/watch?v=PClmQXc1ytg
  - Games: https://www.youtube.com/watch?v=wmTmhG1yrl0&t=4s


## Used tools from other developers
| Tool | Developer |
|-----|-----|
| hdl_dump | https://github.com/ps2homebrew/hdl-dump |
| pfsshell & pfsfuse | https://github.com/ps2homebrew/pfsshell |
| SCEDoormat_NoME | krHACKen |
