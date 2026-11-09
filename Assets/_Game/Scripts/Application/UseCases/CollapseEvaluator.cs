using UnityEngine;
using Game.Domain.Turns;
using System.Collections.Generic;
using Unity.Netcode;

namespace Game.Application.UseCases
{
    public class CollapseEvaluator : MonoBehaviour
    {
        [Header("Practice Mode Settings")]
        [Tooltip("Practice/Pathway modunda kulesinin çökmesi için kaç parçanın düşmesi gerektiği")]
        public int practiceAllowedDrops = 2;

        private int currentDrops = 0;
        public bool HasTowerCollapsed { get; private set; } = false;
        private readonly HashSet<int> countedBodies = new HashSet<int>();


        private void Start()
        {
            ResetEvaluator();
        }

        /// <summary>
        /// Yeni maç başladığında sayacı ve durumu sıfırlar.
        /// </summary>
        public void ResetEvaluator()
        {
            currentDrops = 0;
            HasTowerCollapsed = false;
            countedBodies.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Piece")) return; 

            // Multiplayer'da çöküş kararını sadece SUNUCU verir
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer) return;

            // Çok colliderlı parça tek parça sayılsın
            GameObject body = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.transform.root.gameObject;
            if (!countedBodies.Add(body.GetInstanceID())) return;

            currentDrops++;
            Debug.Log($"[KillZone] Düşen: {other.transform.root.name} / {other.name} | Toplam: {currentDrops}");

            // 1. LevelManager'a düşen parçayı bildir (Yıldız hesabı için)
            var levelManager = FindAnyObjectByType<LevelManager>();
            if (levelManager != null)
            {
                levelManager.RegisterPieceDropped();
            }

            // 2. Aktif Oyun Modunu TurnCoordinator'dan Öğren
            var coordinator = FindAnyObjectByType<TurnCoordinator>();
            // if (coordinator != null && coordinator.currentMode == GameMode.Multiplayer &&
            // coordinator.currentState != TurnState.Simulating &&
            // coordinator.currentState != TurnState.Evaluating)
            // return;   // parça bırakılmadan "düştü" sayma
            GameMode currentMode = coordinator != null ? coordinator.currentMode : GameMode.Practice;

            // 3. MODA GÖRE ÇÖKÜŞ KONTROLÜ
            if (currentMode == GameMode.VsAI || currentMode == GameMode.Multiplayer)
            {
                // VS AI ve Multiplayer modunda 1 TEK PARÇA bile düşerse KULE ANINDA ÇÖKER!
                HasTowerCollapsed = true;
                Debug.Log($"[KillZone] {currentMode} modunda parça düştü! OYUN BİTTİ.");
            }
            else
            {
                // Practice / Pathway modunda belirlenen düşüş toleransı geçerlidir
                if (currentDrops > practiceAllowedDrops)
                {
                    HasTowerCollapsed = true;
                    Debug.Log($"[KillZone] Practice sınır aşıldı! Düşen: {currentDrops} > İzin Verilen: {practiceAllowedDrops}");
                }
            }
        }
    }
}