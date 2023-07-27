using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
public class MyTeam : NetworkBehaviour
{
    [Tooltip("Set this to the same value on all networked objects that belong to a given team")]
    [SyncVar] public string teamId = "Lobby";

    [Tooltip("When enabled this object is visible to all clients. Typically this would be true for player objects")]
    [SyncVar] public bool forceShown;

    public void SetteamId(string value) { this.teamId = value; }
}
