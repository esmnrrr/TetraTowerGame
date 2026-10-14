using UnityEngine;
using Game.Domain.Pieces; // PieceDefinition için (Eğer farklı bir namespace kullandıysan burayı güncelle)
using Game.Application.Factories;

namespace Game.Presentation.Input
{
    public class PiecePlacementController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Parçayı sahnede tutacağımız derinlik (Z ekseni). Genelde 0'dır.")]
        public float placementDepth = 0f;
        
        [Tooltip("Döndürme hızı (Q/E tuşları için)")]
        public float rotationSpeed = 90f;

        [Header("State (Şimdilik Inspector'dan Gözlemlemek İçin)")]
        public PieceDefinition currentPieceDef; // Seçili olan parça tanımı

        public PieceFactory pieceFactory;
        
        private GameObject ghostPiece; // Farenin ucunda gezen yarı saydam kopya
        private Camera mainCamera;
        private Plane placementPlane; // Işınların çarpacağı matematiksel düzlem
        public event System.Action OnPieceDropped; // Parça bırakıldığında TurnCoordinator'a haber vermek için
        private void Start()
        {
            mainCamera = Camera.main;
            // Matematiksel bir düzlem oluşturuyoruz. 
            // Vector3.back (veya forward) düzlemin yüzünün nereye baktığını (Normal),
            // Vector3(0, 0, placementDepth) ise düzlemin merkez noktasını belirtir.
            // Bu düzlem kameraya dönük görünmez bir duvar gibidir.
            placementPlane = new Plane(Vector3.back, new Vector3(0, 0, placementDepth));
        }

        private void Update()
        {
            // Eğer elimizde bir parça yoksa hiçbir şey yapma
            if (ghostPiece == null && currentPieceDef != null)
            {
                CreateGhostPiece();
            }

            if (ghostPiece != null)
            {
                HandleMovement();
                HandleRotation();
                HandlePlacement();
            }
        }

        // Test için dışarıdan (örneğin UI'dan) yeni parça seçilmesini sağlayan metod
        public void SelectPiece(PieceDefinition newPiece)
        {
            currentPieceDef = newPiece;
            if (ghostPiece != null)
            {
                Destroy(ghostPiece);
            }
        }

        private void CreateGhostPiece()
        {
            // PieceDefinition'dan Prefab'ı alıp sahneye üretiyoruz
            ghostPiece = Instantiate(currentPieceDef.Prefab);
            
            // "Ghost" olduğu için fizik kurallarından ve çarpışmalardan etkilenmemesi lazım.
            // Collider ve Rigidbody bileşenlerini kapatıyoruz.
            Collider coll = ghostPiece.GetComponent<Collider>();
            if (coll != null) coll.enabled = false;

            Rigidbody rb = ghostPiece.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Fizik motoru yönetmesin, biz yöneteceğiz
                rb.useGravity = false; // Yere düşmesin
            }

            // Tasarım Belgesi: Yarı saydam ghost (İleri aşamada buraya saydam bir materyal atanabilir)
            // Şimdilik sadece Unity'nin varsayılan haliyle hareket edecek.
        }

        private void HandleMovement()
        {
            // 1. Ekrandaki fare pozisyonundan (2D) kameranın baktığı yöne doğru bir ışın (Ray) oluştur
            Ray ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
            // 2. Bu ışın, Start() metodunda oluşturduğumuz düzlemle kesişiyor mu?
            float distanceToPlane;
            if (placementPlane.Raycast(ray, out distanceToPlane))
            {
                // 3. Kesiştiyse, ışının başlangıç noktasından düzleme olan mesafe kadar git
                // ve o tam 3D koordinatı bul.
                Vector3 targetPosition = ray.GetPoint(distanceToPlane);
                
                // 4. Ghost objeyi o noktaya taşı
                ghostPiece.transform.position = targetPosition;
            }
        }

        private void HandleRotation()
        {
            // Fare tekerleği (Scroll Wheel) ile döndürme
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (scroll != 0)
            {
                // Z ekseninde (kamera bize bakarken saat yönü/tersi) döndür
                ghostPiece.transform.Rotate(Vector3.forward, scroll * rotationSpeed, Space.World);
            }

            // Q ve E tuşları ile döndürme (Alternatif)
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
            {
                ghostPiece.transform.Rotate(Vector3.forward, rotationSpeed, Space.World);
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                ghostPiece.transform.Rotate(Vector3.forward, -rotationSpeed, Space.World);
            }
        }

        private void HandlePlacement()
        {
            // Hem sol tık (Mouse 0) hem de BOŞLUK (Space) tuşu ile test edelim
            if (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("---> 1. ADIM: Yerleştirme tuşuna basıldı!");

                if (pieceFactory == null)
                {
                    Debug.LogError("---> HATA: pieceFactory referansı boş! Kod burada durdu.");
                    return;
                }

                Debug.Log("---> 2. ADIM: Factory referansı var. Hayalet obje siliniyor...");

                Vector3 finalPosition = ghostPiece.transform.position;
                Quaternion finalRotation = ghostPiece.transform.rotation;

                // Hayaleti yok et
                Destroy(ghostPiece);
                ghostPiece = null;

                Debug.Log("---> 3. ADIM: Factory'den gerçek parça isteniyor...");
                
                GameObject realObj = pieceFactory.CreateRealPiece(currentPieceDef, finalPosition, finalRotation);
                if (realObj == null)
                {
                    Debug.LogError("---> HATA: Factory gerçek objeyi üretemedi!");
                }
                else
                {
                    Debug.Log("---> 4. ADIM: Gerçek obje başarıyla üretildi!");
                }

                // Orkestra şefine haber ver
                OnPieceDropped?.Invoke();

                // Elimizdeki tanımı boşaltalım ki tekrar hayalet üretmesin
                currentPieceDef = null; 
                
                Debug.Log("---> İŞLEM TAMAM: Farenin ucu boşaldı!");
            }
        }
    }
}