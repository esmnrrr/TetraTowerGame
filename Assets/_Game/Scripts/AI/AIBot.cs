using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Pieces;
using Game.Domain.Turns;
using Game.Presentation.UI;
using Game.Application.Factories;

namespace Game.AI
{
    /// <summary>
    /// AI'ın "uygulayıcısı". TurnCoordinator.OnAITurnRequested olayını dinler.
    /// Akış: input kilidi -> mevcut 3 kart -> AIPlanner -> kart vurgusu -> ghost (kadrajın tepesi)
    ///       -> Slerp ile döndür -> Lerp ile boşluğa kaydır -> ghost sil -> gerçek parça
    ///       -> fiziği başlat -> turnCoordinator.BeginSimulation().
    /// </summary>
    public class AIBot : MonoBehaviour
    {
        [Header("Referanslar (boşsa sahneden otomatik bulunur)")]
        [SerializeField] private AIPlanner planner;
        [SerializeField] private TurnCoordinator turnCoordinator;
        [SerializeField] private HUDPresenter hud;
        [SerializeField] private PieceFactory pieceFactory;
        [SerializeField] private Material ghostMaterial;

        [Header("Zamanlama (saniye)")]
        [SerializeField] private float thinkDelay = 0.5f;
        [SerializeField] private float highlightDuration = 0.8f;
        [SerializeField] private float rotateDuration = 0.5f;
        [SerializeField] private float moveDuration = 1.3f;
        [SerializeField] private float hoverBeforeRelease = 0.2f;

        [Header("Ghost doğma yeri")]
        [SerializeField, Range(0.1f, 1f)] private float spawnHeightFactor = 0.8f;

        public bool IsPlaying { get; private set; }

        private GameObject ghostPiece;

        // ------------------------------------------------------------------ Kurulum
        private void Awake()
        {
            if (planner == null) planner = GetComponent<AIPlanner>();
            if (turnCoordinator == null) turnCoordinator = FindAnyObjectByType<TurnCoordinator>();

            if (turnCoordinator != null)
            {
                if (hud == null) hud = turnCoordinator.uiPresenter;
                if (pieceFactory == null && turnCoordinator.placementController != null)
                    pieceFactory = turnCoordinator.placementController.pieceFactory;
            }
        }

        private void OnEnable()
        {
            if (turnCoordinator != null) turnCoordinator.OnAITurnRequested += StartTurn;
        }

        private void OnDisable()
        {
            if (turnCoordinator != null) turnCoordinator.OnAITurnRequested -= StartTurn;
        }

        public void StartTurn()
        {
            if (IsPlaying) return;

            if (planner == null || turnCoordinator == null || hud == null || pieceFactory == null)
            {
                Debug.LogError("[AIBot] Referanslar eksik (planner / turnCoordinator / hud / pieceFactory).");
                return;
            }
            StartCoroutine(PlayTurn());
        }

