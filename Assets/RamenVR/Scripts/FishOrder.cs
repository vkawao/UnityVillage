using UnityEngine;
using UnityEngine.UI;

namespace RamenVR
{
    /// The customer's order at the fish shop: how many fish, their weight and the price.
    /// Shows it on the order panel, the scale screen and the pay panel.
    public class FishOrder : MonoBehaviour
    {
        public int count = 1;
        public int maxCount = 5;
        public int pricePerKg = 120;
        public Text orderText;     // order panel
        public Text scaleText;     // scale screen
        public Text payText;       // pay panel

        float[] weights;
        bool locked;               // can't change after TAKE ORDER

        void Awake()
        {
            weights = new float[maxCount];
            for (int i = 0; i < maxCount; i++) weights[i] = Random.Range(1.6f, 2.9f);
            Refresh();
        }

        public void More() { if (!locked && count < maxCount) { count++; Refresh(); } }
        public void Less() { if (!locked && count > 1) { count--; Refresh(); } }
        public void Lock() { locked = true; }

        public float TotalKg
        {
            get { float kg = 0; for (int i = 0; i < count; i++) kg += weights[i]; return kg; }
        }

        public int TotalPrice => Mathf.RoundToInt(TotalKg * pricePerKg);

        void Refresh()
        {
            if (orderText) orderText.text = $"Fish:  {count}\n(about {pricePerKg} B / kg)";
            if (scaleText) scaleText.text = $"{count} fish  {TotalKg:0.0} kg\n{TotalPrice} B";
            if (payText) payText.text = $"{count} fish   {TotalKg:0.0} kg\nTOTAL  {TotalPrice} B";
        }

        /// called by the WEIGH and GET PAID buttons so the numbers are always up to date
        public void ShowNumbers() => Refresh();
    }
}
