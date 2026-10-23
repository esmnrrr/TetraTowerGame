using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Pieces;
using Game.Application.UseCases;

namespace Game.AI
{
    public enum AIDifficulty { Easy, Normal, Hard }

    /// <summary>
    /// AI'ın "beyni" (v5: simülasyonlu). Raycast KULLANMAZ.
    ///
    /// 1) TARAMA   : Her kart x 24 açı x her X sütunu için parçayı yukarıdan "bırakır"; ilk temasta durur
    ///               (tırtıklı yüzeyde oyuğa girer). Ağırlık merkezi desteğin üstünde değilse elenir.
    /// 2) KISA LİSTE: Kart başına en iyi N farklı aday seçilir (birbirine yakın kopyalar elenir).
    /// 3) SİMÜLASYON: Her aday, platformun ve kulenin fiziksel kopyasında (PhysicsSandbox) GERÇEKTEN oynatılır.
    ///               Kule çökerse aday elenir. Hayatta kalanlar; sarsıntı, sapma ve rakibe bıraktığı zorluğa göre puanlanır.
    /// 4) SAĞLAMLIK : En iyi adaylar küçük sapmalarla (kayma/dönme) tekrar denenir; titrek hamleler elenir.
    /// Sandbox çalışmazsa (kurulamadı / gerçek sahneyle uyuşmadı) 1-2 aşamanın sezgisel puanıyla oynar.
    /// </summary>
    public class AIPlanner : MonoBehaviour
    {
        public struct AIMove
        {
            public bool found;
            public int cardIndex;
            public Vector3 position;
            public Quaternion rotation;
            public int rotationDegrees;
            public float score;
        }

        private struct LocalBox
        {
            public Vector3 center;
            public Quaternion rot;
            public Vector3 half;
        }

        private sealed class Shape
        {
            public LocalBox[] boxes;
            public Vector3 com;
            public float mass;
            public BodyBlueprint blueprint;
        }

        private struct Candidate
        {
            public int card;
            public Vector3 pos;
            public Quaternion rot;
            public float angle;
            public float comX;
            public float heuristic;
        }

        private sealed class Evaluated
        {
            public Candidate c;
            public PhysicsSandbox.Result r;
            public float score;
            public bool robust = true;
        }

        [Header("Platform")]
        [SerializeField] private Vector3 platformCenter = new Vector3(-6.7f, 1.223348f, 0f);
        [Tooltip("Platformun yarı genişliği (kanatlar dahil). Tarama alanını ve tork sınırını belirler.")]
        [SerializeField] private float platformHalfWidth = 0.35f;
        [Tooltip("Platformun iki yanında, parçaların taşabileceği ek alan (hücre sayısı)")]
        [SerializeField] private float sideMarginCells = 3f;

        [Header("Ölçek")]
        [Tooltip("0 = otomatik (parçanın en küçük kutu kenarı)")]
        [SerializeField] private float cellSizeOverride = 0f;

        [Header("Çarpışma kontrolü")]
        [SerializeField, Range(0.5f, 1f)] private float extentsShrink = 0.85f;
        [Tooltip("0 bırakırsan 'GameplayObjects' katmanı. Platform (ve tırtık çocukları) bu katmanda OLMALI.")]
        [SerializeField] private LayerMask collisionMask = 0;

        [Header("Tarama (tırtıklı platform için ince açılar)")]
        [Tooltip("15 = 24 açı. Yavaşsa 22.5 / 30 yap (30, 45 dereceyi de kapsar).")]
        [SerializeField, Range(5f, 90f)] private float rotationStepDegrees = 15f;
        [SerializeField, Range(0f, 0.4f)] private float supportMarginRatio = 0.1f;

        [Header("Sezgisel puan (kısa liste + yedek mod)")]
        [SerializeField] private float depthWeight = 1f;
        [SerializeField] private float centerWeight = 1.5f;
        [SerializeField] private float supportWeight = 1.5f;
        [SerializeField] private float balanceWeight = 2f;
        [Tooltip("Sezgisel mod için güvenli tork = en ağır parça x yarı genişlik x bu çarpan")]
        [SerializeField] private float torqueSafetyFactor = 1.5f;
        [SerializeField] private float jitter = 0.02f;

