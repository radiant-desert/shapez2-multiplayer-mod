# Shapez 2 Multiplayer Mod

This is a work-in-progress BepInEx mod to add multiplayer functionality to the game Shapez 2.

## Current Status

This mod is currently in active development. It has several functional features, including:
- Client/Server connection
- Synchronized building and demolition
- Synchronized island placement and demolition
- Shared player cursors with names
- Ghost placement previews for buildings, islands, and blueprints

## How to Install / Build

1.  Download R2MODMAN and create a folder for shapez2 and install BepinEx and the SmarterStackSim (this isnt my mod its just this multiplayer mod doesnt work without it?).
2.  Clone this repository or download the source code.
3.  Open the `.csproj` or `.sln` file in your preferred C# editor (like Visual Studio).
4.  Ensure all game-specific DLL references are correctly pointing to your Shapez 2 `Managed` directory.
5.  Build the project.
6.  Copy the resulting `Shapez2MultiplayerMod.dll` into your `BepInEx/plugins` folder.
7.  Make sure to add the LiteNetLib.dll and Newtonsoft.Json.dll to the `BepInEx/plugins` folder aswell.
8.  Not a step but if you see errors in CMD window about SmarterStackSim you can ignore it as it doesnt affect the multiplayermod but please can someone remove the dependency of it.

## To run

1.  You will Need HAMACHI or another vpn service.
2.  Create a server and make sure everyone is connected to the same local IP.
3.  Both the host and the client(s) all need to have a world with the same generation seed. (Dont need to create a new world if you already have one that you want friends to join they just have to make one with the same seed as it and they will be able to join it without losing progress)
4.  All players load into the world before host or joining.
5.  The host will open the Multiplayer menu with "M" and just host a world
6.  The client(s) will then join the server and keep the port as 7777 (might need to join or open a port if it doesnt work but 7777 worked for me) and for the IP copy the IPv4 of the host from the VPN service
7.  For the syncing to start 1 of the clients has to place any object down and the multiplayer syncing will start and work

## Dependencies

-   BepInEx 5.x
-   HarmonyX