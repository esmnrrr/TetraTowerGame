using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Presentation.UI
{
    /// "YOUR TURN" / "WAIT..." etiketi. Sadece gösterir, oyun kuralını bilmez.
    public class TurnIndicatorView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string myTurnText = "YOUR TURN";
        [SerializeField] private string waitText = "WAIT...";
        [SerializeField] private Color myTurnColor = new Color32(46, 204, 113, 255);
        [SerializeField] private Color waitColor = new Color32(190, 190, 190, 255);

        private bool? lastState;

        public void Show(bool isMyTurn)
        {
            gameObject.SetActive(true);
            label.text = isMyTurn ? myTurnText : waitText;
            label.color = isMyTurn ? myTurnColor : waitColor;

            // Aynı durum tekrar gelirse animasyonu tekrar oynatma
            if (lastState == isMyTurn) return;
            lastState = isMyTurn;

            StopAllCoroutines();
            StartCoroutine(Pop());
        }

        public void Hide()
        {
            lastState = null;
            gameObject.SetActive(false);
        }

        private IEnumerator Pop()
        {
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                float s = 1f + 0.25f * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI);
                transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            transform.localScale = Vector3.one;
        }
    }
}