        // ------------------------------------------------------------------ Ana akış
        private IEnumerator PlayTurn()
        {
            IsPlaying = true;
            bool completed = false;

            try
            {
                // 1) İnsan müdahale edemesin
                hud.SetHumanInputInteractable(false);
                if (thinkDelay > 0f) yield return new WaitForSeconds(thinkDelay);

                // 2) Yeni kart üretme: UI'daki mevcut 3 kart
                IReadOnlyList<PieceDefinition> options = hud.CurrentOptions;
                if (options == null || options.Count == 0)
                {
                    Debug.LogError("[AIBot] UI'da kart yok (CurrentOptions boş).");
                    yield break;
                }

                var prefabs = new List<GameObject>(options.Count);
                var masses = new List<float>(options.Count);
                for (int i = 0; i < options.Count; i++)
                {
                    prefabs.Add(options[i].Prefab);
                    float mass = 1f;
                    var prb = options[i].Prefab.GetComponent<Rigidbody>();
                    if (prb == null) prb = options[i].Prefab.GetComponentInChildren<Rigidbody>();
                    if (prb != null) mass = prb.mass;
                    masses.Add(mass);
                }

                // 3) En iyi hamleyi planner seçsin (denge farkındalıklı)
                AIPlanner.AIMove move = default;
                yield return planner.PlanRoutine(prefabs, masses, m => move = m);

                if (!move.found)
                {
                    move.found = true;
                    move.cardIndex = 0;
                    move.position = planner.FallbackPosition;
                    move.rotation = Quaternion.identity;
                }

                PieceDefinition chosen = options[move.cardIndex];

                // 4) UI'da seçilen kartı vurgula
                hud.HighlightCardForAI(move.cardIndex);
                if (highlightDuration > 0f) yield return new WaitForSeconds(highlightDuration);

                // 5) Ghost: kadrajın en üstünde doğar
                Vector3 spawnPos = GetSpawnPosition();
                Quaternion spawnRot = Quaternion.identity;
                ghostPiece = Instantiate(chosen.Prefab, spawnPos, spawnRot);
                ConvertToGhost(ghostPiece);

                // 6) Önce hedef rotasyona Slerp
                yield return RotateGhost(ghostPiece.transform, spawnRot, move.rotation, rotateDuration);

                // 7) Sonra doğrudan hedef X,Y'ye Lerp (yukarıdan düşmez, boşluğa girer)
                yield return MoveGhost(ghostPiece.transform, spawnPos, move.position, moveDuration);
                if (hoverBeforeRelease > 0f) yield return new WaitForSeconds(hoverBeforeRelease);

                // 8) Ghost'u sil, gerçek parçayı tam o noktada üret
                Destroy(ghostPiece);
                ghostPiece = null;

                GameObject real = pieceFactory.CreateRealPiece(chosen, move.position, move.rotation);
                if (real == null)
                {
                    Debug.LogError("[AIBot] Factory gerçek parçayı üretemedi.");
                    yield break;
                }

                // 9) Fizik: önce kinematik kapat, sonra hızları sıfırla (sıçramasın)
                Rigidbody rb = real.GetComponent<Rigidbody>();
                if (rb == null) rb = real.GetComponentInChildren<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // 10) İnsanla AYNI giriş noktası: simülasyonu ve settle takibini başlat.
                //     (İnsan girdisi bu turda zaten kilitli; sıra insana gelince TurnCoordinator açar.)
                turnCoordinator.BeginSimulation();

                completed = true;
            }
            finally
            {
                if (ghostPiece != null) { Destroy(ghostPiece); ghostPiece = null; }
                if (!completed && hud != null) hud.SetHumanInputInteractable(true);
                IsPlaying = false;
            }
        }

        // ------------------------------------------------------------------ Ghost
        private Vector3 GetSpawnPosition()
        {
            Vector3 c = planner.PlatformCenter;
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return new Vector3(c.x, planner.FallbackPosition.y, c.z);

            float y;
            if (cam.orthographic)
            {
                y = cam.transform.position.y + cam.orthographicSize * spawnHeightFactor;
            }
            else
            {
                // Perspektifli kamerada: kadrajın üst kısmından z düzlemine ışın at
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f + 0.5f * spawnHeightFactor, 0f));
                var plane = new Plane(Vector3.back, new Vector3(0f, 0f, c.z));
                y = plane.Raycast(ray, out float d) ? ray.GetPoint(d).y : cam.transform.position.y;
            }
            return new Vector3(c.x, y, c.z);
        }

        /// <summary>Kamera/çöküş kodu ghost'u kule sanmasın: Untagged, çarpışma yok, fizik yok, ghost materyali.</summary>
        private void ConvertToGhost(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.tag = "Untagged";

            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;

            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.detectCollisions = false;
            }

            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;

            if (ghostMaterial == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                r.sharedMaterials = mats;
            }
        }

        private IEnumerator RotateGhost(Transform t, Quaternion from, Quaternion to, float duration)
        {
            if (duration <= 0f) { t.rotation = to; yield break; }
            float time = 0f;
            while (time < duration)
            {
                if (t == null) yield break;
                time += Time.deltaTime;
                t.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration)));
                yield return null;
            }
            if (t != null) t.rotation = to;
        }

        private IEnumerator MoveGhost(Transform t, Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f) { t.position = to; yield break; }
            float time = 0f;
            while (time < duration)
            {
                if (t == null) yield break;
                time += Time.deltaTime;
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration)));
                yield return null;
            }
            if (t != null) t.position = to;
        }
    }
}