        [Header("Simülasyon (gerçek fizikle deneme)")]
        [SerializeField] private bool useSandbox = true;
        [SerializeField] private AIDifficulty difficulty = AIDifficulty.Normal;
        [Tooltip("Kart başına simüle edilecek farklı aday sayısı. Yavaşsa düşür (4-6).")]
        [SerializeField, Range(1, 16)] private int candidatesPerCard = 8;
        [Tooltip("Simülasyon için kare başına ayrılan süre (ms)")]
        [SerializeField] private float simBudgetMs = 8f;
        [Tooltip("0 = rakibe tuzak kurma (sadece güvenli oyna), 1 = rakibi en zor duruma sokmaya çalış")]
        [SerializeField, Range(0f, 1f)] private float aggression = 0.4f;
        [Tooltip("Rakibe en zor duruma karşılık gelen platform eğimi (0 = düz, 1 = menteşe sınırı). 0.4-0.6 iyi.")]
        [SerializeField, Range(0.1f, 0.9f)] private float targetTilt01 = 0.5f;
        [SerializeField] private float disturbWeight = 3f;
        [SerializeField] private float shiftWeight = 2f;
        [SerializeField] private float opponentWeight = 3f;
        [Tooltip("Sağlamlık testinde adayın kaydırılacağı mesafe (metre)")]
        [SerializeField] private float robustnessOffset = 0.004f;
        [Tooltip("Sağlamlık testinde adayın döndürüleceği açı (derece)")]
        [SerializeField] private float robustnessAngle = 1.5f;
        [Tooltip("Platform merkezinin bu kadar altına düşen parça 'düştü' sayılır")]
        [SerializeField] private float fallDepth = 2f;
        [Tooltip("Platformun çevresinde bu yarıçaptaki sabit colliderlar (masa vb.) sandbox'a kopyalanır")]
        [SerializeField] private float environmentRadius = 3f;
        [Header("Sandbox referansları (boşsa sahneden bulunur)")]
        [SerializeField] private Rigidbody platformBody;
        [SerializeField] private CollapseEvaluator collapseEvaluator;

        [Header("Performans / Debug")]
        [SerializeField] private float frameBudgetMs = 4f;
        [SerializeField] private bool logDecision = true;
        [SerializeField] private bool logEveryCandidate = false;

        private int mask;
        private float scanStep, supportCheckDistance, snapStep, maxOverlapResolveUp;

        public Vector3 PlatformCenter => platformCenter;
        public Vector3 FallbackPosition => new Vector3(platformCenter.x, GetTowerTopY() + 0.1f, platformCenter.z);

