# PSX XMB Manager
A tool that helps to install PS1 & PS2 games and PS2 homebrew on the internal HDD of the PSX DVR (DESR).</br>
The installed game or homebrew will show up on the XMB where you can start it like on the PS3.</br>

<img width="648" alt="psx-xmb-manager-v3" src="https://github.com/SvenGDK/PSX-XMB-Manager/assets/84620/2cd13f50-3bfe-44cf-a00f-2bbf6c6f81f4">

## Features
- Install PS2 homebrew and games on the internal PSX HDD
- Install PS1 games on the internal PSX HDD
- Backup manager for PS2 games on PC & installed games on the PSX HDD (Game Library)
  - A right click on a (local) game will give you the possibility to quickly create a PS2 game project for the PSX.
- Backup manager for PS1 games on PC (Game Library)
  - A right click on a game will give you the possibility to quickly create a PS1 game project for the PSX.
- Mount PS2 games from PSX HDD and modify/update the partition header
  - Change game properties / Update OPL-Launcher
- HDD Partition Manager (Create partition, Remove partition (destructive), Change partition visibility, Mount/Unmount partition)
- PS2 Game Partition Manager (Dump partition header, Change game title, flags, DMA)
- XMB Files Explorer (XMB Tools)
  - Open a _system or xosd folder to load, view and edit its content
  - Text Editor for .xml & .dic files with syntax highlighting
  - Translate .dic & .xml files automatically in most languages
- Utilities
  - HDD Utilities
    - Create & Restore full raw HDD backups
    - Install POPStarter on the connected PSX HDD
  - Converters
    - Convert CUE backups to POPS (VCD) format
    - Convert BIN/CUE backups to ISO format (PS1 & PS2)
  - Extractors
    - Extract STAR files using stargazer
    - Extract PAK files using PAKerUtility
  - Decryptors
    - Decrypt KELF files using kelftool
    - Decrypt REL files
  - Decompressors/Unpackers
    - Decompress a decrypted xosdmain file
    - Unpack a PS2/PSX BIOS file

## Notes
- Requires FMCB installed on your memory card and Open PS2 Loader (to load PS2 games)
  - Installation Guide: [https://www.youtube.com/watch?v=9SU594F0pYc](https://www.youtube.com/watch?v=9SU594F0pYc)
  - Modified Open PS2 Loader: [https://github.com/SvenGDK/Open-PS2-Loader](https://github.com/SvenGDK/Open-PS2-Loader)
  - Do not forget to place/replace OPNPS2LD.ELF in the OPL+ partition
- Requires POPStarter installed for PS1 games
  - Installation Guide: [https://bitbucket.org/ShaolinAssassin/popstarter-documentation-stuff/wiki/quickstart-hdd](https://bitbucket.org/ShaolinAssassin/popstarter-documentation-stuff/wiki/quickstart-hdd)
- It is not recommended to abort an installation within the first 3%, this could corrupt your HDD. The same goes for the last percentages of the installation.
- Only connect your PSX's HDD locally if you know how to and never initialize it on Windows !

## Required Drivers
- [https://github.com/SvenGDK/PSX-XMB-Manager/wiki/Required-Drivers](https://github.com/SvenGDK/PSX-XMB-Manager/wiki/Required-Drivers)

## Create & Install Projects
- [https://github.com/SvenGDK/PSX-XMB-Manager/wiki/Manage-Projects](https://github.com/SvenGDK/PSX-XMB-Manager/wiki/Manage-Projects)

## Used tools from other developers
| Tool | Developer |
|-----|-----|
| hdl_dump | https://github.com/ps2homebrew/hdl-dump |
| PAKerUtility | [El_isra](https://github.com/israpps/PAKerUtility) |
| pfsshell & pfsfuse | https://github.com/ps2homebrew/pfsshell |
| SCEDoormat_NoME | krHACKen |
| stargazer | [Brawl345](https://github.com/Brawl345/stargazer) |
