using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.AI
{
    /// <summary>
    /// AI için "deneme tahtası": platformun ve kulenin fiziksel bir kopyasını ayrı bir fizik sahnesinde kurar,
    /// aday hamleyi orada gerçekten oynatır ve sonucu (çöktü mü? ne kadar sarsıldı? platform ne kadar yattı?) döner.
    /// Ana sahneye hiçbir etkisi yoktur (ayrı PhysicsScene, script yok, görsel yok).
    /// </summary>
    public sealed class PhysicsSandbox : IDisposable
    {
        public struct Result
        {
            public bool survived;          // Kule çökmedi (hiçbir parça kill zone'a / aşağı düşmedi)
            public bool settled;           // Süre dolmadan sakinleşti
            public float duration;         // Çöküşe ya da sakinleşmeye kadar geçen süre
            public float candidateShift;   // Yeni parça planlanan yerden ne kadar oynadı (metre, dönme dahil)
            public float towerDisturbance; // Mevcut parçalardan en çok oynayanın yer değiştirmesi
            public float platformDrift;    // Platformun başlangıca göre dönüşü (derece)
            public float tiltDegrees;      // Platformun düzden (0) mutlak eğimi
            public float tilt01;           // tiltDegrees / menteşe sınırı
            public string fellBody;        // Çöktüyse: kim düştü (teşhis için)
        }

        private sealed class SandboxBody
        {
            public GameObject go;
            public Rigidbody rb;
            public Collider[] colliders;
            public Vector3 startPos;
            public Quaternion startRot;
        }

        private const float SettleSpeed = 0.05f;   // PhysicsSettleDetector.velocityThreshold ile aynı

        private Scene scene;
        private PhysicsScene physics;
        private SandboxBody platform;
        private readonly List<SandboxBody> tower = new List<SandboxBody>();
        private readonly List<SandboxBody> candidates = new List<SandboxBody>();
        private IReadOnlyList<BodyBlueprint> candidateBlueprints;
        private Bounds[] killBounds = Array.Empty<Bounds>();
        private float fallY;
        private float limitAbsDeg = 35f;

        // Simülasyon süreleri (saniye)
        public float MinDuration = 1.5f;
        public float MaxDuration = 6f;
        public float SettleHold = 1.0f;

        public bool IsOperational { get; private set; }

        // ------------------------------------------------------------------ Kurulum
        public static PhysicsSandbox Create(Rigidbody platformBody, HingeJoint liveHinge,
                                            IReadOnlyList<Rigidbody> towerBodies,
                                            IReadOnlyList<BodyBlueprint> candidateBlueprints,
                                            IReadOnlyList<Bounds> killBounds,
                                            IReadOnlyList<Collider> environment, float fallY)
        {
            var sb = new PhysicsSandbox();
            try
            {
                sb.Build(platformBody, liveHinge, towerBodies, candidateBlueprints, killBounds, environment, fallY);
                return sb;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PhysicsSandbox] Kurulamadı, sezgisel moda geçilecek: " + e.Message);
                sb.Dispose();
                return null;
            }
        }

        private void Build(Rigidbody platformBody, HingeJoint liveHinge, IReadOnlyList<Rigidbody> towerBodies,
                           IReadOnlyList<BodyBlueprint> blueprints, IReadOnlyList<Bounds> kill,
                           IReadOnlyList<Collider> environment, float fallLimitY)
        {
            scene = SceneManager.CreateScene("AI_Sandbox", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
            candidateBlueprints = blueprints;
            fallY = fallLimitY;

            killBounds = new Bounds[kill.Count];
            for (int i = 0; i < kill.Count; i++) killBounds[i] = kill[i];

            // Platform: menteşe, "dinlenme" pozunda (dünya rotasyonu = 0) kurulur; açı ve limitler
            // sahne başlangıcındaki gibi 0 derece çevresinde olsun diye. Sonra canlı poza ışınlanır.
            Vector3 liveRootPos = platformBody.transform.position;
            Quaternion liveRootRot = platformBody.transform.rotation;

            BodyBlueprint pbp = BodyBlueprint.Capture(platformBody);
            platform = MakeBody(pbp, "Platform", liveRootPos, Quaternion.identity);

            if (liveHinge != null)
            {
                var h = platform.go.AddComponent<HingeJoint>();
                h.autoConfigureConnectedAnchor = false;
                h.anchor = liveHinge.anchor;
                h.axis = liveHinge.axis;
                // Bağlı gövde yoksa connectedAnchor DÜNYA koordinatıdır (canlı eklemle aynı nokta).
                h.connectedAnchor = liveHinge.connectedBody == null
                    ? liveHinge.connectedAnchor
                    : platform.go.transform.TransformPoint(liveHinge.anchor);
                h.useLimits = liveHinge.useLimits;
                JointLimits lim = liveHinge.limits;
                limitAbsDeg = Mathf.Max(Mathf.Abs(lim.min), Mathf.Abs(lim.max), 1f);
                h.limits = lim;
                h.useSpring = false;
                h.useMotor = false;
            }
            else
            {
                platform.rb.isKinematic = true;   // menteşe yoksa platform sabit
            }

            // Eklem kurulduktan SONRA platformu canlı eğimine döndür
            platform.startPos = liveRootPos;
            platform.startRot = liveRootRot;
            ResetBody(platform);
            Physics.SyncTransforms();

            // Ortam: masa, tabak vb. sabit colliderlar (platform bunlara dayanıp durabiliyor)
            int envCount = 0;
            for (int i = 0; i < environment.Count; i++)
                if (environment[i] != null && AddStatic(environment[i])) envCount++;

            // Kule
            for (int i = 0; i < towerBodies.Count; i++)
            {
                Rigidbody rb = towerBodies[i];
                BodyBlueprint bp = BodyBlueprint.Capture(rb);
                if (!bp.HasColliders) continue;
                tower.Add(MakeBody(bp, "Piece" + i, rb.transform.position, rb.transform.rotation));
            }

            // Aday gövdeler (kart başına bir tane, kapalı bekler)
            for (int i = 0; i < blueprints.Count; i++)
            {
                BodyBlueprint bp = blueprints[i];
                SandboxBody b = bp != null && bp.HasColliders
                    ? MakeBody(bp, "Candidate" + i, new Vector3(0f, 500f, 0f), Quaternion.identity)
                    : null;
                if (b != null) b.go.SetActive(false);
                candidates.Add(b);
            }

            Debug.Log($"[PhysicsSandbox] Kuruldu: platform '{platformBody.name}' kütle {platform.rb.mass}, COM {platform.rb.centerOfMass}, " +
                      $"menteşe {(liveHinge != null ? "var, limit ±" + limitAbsDeg : "YOK")}, canlı eğim {Mathf.DeltaAngle(0f, liveRootRot.eulerAngles.z):F1}°, " +
                      $"kule {tower.Count} parça, ortam {envCount} collider, kill zone {killBounds.Length}");
            IsOperational = SelfTest(platformBody.transform.position);
            if (!IsOperational)
                Debug.LogWarning("[PhysicsSandbox] Self-test başarısız: PhysicsScene.Simulate gövdeleri hareket ettirmedi.");
        }

        /// <summary>Sabit bir collider'ı (masa gibi) sandbox'a rigidbody'siz kopyalar.</summary>
        private bool AddStatic(Collider src)
        {
            var go = new GameObject("env_" + src.name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = src.gameObject.layer;
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            go.transform.localScale = src.transform.lossyScale;

            Collider copy;
            switch (src)
            {
                case BoxCollider b:
                    var nb = go.AddComponent<BoxCollider>();
                    nb.center = b.center; nb.size = b.size; copy = nb;
                    break;
                case SphereCollider sp:
                    var ns = go.AddComponent<SphereCollider>();
                    ns.center = sp.center; ns.radius = sp.radius; copy = ns;
                    break;
                case CapsuleCollider cp:
                    var nc = go.AddComponent<CapsuleCollider>();
                    nc.center = cp.center; nc.radius = cp.radius; nc.height = cp.height; nc.direction = cp.direction; copy = nc;
                    break;
                case MeshCollider mc:
                    var nm = go.AddComponent<MeshCollider>();
                    nm.convex = mc.convex; nm.sharedMesh = mc.sharedMesh; copy = nm;
                    break;
                default:
                    UnityEngine.Object.Destroy(go);
                    return false;
            }
            copy.sharedMaterial = src.sharedMaterial;
            return true;
        }

        private SandboxBody MakeBody(BodyBlueprint bp, string name, Vector3 pos, Quaternion rot)
        {
            GameObject go = bp.Spawn(scene, name, pos, rot);
            return new SandboxBody
            {
                go = go,
                rb = go.GetComponent<Rigidbody>(),
                colliders = go.GetComponentsInChildren<Collider>(),
                startPos = pos,
                startRot = rot
            };
        }

        /// <summary>Havaya bir küre bırakır; yerçekimiyle düşüyorsa sandbox gerçekten simüle ediyordur.</summary>
        private bool SelfTest(Vector3 around)
        {
            var probe = new GameObject("probe");
            SceneManager.MoveGameObjectToScene(probe, scene);
            probe.transform.position = around + new Vector3(0f, 20f, 8f);
            probe.AddComponent<SphereCollider>().radius = 0.05f;
            var rb = probe.AddComponent<Rigidbody>();
            Physics.SyncTransforms();

            float y0 = rb.position.y;
            for (int i = 0; i < 10; i++) physics.Simulate(Time.fixedDeltaTime);
            bool ok = y0 - rb.position.y > 0.02f;

            UnityEngine.Object.DestroyImmediate(probe);
            return ok;
        }

        // ------------------------------------------------------------------ Simülasyon
        /// <summary>
        /// Dünyayı başlangıç haline sıfırlar, (varsa) adayı koyar ve sakinleşene / çökene kadar oynatır.
        /// card = -1 -> aday yok (kalibrasyon testi). Kare başına frameBudgetMs kadar çalışıp bekler.
        /// </summary>
        public IEnumerator Run(int card, Vector3 rootPos, Quaternion rootRot, Action<Result> onDone, float frameBudgetMs)
        {
            ResetWorld();

            SandboxBody cand = null;
            Vector3 candStart = default;
            Quaternion candStartRot = Quaternion.identity;
            float radius = 0.1f;

            if (card >= 0 && card < candidates.Count && candidates[card] != null)
            {
                cand = candidates[card];
                candidateBlueprints[card].ComputeBodyPose(rootPos, rootRot, out candStart, out candStartRot);
                radius = candidateBlueprints[card].Radius;
                cand.go.transform.SetPositionAndRotation(candStart, candStartRot);
                cand.go.SetActive(true);
                cand.rb.position = candStart;
                cand.rb.rotation = candStartRot;
                cand.rb.linearVelocity = Vector3.zero;
                cand.rb.angularVelocity = Vector3.zero;
                cand.rb.WakeUp();
            }
            Physics.SyncTransforms();

            float startZ = Mathf.DeltaAngle(0f, platform.startRot.eulerAngles.z);
            float dt = Time.fixedDeltaTime;
            float t = 0f, calm = 0f;
            bool fell = false, settled = false;
            string fellName = null;
            float sliceStart = Time.realtimeSinceStartup;

            while (t < MaxDuration)
            {
                physics.Simulate(dt);
                t += dt;

                if (HasFallen(cand, out fellName)) { fell = true; break; }

                calm = AllCalm(cand) ? calm + dt : 0f;
                if (t >= MinDuration && calm >= SettleHold) { settled = true; break; }

                if ((Time.realtimeSinceStartup - sliceStart) * 1000f > frameBudgetMs)
                {
                    yield return null;
                    sliceStart = Time.realtimeSinceStartup;
                }
            }

            var result = new Result { survived = !fell, settled = settled, duration = t, fellBody = fellName };

            float disturb = 0f;
            foreach (SandboxBody b in tower)
                disturb = Mathf.Max(disturb, (b.rb.position - b.startPos).magnitude);
            result.towerDisturbance = disturb;

            if (cand != null)
            {
                float angleRad = Quaternion.Angle(candStartRot, cand.rb.rotation) * Mathf.Deg2Rad;
                result.candidateShift = (cand.rb.position - candStart).magnitude + angleRad * radius;
                cand.go.SetActive(false);
            }

            float endZ = Mathf.DeltaAngle(0f, platform.rb.rotation.eulerAngles.z);
            result.platformDrift = Mathf.Abs(Mathf.DeltaAngle(startZ, endZ));
            result.tiltDegrees = Mathf.Abs(endZ);
            result.tilt01 = Mathf.Clamp01(result.tiltDegrees / limitAbsDeg);

            onDone?.Invoke(result);
        }

        private void ResetWorld()
        {
            foreach (SandboxBody c in candidates) if (c != null) c.go.SetActive(false);
            ResetBody(platform);
            foreach (SandboxBody b in tower) ResetBody(b);
            Physics.SyncTransforms();
        }

        private static void ResetBody(SandboxBody b)
        {
            b.go.transform.SetPositionAndRotation(b.startPos, b.startRot);
            b.rb.position = b.startPos;
            b.rb.rotation = b.startRot;
            if (!b.rb.isKinematic)
            {
                b.rb.linearVelocity = Vector3.zero;
                b.rb.angularVelocity = Vector3.zero;
                b.rb.WakeUp();
            }
        }

        private bool HasFallen(SandboxBody cand, out string who)
        {
            foreach (SandboxBody b in tower) if (IsOut(b)) { who = b.go.name; return true; }
            if (cand != null && IsOut(cand)) { who = cand.go.name; return true; }
            who = null;
            return false;
        }

        private bool IsOut(SandboxBody b)
        {
            if (b.rb.worldCenterOfMass.y < fallY) return true;
            if (killBounds.Length == 0) return false;
            foreach (Collider c in b.colliders)
            {
                Bounds cb = c.bounds;
                for (int i = 0; i < killBounds.Length; i++)
                    if (killBounds[i].Intersects(cb)) return true;
            }
            return false;
        }

        private bool AllCalm(SandboxBody cand)
        {
            if (!IsCalm(platform)) return false;
            foreach (SandboxBody b in tower) if (!IsCalm(b)) return false;
            return cand == null || IsCalm(cand);
        }

        private static bool IsCalm(SandboxBody b)
        {
            Rigidbody rb = b.rb;
            if (rb.isKinematic || rb.IsSleeping()) return true;
            float sq = SettleSpeed * SettleSpeed;
            return rb.linearVelocity.sqrMagnitude <= sq && rb.angularVelocity.sqrMagnitude <= sq;
        }

        // ------------------------------------------------------------------ Temizlik
        public void Dispose()
        {
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            platform = null;
            tower.Clear();
            candidates.Clear();
            IsOperational = false;
        }
    }
}