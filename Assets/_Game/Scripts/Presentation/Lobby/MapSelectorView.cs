using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Domain.Lobby;

namespace Game.Presentation.Lobby
{
    /// <summary>Host sekmesindeki 4 harita ikonu: açık olan seçilebilir, kilitli olanlar tıklanmaz.</summary>
    public class MapSelectorView : MonoBehaviour
    {
        [Serializable]
        private class Slot
        {
            public Button button;
            public Image icon;
            public GameObject lockIcon;      // kilit resmi
            public GameObject selectedMark;  // tik resmi
        }

        [SerializeField] private MapCatalog catalog;
        [SerializeField] private Slot[] slots;

        public int SelectedIndex { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;                                   // closure için kopya
                MapEntry entry = i < catalog.Count ? catalog.Get(i) : null;
                bool usable = entry != null && entry.unlocked;

                if (entry != null && entry.icon != null) slots[i].icon.sprite = entry.icon;
                slots[i].lockIcon.SetActive(!usable);
                slots[i].button.interactable = usable;
                slots[i].button.onClick.AddListener(() => Select(index));
            }
            Select(0);
        }

        private void Select(int index)
        {
            SelectedIndex = index;
            for (int i = 0; i < slots.Length; i++)
                slots[i].selectedMark.SetActive(i == index);
        }
    }
}
