using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// AI'ın "beyni" (v3: denge farkındalıklı). Raycast KULLANMAZ; Izgara Taraması + Physics.CheckBox.
    ///
    /// Bir aday (X, Y, rotasyon) şu aşamalardan geçer:
    ///   1) BOŞLUK     : Parça kutuları (%15 küçük) başka bir objeyle iç içe değil.
    ///   2) TABAN      : Parça biraz aşağı inerse bir şeye değiyor (havada asılı değil).
    ///   3) DENGE      : Parçanın ağırlık merkezi, değdiği yüzeylerin kapladığı X aralığının içinde
    ///                   (kenarda yarı havada duran parçalar elenir).
    ///   4) TORK       : Platform bir mil üzerinde sallandığı için, kuledeki tüm kütlelerin
    ///                   (kütle x mile uzaklık) toplamı güvenli sınırı aşmamalı.
    ///
    /// Puan: derinlik + merkeze yakınlık + denge tercihi.
    ///   aggression = 0  -> kuleyi dengede tutmaya çalışır (kolay, güvenli)
    ///   aggression = 1  -> güvenli sınıra kadar kuleyi eğer, rakibe zor pozisyon bırakır (tuzak)
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

        private class Shape
        {
            public LocalBox[] boxes;
            public Vector3 com;     // pivota göre ağırlık merkezi (kutu hacimlerine göre)
            public float mass;
        }

        [Header("Platform (hesapların merkezi ve mili)")]
        [SerializeField] private Vector3 platformCenter = new Vector3(-6.7f, 1.223348f, 0f);
        [Tooltip("Platformun yarı genişliği. Genişlik ~0.7 => 0.35")]
        [SerializeField] private float platformHalfWidth = 0.35f;
        [Tooltip("Platformun iki yanında, parçaların taşabileceği ek alan (hücre sayısı)")]
        [SerializeField] private float sideMarginCells = 3f;

        [Header("Ölçek")]
        [Tooltip("0 = otomatik (parçanın en küçük kutu kenarı)")]
        [SerializeField] private float cellSizeOverride = 0f;

        [Header("Çarpışma kontrolü")]
        [SerializeField, Range(0.5f, 1f)] private float extentsShrink = 0.85f;
        [Tooltip("0 bırakırsan 'GameplayObjects' katmanı. Platform da bu katmanda OLMALI.")]
        [SerializeField] private LayerMask collisionMask = 0;

        [Header("Denge kapıları")]
        [Tooltip("Ağırlık merkezi taban aralığının kenarından en az bu oranda içeride olmalı (0.1 = %10)")]
        [SerializeField, Range(0f, 0.4f)] private float supportMarginRatio = 0.1f;
        [Tooltip("Güvenli tork = en ağır parça x platform yarı genişliği x bu çarpan. Küçük = daha temkinli")]
        [SerializeField] private float torqueSafetyFactor = 1.5f;

        [Header("Puanlama")]
        [SerializeField] private float depthWeight = 2f;
        [SerializeField] private float centerWeight = 1.5f;
        [SerializeField] private float balanceWeight = 3f;
        [Tooltip("0 = kuleyi dengele, 1 = güvenli sınıra kadar eğ (rakibe tuzak kur)")]
        [SerializeField, Range(0f, 1f)] private float aggression = 0.4f;
        [SerializeField] private float jitter = 0.02f;

        [Header("Performans / Debug")]
        [SerializeField] private float frameBudgetMs = 4f;
        [SerializeField] private bool logDecision = true;

        private static readonly int[] RotationAngles = { 0, 90, 180, 270 };

        private int mask;
        private float scanStep, supportCheckDistance, snapStep, maxOverlapResolveUp;

        public Vector3 PlatformCenter => platformCenter;
        public Vector3 FallbackPosition => new Vector3(platformCenter.x, GetTowerTopY() + 0.1f, platformCenter.z);

        // ------------------------------------------------------------------ Genel API
        public IEnumerator PlanRoutine(IReadOnlyList<GameObject> prefabs, IReadOnlyList<float> masses, Action<AIMove> onDone)
        {
            AIMove best = default;
            best.score = float.NegativeInfinity;

            mask = collisionMask.value != 0 ? collisionMask.value : LayerMask.GetMask("GameplayObjects");

            // 1) Şekiller, hücre boyutu, en ağır parça
            var shapes = new List<Shape>(prefabs.Count);
            float cell = float.MaxValue, maxMass = 0f;
            for (int i = 0; i < prefabs.Count; i++)
            {
                Shape s = prefabs[i] != null ? BuildShape(prefabs[i], i < masses.Count ? masses[i] : 1f) : null;
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
            if (cell == float.MaxValue) { onDone?.Invoke(best); yield break; }

            scanStep = cell / 3f;
            supportCheckDistance = cell / 6f;
            snapStep = cell / 30f;
            maxOverlapResolveUp = cell;

            // 2) Kule durumu: mevcut tork ve tepe yüksekliği
            float currentTorque = GetCurrentTorque();
            float maxSafeTorque = Mathf.Max(0.0001f, maxMass * platformHalfWidth * torqueSafetyFactor);

            float xMin = platformCenter.x - (platformHalfWidth + sideMarginCells * cell);
            float xMax = platformCenter.x + (platformHalfWidth + sideMarginCells * cell);
            float yMin = platformCenter.y;
            float yMax = GetTowerTopY() + 5f * cell;

            int xSteps = Mathf.FloorToInt((xMax - xMin) / scanStep + 0.0001f);
            int ySteps = Mathf.FloorToInt((yMax - yMin) / scanStep + 0.0001f);
            float halfRangeX = Mathf.Max(0.0001f, xMax - platformCenter.x);

            float sliceStart = Time.realtimeSinceStartup;
            float bestImbalance = 0f, bestDepth = 0f, bestCenter = 0f;
            int accepted = 0;

            for (int card = 0; card < shapes.Count; card++)
            {
                Shape shape = shapes[card];
                if (shape == null || shape.boxes.Length == 0) continue;

                foreach (int angle in RotationAngles)
                {
                    Quaternion rot = Quaternion.Euler(0f, 0f, angle);
                    Vector3 comOffset = rot * shape.com;

                    for (int ix = 0; ix <= xSteps; ix++)
                    {
                        float x = xMin + ix * scanStep;

                        for (int iy = 0; iy <= ySteps; iy++)
                        {
                            float y = yMin + iy * scanStep;
                            Vector3 pos = new Vector3(x, y, platformCenter.z);

                            // 1) BOŞLUK
                            if (!IsFree(shape.boxes, pos, rot, extentsShrink)) continue;
                            // 2) TABAN (ucuz ön eleme: toplu kutu aşağı inince çarpıyor mu)
                            if (IsFree(shape.boxes, pos + Vector3.down * supportCheckDistance, rot, extentsShrink)) continue;
                            // 3) DENGE: ağırlık merkezi temas aralığının içinde mi
                            float comX = pos.x + comOffset.x;
                            if (!HasStableSupport(shape.boxes, pos, rot, comX)) continue;

                            // 4) TORK
                            float torqueNew = currentTorque + shape.mass * (comX - platformCenter.x);
                            float imbalance = Mathf.Abs(torqueNew) / maxSafeTorque;

                            float depth01 = 1f - Mathf.InverseLerp(yMin, yMax, y);
                            float center01 = 1f - Mathf.Clamp01(Mathf.Abs(comX - platformCenter.x) / halfRangeX);

                            // Denge terimi: 0'a yakınlık (güvenli) ile 1'e yakınlık (tuzak) arasında karışım
                            float capped = Mathf.Min(imbalance, 1f);
                            float balanceTerm = Mathf.Lerp(-capped, capped, aggression);

                            float score = depthWeight * depth01
                                        + centerWeight * center01
                                        + balanceWeight * balanceTerm
                                        - 10f * Mathf.Max(0f, imbalance - 1f)   // güvenli sınırı aşanlar çok kötü
                                        + UnityEngine.Random.Range(-jitter, jitter);
                            accepted++;

                            if (score > best.score)
                            {
                                best.found = true;
                                best.cardIndex = card;
                                best.position = pos;
                                best.rotation = rot;
                                best.rotationDegrees = angle;
                                best.score = score;
                                bestImbalance = imbalance; bestDepth = depth01; bestCenter = center01;
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

            if (best.found)
            {
                best.position = RefinePlacement(shapes[best.cardIndex].boxes, best.position, best.rotation);
                if (logDecision)
                    Debug.Log($"[AIPlanner] Seçim: kart {best.cardIndex}, açı {best.rotationDegrees}, " +
                              $"pos ({best.position.x:F3}, {best.position.y:F3}), puan {best.score:F2} | " +
                              $"derinlik {bestDepth:F2}, merkez {bestCenter:F2}, dengesizlik {bestImbalance:F2} " +
                              $"(1 = güvenli sınır) | mevcut tork {currentTorque:F3}, aday sayısı {accepted}");
            }
            else
            {
                Debug.LogWarning("[AIPlanner] Dengeli bir yer bulunamadı. Collision Mask / platform katmanını kontrol et.");
            }

            onDone?.Invoke(best);
        }

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

        /// <summary>Kuledeki tüm parçaların (kütle x mile yatay uzaklık) toplamı.</summary>
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
        /// Aşağı inince değen kutuların kapladığı X aralığını bulur; parçanın ağırlık merkezi
        /// bu aralığın (kenarlardan supportMarginRatio kadar içerideki) bölümünde mi bakar.
        /// </summary>
        private bool HasStableSupport(LocalBox[] boxes, Vector3 pivotPos, Quaternion pivotRot, float comX)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            int touching = 0;
            Vector3 down = Vector3.down * supportCheckDistance;

            for (int i = 0; i < boxes.Length; i++)
            {
                Vector3 center = pivotPos + pivotRot * boxes[i].center;
                Quaternion orient = pivotRot * boxes[i].rot;

                if (!Physics.CheckBox(center + down, boxes[i].half * extentsShrink, orient, mask, QueryTriggerInteraction.Ignore))
                    continue;

                touching++;
                Matrix4x4 m = Matrix4x4.Rotate(orient);
                Vector3 h = boxes[i].half;
                float halfWorldX = Mathf.Abs(m.m00) * h.x + Mathf.Abs(m.m01) * h.y + Mathf.Abs(m.m02) * h.z;
                minX = Mathf.Min(minX, center.x - halfWorldX);
                maxX = Mathf.Max(maxX, center.x + halfWorldX);
            }

            if (touching == 0) return false;

            float margin = (maxX - minX) * supportMarginRatio;
            return comX >= minX + margin && comX <= maxX - margin;
        }

        private Vector3 RefinePlacement(LocalBox[] boxes, Vector3 pos, Quaternion rot)
        {
            float drop = 0f;
            while (drop < scanStep && IsFree(boxes, pos + Vector3.down * (drop + snapStep), rot, extentsShrink))
                drop += snapStep;
            pos.y -= drop;

            float up = 0f;
            while (up < maxOverlapResolveUp && !IsFree(boxes, pos + Vector3.up * up, rot, 0.995f))
                up += snapStep;
            pos.y += up;
            return pos;
        }

        // ------------------------------------------------------------------ Prefab -> şekil
        private Shape BuildShape(GameObject prefab, float mass)
        {
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

            // Ağırlık merkezi: kutu hacimleriyle ağırlıklı ortalama
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
                mass = Mathf.Max(0.0001f, mass)
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