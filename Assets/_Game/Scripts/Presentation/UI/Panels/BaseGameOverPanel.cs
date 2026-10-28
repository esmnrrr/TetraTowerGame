using UnityEngine;
using Game.Domain.Turns;

namespace Game.Presentation.UI.Panels
{
    public abstract class BaseGameOverPanel : MonoBehaviour
    {
        // Alt sınıflar (stratejiler) kendi UI'larını bu metoda göre dolduracak
        public abstract void ShowResult(GameResultData data);

        public virtual void HidePanel()
        {
            gameObject.SetActive(false);
        }
    }
}