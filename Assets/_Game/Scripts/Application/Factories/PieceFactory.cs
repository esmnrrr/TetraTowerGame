using UnityEngine;
using Game.Domain.Pieces; // PieceDefinition için
using Unity.Netcode; // NetworkObject ve NetworkManager için

namespace Game.Application.Factories
{
    public class PieceFactory : MonoBehaviour
    {
        // İstediğimiz pozisyonda ve açıda gerçek fiziksel parçayı üretir
        public GameObject CreateRealPiece(PieceDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || definition.Prefab == null) return null;

            // 1. Prefab'ın bir kopyasını sahneye o anki farenin (hayalet objenin) konum ve rotasyonuyla üret
            GameObject realPiece = Instantiate(definition.Prefab, position, rotation);

            // 2. Orijinal isimle görünmesi için '(Clone)' ekini kaldır (İsteğe bağlı temizlik)
            realPiece.name = definition.DisplayName;

            // 3. Fizik ayarlarını yap (Emin olmak için)
            Rigidbody rb = realPiece.GetComponent<Rigidbody>();

            // MULTIPLAYER AĞA DOĞURMA
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkObject netObj = realPiece.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true); // true: sahneye spawn edildiğinde tüm client'lara otomatik olarak gönderilsin

                    // KRİTİK FİZİK ÇÖZÜMÜ:
                    // Eğer bu bilgisayar Client ise yerel fiziği kapatıyoruz!
                    // Parçanın hareketini yalnızca Host'un NetworkTransform verisi yönlendirmelidir.
                    if (rb != null && !NetworkManager.Singleton.IsServer)
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    }
                    else if (rb != null && NetworkManager.Singleton.IsServer)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                        if (definition.Mass > 0) rb.mass = definition.Mass;
                    }
                }
                else
                {
                    Debug.LogError($"[PieceFactory] {realPiece.name} üzerinde NetworkObject bileşeni YOK! Ağda paylaşılamaz.");
                }
            }
            else if (rb != null) // Tek oyunculu (Practice/VsAI) modlar için normal fizik
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                if (definition.Mass > 0) rb.mass = definition.Mass;
            }

            // 4. Collider'ları etkinleştir ve fizik materyalini uygula
            Collider[] colliders = realPiece.GetComponentsInChildren<Collider>();
            foreach (Collider coll in colliders)
            {
                coll.enabled = true;
                if (definition.PhysicsMaterial != null) coll.material = definition.PhysicsMaterial;

            }

            return realPiece;
        }
    }
}