        // ------------------------------------------------------------------ Genel API
        public IEnumerator PlanRoutine(IReadOnlyList<PieceDefinition> options, Action<AIMove> onDone)
        {
            AIMove none = default;
            mask = collisionMask.value != 0 ? collisionMask.value : LayerMask.GetMask("GameplayObjects");

            // ---- Şekiller ve hücre boyutu
            var shapes = new List<Shape>(options.Count);
            float cell = float.MaxValue, maxMass = 0f;
            for (int i = 0; i < options.Count; i++)
            {
                Shape s = options[i] != null && options[i].Prefab != null ? BuildShape(options[i]) : null;
                shapes.Add(s);
                if (s == null) continue;
                maxMass = Mathf.Max(maxMass, s.mass);
                foreach (LocalBox b in s.boxes)
                {
                    float m = Mathf.Min(b.half.x, b.half.y) * 2f;
                    if (m > 0.0001f && m < cell) cell = m;
                }
            }
            if (cellSizeOverride > 0f) cell = cellSizeOverride;
            if (cell == float.MaxValue) { onDone?.Invoke(none); yield break; }

            scanStep = cell / 3f;
            supportCheckDistance = cell / 6f;
            snapStep = cell / 30f;
            maxOverlapResolveUp = cell;

            // ---- 1) TARAMA
            var all = new List<Candidate>(2048);
            yield return ScanRoutine(shapes, cell, maxMass, all);

            if (all.Count == 0)
            {
                Debug.LogWarning("[AIPlanner] Hiç aday yok. Platform katmanı 'GameplayObjects' mı? (tırtık çocukları dahil)");
                onDone?.Invoke(none);
                yield break;
            }

            // ---- 2) KISA LİSTE
            List<Candidate> shortlist = BuildShortlist(all, cell, shapes.Count);

            // ---- 3) SİMÜLASYON
            AIMove move = default;
            PhysicsSandbox sandbox = useSandbox ? BuildSandbox(shapes) : null;
            try
            {
                bool simOk = sandbox != null && sandbox.IsOperational;
                if (simOk)
                {
                    PhysicsSandbox.Result baseline = default;
                    yield return sandbox.Run(-1, Vector3.zero, Quaternion.identity, r => baseline = r, simBudgetMs);
                    if (!baseline.survived || baseline.towerDisturbance > 0.03f || baseline.platformDrift > 2f)
                    {
                        Debug.LogWarning($"[AIPlanner] Sandbox gerçek sahneyle uyuşmuyor (kule kayması {baseline.towerDisturbance:F3} m, " +
                                         $"platform {baseline.platformDrift:F1}°, hayatta {baseline.survived}, düşen: {baseline.fellBody ?? "-"}). Sezgisel moda geçildi.");
                        simOk = false;
                    }
                }

                if (simOk)
                {
                    List<Evaluated> evaluated = null;
                    yield return EvaluateRoutine(sandbox, shortlist, cell, list => evaluated = list);
                    move = PickFromEvaluated(evaluated);
                }
            }
            finally
            {
                if (sandbox != null) sandbox.Dispose();
            }

            // ---- Yedek: sezgisel en iyi
            if (!move.found)
            {
                Candidate bestC = shortlist[0];
                foreach (Candidate c in shortlist) if (c.heuristic > bestC.heuristic) bestC = c;
                move = ToMove(bestC, bestC.heuristic);
                move.position = RefinePlacement(shapes[bestC.card].boxes, move.position, move.rotation);
                if (logDecision)
                    Debug.Log($"[AIPlanner] (sezgisel mod) kart {move.cardIndex}, açı {move.rotationDegrees}, " +
                              $"pos ({move.position.x:F3}, {move.position.y:F3}), puan {move.score:F2}");
            }

            onDone?.Invoke(move);
        }

