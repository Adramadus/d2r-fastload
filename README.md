Adramadus D2R Fast Load v2
===========================
CASC Extraction & Load Optimizer for Diablo II: Resurrected
By Adramadus


HOW TO USE
-----------
1. Double-click "Adramadus_D2R_FastLoad_v2.exe"
2. Click Yes on the UAC (admin) prompt — required for firewall + config edits
3. Click "Scan" — your D2R install is detected automatically
4. Choose your options (Recommended: VSync, FPS Cap, -direct)
5. Click "Extract + Optimize"  (~65 min, D2R must be closed)
6. Launch D2R via Battle.net normally — enjoy faster loads

AFTER A PATCH: Re-run Extract + Optimize (stale files crash the game with -direct -txt)
REVERT: Click "Revert Everything" to restore D2R to its original settings


WHAT IT DOES
-------------
PERFORMANCE  (Settings.json)
  VSync=0              Uncaps FPS on load screens (400-500fps)
  Framerate Cap=0      #1 hidden load-killer — any cap slows loads dramatically
  Reduce Graphics      Texture/Shadow/AA to Low for better in-game FPS

LAUNCH ARGUMENTS  (Battle.net.config)
  -direct -txt         Load from extracted loose files instead of CASC archives
  -ns                  Skip audio init on startup (faster launch on HDD)
  -enablerespec        Free offline character respec (ALT+click stat button)
  -players N           Auto-set /players N at game start

EXTRACTION OPTIONS  (run during Extract + Optimize)
  SD Only              Remove Data\hd\ after extract (saves 37 GB)
  Delete HD Models     Delete .model/.texture files (near-instant waypoint loads)
  Delete Lobby Folder  Remove Data\hd\global\ui\lobby\ (saves 320 MB)
  Audio Degradation    Convert FLAC to MP3 128kbps (saves ~65% audio size)

NETWORK
  Firewall Block;       Block D2R.exe outbound connections (eliminates launch delay)


NOTES
------
v2 is a full native C# rewrite — no PowerShell runtime required.
It should pass antivirus scans without exclusions or workarounds.

By Adramadus
