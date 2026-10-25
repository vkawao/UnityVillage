using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRVillage
{
    /// <summary>
    /// Permanent cashier screen at the counter: lists what's on the counter with prices
    /// and a total, and has a Purchase button that only the purchase step enables.
    /// </summary>
    public class CashierUI : MonoBehaviour
    {
        [SerializeField] private CounterZone m_Zone;
        [SerializeField] private TMP_Text m_ItemsText;
        [SerializeField] private TMP_Text m_TotalText;
        [SerializeField] private Button m_PurchaseButton;
        [SerializeField] private Image m_PurchaseImage;
        [SerializeField] private TMP_Text m_PurchaseLabel;

        [SerializeField] private Color m_PurchaseOn = new Color(0.16f, 0.65f, 0.3f);
        [SerializeField] private Color m_PurchaseOff = new Color(0.3f, 0.3f, 0.33f);

        private bool m_ThankedYou;

        /// <summary>Raised when the (enabled) Purchase button is pressed.</summary>
        public event Action Purchased;

        public CounterZone Zone => m_Zone;

        private void Awake()
        {
            if (m_PurchaseButton != null) m_PurchaseButton.onClick.AddListener(OnPurchaseClicked);
            SetPurchaseEnabled(false);
        }

        private void OnEnable()
        {
            if (m_Zone != null) m_Zone.ItemsChanged += OnItemsChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (m_Zone != null) m_Zone.ItemsChanged -= OnItemsChanged;
        }

        public void SetPurchaseEnabled(bool enabled)
        {
            if (m_PurchaseButton != null) m_PurchaseButton.interactable = enabled;
            if (m_PurchaseImage != null) m_PurchaseImage.color = enabled ? m_PurchaseOn : m_PurchaseOff;
            if (m_PurchaseLabel != null) m_PurchaseLabel.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        /// <summary>Switch the screen to a permanent thank-you message.</summary>
        public void ShowThankYou(int paid)
        {
            m_ThankedYou = true;
            if (m_ItemsText != null)
                m_ItemsText.text = "<size=130%><b>Thank you!</b></size>\nEnjoy your new gear. Rock on!";
            if (m_TotalText != null) m_TotalText.text = $"Paid  <b>${Money(paid)}</b>";
            if (m_PurchaseLabel != null) m_PurchaseLabel.text = "Paid";
            SetPurchaseEnabled(false);
        }

        private void OnPurchaseClicked() => Purchased?.Invoke();

        private void OnItemsChanged(CounterZone zone) => Refresh();

        private void Refresh()
        {
            if (m_ThankedYou || m_ItemsText == null) return;

            var items = m_Zone != null ? m_Zone.Items : null;
            if (items == null || items.Count == 0)
            {
                m_ItemsText.text = "<color=#8a8a92>Place items on the counter</color>";
                if (m_TotalText != null) m_TotalText.text = "Total  <b>$0</b>";
                return;
            }

            var sb = new StringBuilder();
            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(items[i].DisplayName).Append("<pos=72%>$").Append(Money(items[i].Price));
                total += items[i].Price;
            }
            m_ItemsText.text = sb.ToString();
            if (m_TotalText != null) m_TotalText.text = $"Total  <b>${Money(total)}</b>";
        }

        private static string Money(int amount) => amount.ToString("N0", CultureInfo.InvariantCulture);
    }
}