        // ------------------------------------------------------------------ 1) Tarama
        private IEnumerator ScanRoutine(List<Shape> shapes, float cell, float maxMass, List<Candidate> output)
        {
            float currentTorque = GetCurrentTorque();
            float maxSafeTorque = Mathf.Max(0.0001f, maxMass * platformHalfWidth * torqueSafetyFactor);

            float xMin = platformCenter.x - (platformHalfWidth + sideMarginCells * cell);
            float xMax = platformCenter.x + (platformHalfWidth + sideMarginCells * cell);
            float yMin = platformCenter.y;
            float yMax = GetTowerTopY() + 5f * cell;

            int xSteps = Mathf.FloorToInt((xMax - xMin) / scanStep + 0.0001f);
            float halfRangeX = Mathf.Max(0.0001f, xMax - platformCenter.x);
            float sliceStart = Time.realtimeSinceStartup;

            for (int card = 0; card < shapes.Count; card++)
            {
                Shape shape = shapes[card];
                if (shape == null || shape.boxes.Length == 0) continue;

                for (float angle = 0f; angle < 360f - 0.01f; angle += rotationStepDegrees)
                {
                    Quaternion rot = Quaternion.Euler(0f, 0f, angle);
                    Vector3 comOffset = rot * shape.com;

                    for (int ix = 0; ix <= xSteps; ix++)
                    {
                        float x = xMin + ix * scanStep;

                        if (TryDropToRest(shape.boxes, x, yMax, yMin, rot, out float y))
                        {
                            var pos = new Vector3(x, y, platformCenter.z);
                            float comX = pos.x + comOffset.x;

                            if (HasStableSupport(shape.boxes, pos, rot, comX, out float support01))
                            {
                                float torqueNew = currentTorque + shape.mass * (comX - platformCenter.x);
                                float imbalance = Mathf.Abs(torqueNew) / maxSafeTorque;

                                float depth01 = 1f - Mathf.InverseLerp(yMin, yMax, y);
                                float center01 = 1f - Mathf.Clamp01(Mathf.Abs(comX - platformCenter.x) / halfRangeX);

                                float h = depthWeight * depth01
                                        + centerWeight * center01
                                        + supportWeight * support01
                                        - balanceWeight * Mathf.Min(imbalance, 1f)
                                        - 10f * Mathf.Max(0f, imbalance - 1f)
                                        + UnityEngine.Random.Range(-jitter, jitter);

                                output.Add(new Candidate
                                {
                                    card = card, pos = pos, rot = rot, angle = angle, comX = comX, heuristic = h
                                });
                            }
                        }

                        if ((Time.realtimeSinceStartup - sliceStart) * 1000f > frameBudgetMs)
                        {
                            yield return null;
                            sliceStart = Time.realtimeSinceStartup;
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ 2) Kısa liste
        /// <summary>Kart başına en iyi N aday; birbirine çok yakın (aynı X, benzer açı) kopyalar elenir.</summary>
        private List<Candidate> BuildShortlist(List<Candidate> all, float cell, int cardCount)
        {
            var result = new List<Candidate>();
            for (int card = 0; card < cardCount; card++)
            {
                var ofCard = all.FindAll(c => c.card == card);
                ofCard.Sort((a, b) => b.heuristic.CompareTo(a.heuristic));

                int taken = 0;
                int startIndex = result.Count;
                foreach (Candidate c in ofCard)
                {
                    bool tooSimilar = false;
                    for (int i = startIndex; i < result.Count; i++)
                    {
                        Candidate o = result[i];
                        if (Mathf.Abs(o.pos.x - c.pos.x) < cell * 1.2f &&
                            Mathf.Abs(Mathf.DeltaAngle(o.angle, c.angle)) < 25f)
                        { tooSimilar = true; break; }
                    }
                    if (tooSimilar) continue;

                    result.Add(c);
                    if (++taken >= candidatesPerCard) break;
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ 3) Sandbox
        private PhysicsSandbox BuildSandbox(List<Shape> shapes)
        {
            HingeJoint hinge = null;
            Rigidbody body = platformBody;
            if (body != null) hinge = body.GetComponent<HingeJoint>();
            if (body == null)
            {
                hinge = FindAnyObjectByType<HingeJoint>();
                body = hinge != null ? hinge.GetComponent<Rigidbody>() : null;
            }
            if (body == null)
            {
                Debug.LogWarning("[AIPlanner] Platform rigidbody'si bulunamadı (HingeJoint yok). 'Platform Body' alanını doldur. Sezgisel moda geçildi.");
                return null;
            }

            var tower = new List<Rigidbody>();
            var seen = new HashSet<Rigidbody>();
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Piece"))
            {
                Rigidbody rb = go.GetComponentInParent<Rigidbody>();
                if (rb == null || rb == body || rb.isKinematic || !seen.Add(rb)) continue;
                tower.Add(rb);
            }

            var kill = new List<Bounds>();
            CollapseEvaluator ce = collapseEvaluator != null ? collapseEvaluator : FindAnyObjectByType<CollapseEvaluator>();
            if (ce != null)
                foreach (Collider col in ce.GetComponentsInChildren<Collider>())
                    if (col.isTrigger) kill.Add(col.bounds);
            if (kill.Count == 0)
                Debug.LogWarning("[AIPlanner] CollapseEvaluator trigger'ı bulunamadı; çöküş sadece 'çok aşağı düştü' ile algılanacak.");

            // Ortam: platformun dayanabildiği masa vb. (dinamik gövdeler hariç; onları ayrıca kopyalıyoruz)
            var environment = new List<Collider>();
            foreach (Collider col in Physics.OverlapSphere(platformCenter, environmentRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Rigidbody crb = col.attachedRigidbody;
                if (crb != null && !crb.isKinematic) continue;
                environment.Add(col);
                if (environment.Count >= 64) break;
            }

            var blueprints = new List<BodyBlueprint>(shapes.Count);
            foreach (Shape s in shapes) blueprints.Add(s != null ? s.blueprint : null);

            return PhysicsSandbox.Create(body, hinge, tower, blueprints, kill, environment, platformCenter.y - fallDepth);
        }

        private IEnumerator EvaluateRoutine(PhysicsSandbox sandbox, List<Candidate> shortlist, float cell, Action<List<Evaluated>> onDone)
        {
            GetDifficultyParams(out float aggr, out _, out int robustFinalists);
            var evaluated = new List<Evaluated>(shortlist.Count);

            foreach (Candidate c in shortlist)
            {
                PhysicsSandbox.Result r = default;
                yield return sandbox.Run(c.card, c.pos, c.rot, x => r = x, simBudgetMs);

                var e = new Evaluated { c = c, r = r };
                e.score = r.survived ? ScoreResult(c, r, cell, aggr) : float.NegativeInfinity;
                evaluated.Add(e);

                if (logEveryCandidate)
                    Debug.Log($"[AIPlanner] aday kart {c.card} açı {c.angle:F0} x {c.pos.x:F3} -> " +
                              (r.survived ? $"HAYATTA skor {e.score:F2}, sarsıntı {r.towerDisturbance:F3}, kayma {r.candidateShift:F3}, eğim {r.tiltDegrees:F1}°"
                                          : $"ÇÖKTÜ ({r.duration:F2}s)"));
            }

            // Sağlamlık: en iyi adaylar hafifçe kaydırılıp/döndürülüp tekrar denenir.
            if (robustFinalists > 0)
            {
                var order = new List<Evaluated>(evaluated);
                order.Sort((a, b) => b.score.CompareTo(a.score));
                for (int i = 0; i < order.Count && i < robustFinalists; i++)
                {
                    Evaluated e = order[i];
                    if (float.IsNegativeInfinity(e.score)) break;

                    Vector3[] offsets = { new Vector3(robustnessOffset, 0f, 0f), new Vector3(-robustnessOffset, 0f, 0f) };
                    float[] angles = { robustnessAngle, -robustnessAngle };
                    for (int k = 0; k < offsets.Length && e.robust; k++)
                    {
                        PhysicsSandbox.Result pr = default;
                        Quaternion rot = Quaternion.Euler(0f, 0f, angles[k]) * e.c.rot;
                        yield return sandbox.Run(e.c.card, e.c.pos + offsets[k], rot, x => pr = x, simBudgetMs);
                        if (!pr.survived) e.robust = false;
                    }
                    if (!e.robust) e.score -= 50f;
                }
            }

            onDone?.Invoke(evaluated);
        }

        private float ScoreResult(Candidate c, PhysicsSandbox.Result r, float cell, float aggr)
        {
            // Rakibe bırakılan zorluk: platform hedef eğime (örn. %50) ne kadar yakınsa o kadar iyi.
            float span = Mathf.Max(targetTilt01, 1f - targetTilt01);
            float difficulty01 = 1f - Mathf.Clamp01(Mathf.Abs(r.tilt01 - targetTilt01) / span);

            return 0.5f * c.heuristic
                 - disturbWeight * (r.towerDisturbance / cell)
                 - shiftWeight * (r.candidateShift / cell)
                 + opponentWeight * aggr * difficulty01
                 + UnityEngine.Random.Range(-jitter, jitter);
        }

        private AIMove PickFromEvaluated(List<Evaluated> evaluated)
        {
            GetDifficultyParams(out _, out int pickTop, out _);

            var survivors = evaluated.FindAll(e => !float.IsNegativeInfinity(e.score));
            int robustCount = survivors.FindAll(e => e.robust).Count;

            if (survivors.Count == 0)
            {
                // Her hamle kaybettiriyor: en geç çöken hamleyi seç.
                Evaluated last = evaluated[0];
                foreach (Evaluated e in evaluated) if (e.r.duration > last.r.duration) last = e;
                Debug.LogWarning("[AIPlanner] Çökmeyen hamle bulunamadı; en geç çöken seçildi.");
                return ToMove(last.c, last.r.duration);
            }

            survivors.Sort((a, b) => b.score.CompareTo(a.score));
            int index = pickTop <= 1 ? 0 : UnityEngine.Random.Range(0, Mathf.Min(pickTop, survivors.Count));
            Evaluated chosen = survivors[index];

            if (logDecision)
                Debug.Log($"[AIPlanner] Seçim: kart {chosen.c.card}, açı {chosen.c.angle:F0}, " +
                          $"pos ({chosen.c.pos.x:F3}, {chosen.c.pos.y:F3}), skor {chosen.score:F2} | " +
                          $"{evaluated.Count} aday, {survivors.Count} hayatta, {robustCount} sağlam | " +
                          $"sarsıntı {chosen.r.towerDisturbance:F3} m, eğim {chosen.r.tiltDegrees:F1}° (%{chosen.r.tilt01 * 100f:F0}) | zorluk {difficulty}");

            return ToMove(chosen.c, chosen.score);
        }

        private void GetDifficultyParams(out float aggr, out int pickTop, out int robustFinalists)
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    aggr = aggression * 0.25f; pickTop = 3; robustFinalists = 0; break;
                case AIDifficulty.Hard:
                    aggr = Mathf.Min(1f, aggression * 1.75f); pickTop = 1; robustFinalists = 5; break;
                default:
                    aggr = aggression; pickTop = 1; robustFinalists = 3; break;
            }
        }

        private static AIMove ToMove(Candidate c, float score) => new AIMove
        {
            found = true,
            cardIndex = c.card,
            position = c.pos,
            rotation = c.rot,
            rotationDegrees = Mathf.RoundToInt(c.angle),
            score = score
        };

        // ------------------------------------------------------------------ Kule durumu
        private float GetTowerTopY()
        {
            float top = platformCenter.y;
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Piece"))
            {
                Rigidbody rb = go.GetComponentInParent<Rigidbody>();
                if (rb != null && rb.isKinematic) continue;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                    top = Mathf.Max(top, r.bounds.max.y);
            }
            return top;
        }

        private float GetCurrentTorque()
        {
            float torque = 0f;
            var seen = new HashSet<Rigidbody>();
            foreach (GameObject go in GameObject.FindGameObjectsWithTag("Piece"))
            {
                Rigidbody rb = go.GetComponentInParent<Rigidbody>();
                if (rb == null || rb.isKinematic || !seen.Add(rb)) continue;
                torque += rb.mass * (rb.worldCenterOfMass.x - platformCenter.x);
            }
            return torque;
        }

        // ------------------------------------------------------------------ Çarpışma / denge
        private bool IsFree(LocalBox[] boxes, Vector3 pivotPos, Quaternion pivotRot, float shrink)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                Vector3 center = pivotPos + pivotRot * boxes[i].center;
                Quaternion orient = pivotRot * boxes[i].rot;
                if (Physics.CheckBox(center, boxes[i].half * shrink, orient, mask, QueryTriggerInteraction.Ignore))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Parçayı verilen X'te yukarıdan aşağı indirir; ilk temasta durdurur (tırtıklı yüzeyde oyuğun içine girer).
        /// Hiçbir şeye değmeden yMin'e inerse (platformun yanından düşüyorsa) false döner.
        /// </summary>
        private bool TryDropToRest(LocalBox[] boxes, float x, float yStart, float yMin, Quaternion rot, out float y)
        {
            y = yStart;
            var pos = new Vector3(x, y, platformCenter.z);
            if (!IsFree(boxes, pos, rot, extentsShrink)) return false;

            float coarse = scanStep * 1.5f;   // ~cell/2: kalın tırtıkların içinden geçmez
            bool hit = false;

            while (y - coarse >= yMin)
            {
                pos.y = y - coarse;
                if (!IsFree(boxes, pos, rot, extentsShrink)) { hit = true; break; }
                y -= coarse;
            }
            while (y - snapStep >= yMin)
            {
                pos.y = y - snapStep;
                if (!IsFree(boxes, pos, rot, extentsShrink)) { hit = true; break; }
                y -= snapStep;
            }
            return hit;
        }

        /// <summary>
        /// Değen kutuların kapladığı X aralığını bulur; ağırlık merkezi bu aralığın içinde değilse false.
        /// support01: temas aralığının parça genişliğine oranı (geniş destek = daha sağlam).
        /// </summary>
        private bool HasStableSupport(LocalBox[] boxes, Vector3 pivotPos, Quaternion pivotRot, float comX, out float support01)
        {
            support01 = 0f;
            float minX = float.MaxValue, maxX = float.MinValue;
            float pieceMin = float.MaxValue, pieceMax = float.MinValue;
            int touching = 0;
            Vector3 down = Vector3.down * supportCheckDistance;

            for (int i = 0; i < boxes.Length; i++)
            {
                Vector3 center = pivotPos + pivotRot * boxes[i].center;
                Quaternion orient = pivotRot * boxes[i].rot;

                Matrix4x4 m = Matrix4x4.Rotate(orient);
                Vector3 h = boxes[i].half;
                float halfWorldX = Mathf.Abs(m.m00) * h.x + Mathf.Abs(m.m01) * h.y + Mathf.Abs(m.m02) * h.z;
                pieceMin = Mathf.Min(pieceMin, center.x - halfWorldX);
                pieceMax = Mathf.Max(pieceMax, center.x + halfWorldX);

                if (!Physics.CheckBox(center + down, h * extentsShrink, orient, mask, QueryTriggerInteraction.Ignore))
                    continue;

                touching++;
                minX = Mathf.Min(minX, center.x - halfWorldX);
                maxX = Mathf.Max(maxX, center.x + halfWorldX);
            }

            if (touching == 0) return false;

            float margin = (maxX - minX) * supportMarginRatio;
            if (comX < minX + margin || comX > maxX - margin) return false;

            support01 = Mathf.Clamp01((maxX - minX) / Mathf.Max(0.0001f, pieceMax - pieceMin));
            return true;
        }

        private Vector3 RefinePlacement(LocalBox[] boxes, Vector3 pos, Quaternion rot)
        {
            float up = 0f;
            while (up < maxOverlapResolveUp && !IsFree(boxes, pos + Vector3.up * up, rot, 0.995f))
                up += snapStep;
            pos.y += up;
            return pos;
        }

        // ------------------------------------------------------------------ Prefab -> şekil
        private Shape BuildShape(PieceDefinition def)
        {
            GameObject prefab = def.Prefab;
            Rigidbody prb = prefab.GetComponent<Rigidbody>();
            if (prb == null) prb = prefab.GetComponentInChildren<Rigidbody>();
            float mass = def.Mass > 0f ? def.Mass : (prb != null ? prb.mass : 1f);

            var list = new List<LocalBox>();
            Transform root = prefab.transform;
            Vector3 rootScale = root.lossyScale;

            foreach (Collider col in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger) continue;

                if (col is BoxCollider box)
                {
                    Matrix4x4 rel = root.worldToLocalMatrix * box.transform.localToWorldMatrix;
                    Vector3 c = rel.MultiplyPoint3x4(box.center);
                    list.Add(new LocalBox
                    {
                        center = Vector3.Scale(c, rootScale),
                        rot = rel.rotation,
                        half = Vector3.Scale(Vector3.Scale(box.size * 0.5f, Abs(rel.lossyScale)), Abs(rootScale))
                    });
                }
                else
                {
                    Bounds b = col.bounds;
                    list.Add(new LocalBox
                    {
                        center = Vector3.Scale(root.InverseTransformPoint(b.center), rootScale),
                        rot = Quaternion.identity,
                        half = b.extents
                    });
                    Debug.LogWarning($"[AIPlanner] '{col.name}' BoxCollider değil, yaklaşık kutu kullanıldı.");
                }
            }

            if (list.Count == 0) Debug.LogError($"[AIPlanner] '{prefab.name}' prefab'ında collider yok.");

            Vector3 weighted = Vector3.zero;
            float volume = 0f;
            foreach (LocalBox b in list)
            {
                float v = b.half.x * b.half.y * b.half.z;
                weighted += b.center * v;
                volume += v;
            }

            return new Shape
            {
                boxes = list.ToArray(),
                com = volume > 0f ? weighted / volume : Vector3.zero,
                mass = Mathf.Max(0.0001f, mass),
                blueprint = prb != null ? BodyBlueprint.Capture(prb, root, def.PhysicsMaterial, mass) : null
            };
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(platformCenter, 0.03f);
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(platformCenter + Vector3.up * 0.5f, new Vector3(platformHalfWidth * 2f, 1f, 0.05f));
        }
    }
}