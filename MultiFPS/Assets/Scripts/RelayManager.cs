using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async Task<string> CreateRelayAndStartHost(int maxConnections)
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode       = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            NetworkManager.Singleton.StartHost();
            Debug.Log("[Relay] Host started. JoinCode: " + joinCode);
            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError("[Relay] Host error: " + e.Message);
            return null;
        }
    }

    public async Task<bool> JoinRelayAndStartClient(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            NetworkManager.Singleton.StartClient();
            Debug.Log("[Relay] Client connected. Code: " + joinCode);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[Relay] Client error: " + e.Message);
            return false;
        }
    }
}