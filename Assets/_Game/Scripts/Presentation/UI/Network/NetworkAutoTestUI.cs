using Unity.Netcode;
using UnityEngine;

public class NetworkAutoTestUI : MonoBehaviour
{
    private void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        // Bağlantı kurulduysa mevcut durumu göster
        if (NetworkManager.Singleton.IsListening)
        {
            GUILayout.BeginArea(new Rect(10, 10, 250, 100));
            string status = NetworkManager.Singleton.IsHost ? "HOST (Server + Client)" : "CLIENT";
            GUILayout.Box($"<b>Ağ Durumu:</b> {status}");
            
            if (GUILayout.Button("Bağlantıyı Kes / Çık", GUILayout.Height(30)))
            {
                NetworkManager.Singleton.Shutdown();
            }
            GUILayout.EndArea();
            return;
        }

        // Bağlantı yoksa sol üstte otomatik hızlı test butonlarını çiz
        GUILayout.BeginArea(new Rect(10, 10, 220, 150));
        GUILayout.Box("<b>HIZLI MULTIPLAYER TEST</b>");

        if (GUILayout.Button("Host Ol (Kısayol: H)", GUILayout.Height(35)))
        {
            NetworkManager.Singleton.StartHost();
        }

        if (GUILayout.Button("Client Katıl (Kısayol: C)", GUILayout.Height(35)))
        {
            NetworkManager.Singleton.StartClient();
        }

        GUILayout.EndArea();
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening) return;

        // Farenin butonlara basmasıyla bile uğraşmak istemezsen klavye kısayolları:
        if (Input.GetKeyDown(KeyCode.H))
        {
            NetworkManager.Singleton.StartHost();
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            NetworkManager.Singleton.StartClient();
        }
    }
}