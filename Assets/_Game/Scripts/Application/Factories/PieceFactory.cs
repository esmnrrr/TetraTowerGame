using UnityEngine;
using Game.Domain.Pieces; // PieceDefinition için (Kendi namespace'in farklıysa düzelt)

namespace Game.Application.Factories
{
    public class PieceFactory : MonoBehaviour
    {
        // İstediğimiz pozisyonda ve açıda gerçek fiziksel parçayı üretir
        public GameObject CreateRealPiece(PieceDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogError("PieceFactory: PieceDefinition veya Prefab eksik!");
                return null;
            }

            // 1. Prefab'ın bir kopyasını sahneye o anki farenin (hayalet objenin) konum ve rotasyonuyla üret
            GameObject realPiece = Instantiate(definition.Prefab, position, rotation);

            // 2. Orijinal isimle görünmesi için '(Clone)' ekini kaldır (İsteğe bağlı temizlik)
            realPiece.name = definition.DisplayName;

            // 3. Fizik ayarlarını yap (Emin olmak için)
            Rigidbody rb = realPiece.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; // Fizik motoru yönetsin (Düşsün)
                rb.useGravity = true;   // Yerçekimi etki etsin
                
                // Eğer PieceDefinition'da özel bir kütle (Mass) tanımladıysak onu uygula
                if (definition.Mass > 0)
                {
                    rb.mass = definition.Mass;
                }
            }

            Collider coll = realPiece.GetComponent<Collider>();
            if (coll != null)
            {
                coll.enabled = true; // Çarpışmalar açık olsun

                // Eğer PieceDefinition'da özel bir sürtünme materyali varsa onu uygula
                if (definition.PhysicsMaterial != null)
                {
                    coll.material = definition.PhysicsMaterial;
                }
            }

            return realPiece;
        }
    }
}