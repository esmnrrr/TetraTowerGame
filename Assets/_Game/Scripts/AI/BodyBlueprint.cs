using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.AI
{
    /// <summary>
    /// Bir Rigidbody'nin "fizik kimlik kartı": collider'lar (gövdeye göre) + rigidbody ayarları.
    /// Script ve görsel kopyalamadan, sandbox fizik sahnesinde aynı fiziksel gövdeyi yeniden kurmak için kullanılır.
    /// (Instantiate kullanmayız: parçadaki script'ler çalışıp oyunu etkilemesin.)
    /// </summary>
    public sealed class BodyBlueprint
    {
        private enum Kind { Box, Sphere, Capsule, Mesh }

        private struct ColliderSpec
        {
            public Kind kind;
            public Vector3 localPos;
            public Quaternion localRot;
            public Vector3 localScale;
            public int layer;
            public float contactOffset;
            public PhysicsMaterial material;

            public Vector3 center;     // Box / Sphere / Capsule
            public Vector3 size;       // Box
            public float radius;       // Sphere / Capsule
            public float height;       // Capsule
            public int direction;      // Capsule
            public Mesh mesh;          // Mesh
            public bool convex;        // Mesh
        }

        private readonly List<ColliderSpec> colliders = new List<ColliderSpec>();

        private float mass = 1f, linearDamping, angularDamping, maxAngularVelocity, maxDepenetrationVelocity, sleepThreshold;
        private bool useGravity = true, isKinematic, customCom, customTensor;
        private RigidbodyConstraints constraints;
        private CollisionDetectionMode detection;
        private Vector3 com, tensor;
        private Quaternion tensorRot = Quaternion.identity;
        private int solverIterations = 6, solverVelocityIterations = 1;

        // Prefab kökünden gövdeye dönüşüm (rigidbody kökte değilse)
        private Matrix4x4 rootToBody = Matrix4x4.identity;
        private Vector3 rootScale = Vector3.one;

        /// <summary>Gövde orijininden en uzak noktaya yaklaşık mesafe (dönmeyi mesafeye çevirmek için).</summary>
        public float Radius { get; private set; } = 0.1f;
        public bool HasColliders => colliders.Count > 0;

        // ------------------------------------------------------------------ Yakalama
        /// <param name="rb">Kopyalanacak rigidbody (sahnedeki canlı obje ya da prefab).</param>
        /// <param name="root">Prefab için: yerleştirme pozu bu kökün pozudur. Canlı objede null.</param>
        public static BodyBlueprint Capture(Rigidbody rb, Transform root = null,
                                            PhysicsMaterial materialOverride = null, float massOverride = 0f)
        {
            var bp = new BodyBlueprint();
            Transform bt = rb.transform;
            Matrix4x4 bodyInv = Matrix4x4.TRS(bt.position, bt.rotation, Vector3.one).inverse;

            foreach (Collider col in rb.GetComponentsInChildren<Collider>())
            {
                if (!col.enabled || col.isTrigger) continue;
                if (col.GetComponentInParent<Rigidbody>() != rb) continue; // başka bir gövdenin collider'ı

                Matrix4x4 m = bodyInv * col.transform.localToWorldMatrix;
                var spec = new ColliderSpec
                {
                    localPos = m.GetColumn(3),
                    localRot = m.rotation,
                    localScale = m.lossyScale,
                    layer = col.gameObject.layer,
                    contactOffset = col.contactOffset,
                    material = materialOverride != null ? materialOverride : col.sharedMaterial
                };

                float reach = 0.05f;
                switch (col)
                {
                    case BoxCollider b:
                        spec.kind = Kind.Box; spec.center = b.center; spec.size = b.size;
                        reach = 0.5f * Vector3.Scale(b.size, spec.localScale).magnitude;
                        break;
                    case SphereCollider s:
                        spec.kind = Kind.Sphere; spec.center = s.center; spec.radius = s.radius;
                        break;
                    case CapsuleCollider c:
                        spec.kind = Kind.Capsule; spec.center = c.center; spec.radius = c.radius;
                        spec.height = c.height; spec.direction = c.direction;
                        break;
                    case MeshCollider mc:
                        spec.kind = Kind.Mesh; spec.mesh = mc.sharedMesh; spec.convex = mc.convex;
                        break;
                    default:
                        continue;
                }

                bp.Radius = Mathf.Max(bp.Radius, spec.localPos.magnitude + reach);
                bp.colliders.Add(spec);
            }

            bp.mass = massOverride > 0f ? massOverride : rb.mass;
            bp.linearDamping = rb.linearDamping;
            bp.angularDamping = rb.angularDamping;
            bp.useGravity = rb.useGravity;
            bp.isKinematic = rb.isKinematic;
            bp.constraints = rb.constraints;
            bp.detection = rb.collisionDetectionMode;
            bp.maxAngularVelocity = rb.maxAngularVelocity;
            bp.maxDepenetrationVelocity = rb.maxDepenetrationVelocity;
            bp.sleepThreshold = rb.sleepThreshold;
            bp.solverIterations = rb.solverIterations;
            bp.solverVelocityIterations = rb.solverVelocityIterations;
            bp.customCom = !rb.automaticCenterOfMass;
            bp.com = rb.centerOfMass;
            bp.customTensor = !rb.automaticInertiaTensor;
            bp.tensor = rb.inertiaTensor;
            bp.tensorRot = rb.inertiaTensorRotation;

            if (root != null)
            {
                bp.rootToBody = root.worldToLocalMatrix * bt.localToWorldMatrix;
                bp.rootScale = root.lossyScale;
            }
            return bp;
        }

        /// <summary>Prefab kökünü (pos, rot) noktasına koyunca gövdenin (rigidbody) dünya pozu ne olur?</summary>
        public void ComputeBodyPose(Vector3 rootPos, Quaternion rootRot, out Vector3 bodyPos, out Quaternion bodyRot)
        {
            Matrix4x4 m = Matrix4x4.TRS(rootPos, rootRot, rootScale) * rootToBody;
            bodyPos = m.GetColumn(3);
            bodyRot = m.rotation;
        }

        // ------------------------------------------------------------------ Kurulum
        /// <summary>Sandbox sahnesinde (script'siz, görselsiz) fiziksel kopyayı üretir.</summary>
        public GameObject Spawn(Scene scene, string name, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(position, rotation);

            foreach (ColliderSpec s in colliders)
            {
                var child = new GameObject("col");
                child.layer = s.layer;
                child.transform.SetParent(go.transform, false);
                child.transform.localPosition = s.localPos;
                child.transform.localRotation = s.localRot;
                child.transform.localScale = s.localScale;

                Collider c;
                switch (s.kind)
                {
                    case Kind.Box:
                        var box = child.AddComponent<BoxCollider>();
                        box.center = s.center; box.size = s.size; c = box;
                        break;
                    case Kind.Sphere:
                        var sph = child.AddComponent<SphereCollider>();
                        sph.center = s.center; sph.radius = s.radius; c = sph;
                        break;
                    case Kind.Capsule:
                        var cap = child.AddComponent<CapsuleCollider>();
                        cap.center = s.center; cap.radius = s.radius; cap.height = s.height; cap.direction = s.direction; c = cap;
                        break;
                    default:
                        var mc = child.AddComponent<MeshCollider>();
                        mc.convex = s.convex;
                        mc.sharedMesh = s.mesh;
                        c = mc;
                        break;
                }
                c.sharedMaterial = s.material;
                c.contactOffset = s.contactOffset;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.useGravity = useGravity;
            rb.constraints = constraints;
            rb.collisionDetectionMode = detection;
            rb.maxAngularVelocity = maxAngularVelocity;
            rb.maxDepenetrationVelocity = maxDepenetrationVelocity;
            rb.sleepThreshold = sleepThreshold;
            rb.solverIterations = solverIterations;
            rb.solverVelocityIterations = solverVelocityIterations;
            if (customCom) rb.centerOfMass = com;
            if (customTensor) { rb.inertiaTensor = tensor; rb.inertiaTensorRotation = tensorRot; }
            rb.isKinematic = isKinematic;
            return go;
        }
    }
}