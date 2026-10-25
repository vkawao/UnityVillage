using UnityEngine;

namespace NarinVillage
{
    /// <summary>
    /// Flickering and buzzing fluorescent ceiling light in the Backrooms (House 5, Action 8).
    /// </summary>
    public class BackroomsFlickerLight : MonoBehaviour
    {
        public Light pointLight;
        public bool isFlickeringLight = false;
        private AudioSource _buzzAudio;
        private float _baseIntensity = 2.8f;
        private float _timer;
        private bool _recordedAction8 = false;

        private void Start()
        {
            if (pointLight == null) pointLight = GetComponentInChildren<Light>();
            if (pointLight != null) _baseIntensity = pointLight.intensity;

            if (isFlickeringLight)
            {
                _buzzAudio = gameObject.AddComponent<AudioSource>();
                _buzzAudio.clip = ProceduralAudioGenerator.CreateFluorescentBuzz(3.0f);
                _buzzAudio.loop = true;
                _buzzAudio.spatialBlend = 0.9f;
                _buzzAudio.maxDistance = 10f;
                _buzzAudio.volume = 0.20f;
                _buzzAudio.Play();
            }
        }

        private void Update()
        {
            if (isFlickeringLight && pointLight != null)
            {
                _timer += Time.deltaTime;
                if (_timer > 0.08f)
                {
                    _timer = 0f;
                    float r = Random.value;
                    if (r < 0.12f)
                        pointLight.intensity = _baseIntensity * Random.Range(0.2f, 0.55f);
                    else
                        pointLight.intensity = _baseIntensity;
                }
            }

            if (!_recordedAction8 && Camera.main != null)
            {
                if (Vector3.Distance(transform.position, Camera.main.transform.position) < 6.0f)
                {
                    _recordedAction8 = true;
                    if (VillageActionManager.Instance != null)
                        VillageActionManager.Instance.CompleteAction(5, 8);
                }
            }
        }
    }
}
