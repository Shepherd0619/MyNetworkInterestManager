//  MyNetworkInterestManagement
//  作者：朱梓瑞(Shepherd0619)
//  创建日期：2023年7月25日

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mirror;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class MyNetworkInterestManagement : InterestManagement
{
    [Tooltip("The maximum range that objects will be visible at.")]
    public int visRange = 10;

    [Tooltip("Rebuild all every 'rebuildInterval' seconds.")]
    public float rebuildInterval = 1;

    double lastRebuildTime;

    readonly Dictionary<string, HashSet<NetworkIdentity>> teamObjects =
        new Dictionary<string, HashSet<NetworkIdentity>>();

    readonly Dictionary<NetworkIdentity, string> lastObjectTeam = new Dictionary<NetworkIdentity, string>();
    readonly HashSet<string> dirtyTeams = new HashSet<string>();
    public static MyNetworkInterestManagement Instance;

    private void Awake()
    {
        Instance = this;
    }

    [ServerCallback]
    public override void OnSpawned(NetworkIdentity identity)
    {
        Debug.Log("[MyNetworkInterestManagement]OnSpawned started!");
        if (!identity.TryGetComponent(out MyTeam identityMyTeam))
            return;
        Debug.Log("[MyNetworkInterestManagement]OnSpawned successfully getComponent!");
        string networkTeamId = identityMyTeam.teamId;
        lastObjectTeam[identity] = networkTeamId;
        // Null / Empty string is never a valid teamId...do not add to teamObjects collection
        if (string.IsNullOrWhiteSpace(networkTeamId))
            return;

        Debug.Log($"TeamInterestManagement.OnSpawned {identity.name} {networkTeamId}");

        if (!teamObjects.TryGetValue(networkTeamId, out HashSet<NetworkIdentity> objects))
        {
            objects = new HashSet<NetworkIdentity>();
            teamObjects.Add(networkTeamId, objects);
        }

        objects.Add(identity);

        // Team ID could have been set in NetworkBehaviour::OnStartServer on this object.
        // Since that's after OnCheckObserver is called it would be missed, so force Rebuild here.
        // Add the current team to dirtyTeams for Update to rebuild it.
        dirtyTeams.Add(networkTeamId);
    }

    [ServerCallback]
    public override void OnDestroyed(NetworkIdentity identity)
    {
        Debug.Log("[MyNetworkInterestManagement]OnDestroyed started!");
        // Don't RebuildSceneObservers here - that will happen in Update.
        // Multiple objects could be destroyed in same frame and we don't
        // want to rebuild for each one...let Update do it once.
        // We must add the current team to dirtyTeams for Update to rebuild it.
        if (lastObjectTeam.TryGetValue(identity, out string currentTeam))
        {
            lastObjectTeam.Remove(identity);
            if (!string.IsNullOrWhiteSpace(currentTeam) &&
                teamObjects.TryGetValue(currentTeam, out HashSet<NetworkIdentity> objects) && objects.Remove(identity))
                dirtyTeams.Add(currentTeam);
        }
    }

    // internal so we can update from tests
    [ServerCallback]
    internal void Update()
    {
        // rebuild all spawned NetworkIdentity's observers every interval
        if (NetworkTime.time >= lastRebuildTime + rebuildInterval)
        {
            RebuildAll();
            lastRebuildTime = NetworkTime.time;
        }

        // for each spawned:
        //   if team changed:
        //     add previous to dirty
        //     add new to dirty
        foreach (NetworkIdentity netIdentity in NetworkServer.spawned.Values)
        {
            // Ignore objects that don't have a MyTeam component
            if (!netIdentity.TryGetComponent(out MyTeam identityMyTeam))
                continue;

            string networkTeamId = identityMyTeam.teamId;
            if (!lastObjectTeam.TryGetValue(netIdentity, out string currentTeam))
                continue;

            // Null / Empty string is never a valid teamId
            // Nothing to do if teamId hasn't changed
            if (string.IsNullOrWhiteSpace(networkTeamId) || networkTeamId == currentTeam)
                continue;

            // Mark new/old Teams as dirty so they get rebuilt
            UpdateDirtyTeams(networkTeamId, currentTeam);

            // This object is in a new team so observers in the prior team
            // and the new team need to rebuild their respective observers lists.
            UpdateTeamObjects(netIdentity, networkTeamId, currentTeam);
        }

        // rebuild all dirty teams
        foreach (string dirtyTeam in dirtyTeams)
            RebuildTeamObservers(dirtyTeam);

        dirtyTeams.Clear();
    }

    void UpdateDirtyTeams(string newTeam, string currentTeam)
    {
        // Null / Empty string is never a valid teamId
        if (!string.IsNullOrWhiteSpace(currentTeam))
            dirtyTeams.Add(currentTeam);

        dirtyTeams.Add(newTeam);
    }

    void UpdateTeamObjects(NetworkIdentity netIdentity, string newTeam, string currentTeam)
    {
        // Remove this object from the hashset of the team it just left
        // string.Empty is never a valid teamId

        if (!string.IsNullOrWhiteSpace(currentTeam))
            teamObjects[currentTeam].Remove(netIdentity);

        // Set this to the new team this object just entered
        lastObjectTeam[netIdentity] = newTeam;

        // Make sure this new team is in the dictionary
        if (!teamObjects.ContainsKey(newTeam))
            teamObjects.Add(newTeam, new HashSet<NetworkIdentity>());

        // Add this object to the hashset of the new team
        teamObjects[newTeam].Add(netIdentity);
    }

    void RebuildTeamObservers(string teamId)
    {
        foreach (NetworkIdentity netIdentity in teamObjects[teamId])
            if (netIdentity != null)
                NetworkServer.RebuildObservers(netIdentity, false);
    }

    public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
    {
        // Always observed if no MyTeam component
        if (!newObserver.identity.TryGetComponent(out MyTeam newObserverMyTeam))
            return true;
        // Always observed if no MyTeam component
        if (!identity.TryGetComponent(out MyTeam identityMyTeam))
            return true;

        Debug.Log(
            $"TeamInterestManagement.OnCheckObserver {identity.name} {identityMyTeam.teamId} | {newObserver.identity.name} {newObserverMyTeam.teamId}");

        if (identityMyTeam.forceShown)
            return true;

        // string.Empty is never a valid teamId
        if (string.IsNullOrWhiteSpace(identityMyTeam.teamId))
            return false;

        // Null / Empty string is never a valid teamId
        if (string.IsNullOrWhiteSpace(newObserverMyTeam.teamId))
            return false;

        // 我在大厅，对方也在大厅
        if (newObserverMyTeam.teamId == "Lobby" && identityMyTeam.teamId == "Lobby")
        {
            // 判断直线距离
            return Vector3.Distance(identity.transform.position, newObserver.identity.transform.position) <= visRange;
        }
        // 我在大厅，对方在其他地方
        else if (newObserverMyTeam.teamId == "Lobby" && identityMyTeam.teamId != "Lobby")
        {
            return false;
        }
        // 我在其他地方，对方也在大厅
        else if (newObserverMyTeam.teamId != "Lobby" && identityMyTeam.teamId == "Lobby")
        {
            return false;
        }
        // 均在其他地方
        else
        {
            // 判断teamId是否相同
            return newObserverMyTeam.teamId == identityMyTeam.teamId;
        }
    }

    public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
    {
        Vector3 position = identity.transform.position;

        foreach (NetworkConnectionToClient newObserver in newObservers)
        {
            OnCheckObserver(identity, newObserver);
        }


        // If this object doesn't have a MyTeam then it's visible to all clients
        if (!identity.TryGetComponent(out MyTeam networkTeam))
        {
            AddAllConnections(newObservers);
            return;
        }

        // If this object has MyTeam and forceShown == true then it's visible to all clients
        if (networkTeam.forceShown)
        {
            AddAllConnections(newObservers);
            return;
        }

        /*
        // Null / Empty string is never a valid teamId
        if (string.IsNullOrWhiteSpace(networkTeam.teamId))
            return;
        */
        // Abort if this team hasn't been created yet by OnSpawned or UpdateTeamObjects
        if (!teamObjects.TryGetValue(networkTeam.teamId, out HashSet<NetworkIdentity> objects))
        {
            return;
        }

        // Add everything in the hashset for this object's current team
        foreach (NetworkIdentity networkIdentity in objects)
        {
            // 看是不是在Lobby，如果不是，默认认为所有人都可见
            if (networkIdentity.GetComponent<MyTeam>().teamId != "Lobby")
            {
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                {
                    newObservers.Add(networkIdentity.connectionToClient);
                }
            }
            else
            {
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                {
                    // 判断直线距离
                    if (Vector3.Distance(networkIdentity.transform.position, position) < visRange)
                        // add to result
                        newObservers.Add(networkIdentity.connectionToClient);
                }
            }
        }
    }

    void AddAllConnections(HashSet<NetworkConnectionToClient> newObservers)
    {
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            // authenticated and joined world with a player?
            if (conn != null && conn.isAuthenticated && conn.identity != null)
                newObservers.Add(conn);
        }
    }
}
