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
        private UnityEngine.Camera mainCamera;
        private Plane placementPlane; // Işınların çarpacağı matematiksel düzlem
        public event System.Action OnPieceDropped; // Parça bırakıldığında TurnCoordinator'a haber vermek için
        public Material ghostMaterial;

        [Header("Audio Settings")]
        public AudioSource audioSource;
        //public AudioClip placeSound;         // "Tik" sesi (Başarılı bırakma)
        public AudioClip invalidPlaceSound;  // "Dizt" sesi (Geçersiz alan / Hata)

        private void Start()
        {
            mainCamera = UnityEngine.Camera.main;
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
            ghostPiece = Instantiate(currentPieceDef.Prefab);

            MeshRenderer[] renderers = ghostPiece.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer r in renderers)
            {
                if (ghostMaterial != null) r.material = ghostMaterial;
            }
            
            // DİKKAT: Hayalet objenin üzerindeki TÜM Collider'ları kapatıyoruz!
            Collider[] colliders = ghostPiece.GetComponentsInChildren<Collider>();
            foreach (Collider coll in colliders)
            {
                coll.enabled = false;
            }

            Rigidbody rb = ghostPiece.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; 
                rb.useGravity = false; 
            }
        }

        private void HandleMovement()
        {
            if (ghostPiece == null) return;
            
            // 1. Ekrandaki fare pozisyonundan (2D) kameranın baktığı yöne doğru bir ışın (Ray) oluştur
            Ray ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            placementPlane = new Plane(Vector3.back, new Vector3(0, 0, placementDepth));
            
            // 2. Bu ışın, Start() metodunda oluşturduğumuz düzlemle kesişiyor mu?
            float distanceToPlane;
            if (placementPlane.Raycast(ray, out distanceToPlane))
            {
                // 3. Kesiştiyse, ışının başlangıç noktasından düzleme olan mesafe kadar git
                // ve o tam 3D koordinatı bul.
                Vector3 targetPosition = ray.GetPoint(distanceToPlane);

                targetPosition.z = placementDepth;
                
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
                if (!IsPlacementValid())
                {
                    Debug.LogWarning("DİKKAT: Parça başka bir objenin içinde! Buraya bırakılamaz.");

                    if (audioSource != null && invalidPlaceSound != null)
                    {
                        audioSource.PlayOneShot(invalidPlaceSound);
                    }

                    return; // Kodu burada durdur, parçayı bırakmasına İZİN VERME!
                }

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

        private bool IsPlacementValid()
        {
            // Hayalet obje üzerindeki tüm BoxCollider'ları bul
            BoxCollider[] boxes = ghostPiece.GetComponentsInChildren<BoxCollider>();
            int layerMask = LayerMask.GetMask("GameplayObjects");
            
            foreach (BoxCollider box in boxes)
            {
                // Kutunun dünyadaki merkez noktasını ve yarı boyutunu hesapla
                Vector3 worldCenter = box.transform.TransformPoint(box.center);
                Vector3 halfExtents = Vector3.Scale(box.size, box.transform.lossyScale) * 0.45f;
                
                // Radarı SADECE o katmana atıyoruz. Arka plandaki duvarları, sandalyeleri vs. GÖRMEZDEN GELECEK.
                Collider[] overlaps = Physics.OverlapBox(worldCenter, halfExtents, box.transform.rotation, layerMask, QueryTriggerInteraction.Ignore);

                foreach (Collider overlap in overlaps)
                {
                    // Eğer radara takılan obje, hayalet objenin (veya onun altındaki bir parçanın) KENDİSİYSE direkt pas geç!
                    if (overlap.gameObject == ghostPiece || overlap.transform.IsChildOf(ghostPiece.transform))
                    {
                        continue; // Kendi 2. veya 3. collider'ını buldun, sorun yok, aramaya devam et.
                    }

                    // Eğer çarptığı obje hayalet objenin kendisi değilse, içeride başka bir parça var demektir!
                    if (overlap.transform.root != ghostPiece.transform.root)
                    {
                        Debug.LogWarning("YERLEŞTİRME ENGELLENDİ! Çarpan Obje: " + overlap.gameObject.name);
                        return false; // Çakışma var, yerleştirme BAŞARISIZ!
                    }
                }
            }
            
            return true; // Etraf temiz, yerleştirme BAŞARILI!
        }
    }
}