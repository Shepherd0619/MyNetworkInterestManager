# MyNetworkInterestManager
A simple team and spatial distance based NetworkInterestManager for Unity Mirror Networking.

# How it work
MyTeam is based on NetworkTeam. Comparing with original, MyTeam added SyncVar to TeamID and also create a public function for helping server change the TeamID.

MyNetworkInterestManager is based on TeamInterestManager. The OnCheckObserver and OnRebuildObserver have been rewritten.

In this case, there are two siuations of TeamID:

1. Player is in the Lobby. In this situation, the TeamID should be "Lobby".
2. Player is in a room/team. In this situation, the TeamID should be userID or RoomID or anything unique to help distinguish.

When players are in the lobby, not only the server will assign them to the same team called "Lobby", but also check their distance via Vector3.Distance to see whether they are in sight.

If they are in sight, player prefabs will be spawned.
If not, player prefabs will be dispawned but not destroyed.

To change the TeamID, you should write a script and assign it to player's prefab.

You should use [Command] to let server modify the TeamID.

# Contact
If you like this little script or have any question, please contact me via

Twitter, CSDN, Github: @shepherd0619

Telegram: t.me/shepherd0619

YouTube: @shepherd_0619

Email: shepherd0619@outlook.com

Discord: https://discord.gg/JdXbRsAUKq
