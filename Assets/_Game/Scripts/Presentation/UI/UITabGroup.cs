using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.UI
{
    public class UITabGroup : MonoBehaviour
    {
        [Header("Registered Tabs")]
        [SerializeField] private List<UITabButton> tabButtons = new List<UITabButton>();
        
        [Tooltip("Oyun açıldığında varsayılan seçili gelecek tab (0 = HostTabButton, 1 = JoinTabButton)")]
        [SerializeField] private int defaultTabIndex = 0;

        private UITabButton activeTab;

        private void Start()
        {
            foreach (var tab in tabButtons)
            {
                if (tab != null)
                {
                    tab.Initialize(OnTabClicked);
                }
            }

            // Varsayılan Tab'ı Seç (Örn: Host Tab)
            if (tabButtons.Count > defaultTabIndex && tabButtons[defaultTabIndex] != null)
            {
                SelectTab(tabButtons[defaultTabIndex]);
            }
        }

        public void SelectTab(UITabButton tabToSelect)
        {
            activeTab = tabToSelect;

            foreach (var tab in tabButtons)
            {
                if (tab != null)
                {
                    bool isSelected = (tab == activeTab);
                    tab.SetSelectedState(isSelected);
                }
            }
        }

        private void OnTabClicked(UITabButton clickedTab)
        {
            SelectTab(clickedTab);
        }
    }
}