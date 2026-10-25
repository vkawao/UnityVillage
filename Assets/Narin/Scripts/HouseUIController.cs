using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace NarinVillage
{
    /// <summary>
    /// Handles all World-Space Canvas UI interactions across the 5 houses:
    /// Storage Lock/Unlock, Boombox Radio, Shop Cash Register, Mechanic Horn/Engine, and Flashlight UI Switch.
    /// </summary>
    public class HouseUIController : MonoBehaviour
    {
        [Header("House 1: Storage Unit References")]
        public ScriptedMover storageMainDoor;
        public Text storageStatusLabel;

        [Header("House 2: Boombox References")]
        public AudioSource boomboxAudioSource;
        public Text boomboxStatusLabel;
        private bool _boomboxPlaying = false;
        private int _currentTrack = 0;
        private AudioClip[] _tracks;

        [Header("House 3: Shop Register References")]
        public Text registerStatusLabel;
        public ScriptedMover cashRegisterDrawer;
        private float _runningTotal = 0f;
        private AudioSource _registerAudio;

        [Header("House 3: Radio Music Tracks")]
        [Tooltip("Optional custom radio songs/tracks for House 3 Boombox. If empty, uses procedural retro synth loops.")]
        public AudioClip[] customRadioTracks;

        [Header("House 4: Mechanic Shop References")]
        public AudioSource carAudioSource;
        public Text mechanicStatusLabel;
        [Tooltip("Drag & drop custom car horn AudioClip here. If empty, uses procedural horn.")]
        public AudioClip customHornClip;
        [Tooltip("Drag & drop custom engine rev AudioClip here. If empty, uses procedural engine rev.")]
        public AudioClip customEngineRevClip;

        [Header("House 5: Flashlight References")]
        public Light flashlightBeam;
        public Text flashlightStatusLabel;

        private void Awake()
        {
            if (customRadioTracks != null && customRadioTracks.Length > 0)
            {
                _tracks = customRadioTracks;
            }
            else
            {
                _tracks = new AudioClip[]
                {
                    ProceduralAudioGenerator.CreateRadioLoop(0),
                    ProceduralAudioGenerator.CreateRadioLoop(1)
                };
            }
        }

        private void Start()
        {
            Canvas c = GetComponent<Canvas>();
            if (c != null && c.worldCamera == null && Camera.main != null)
            {
                c.worldCamera = Camera.main;
            }
            EnsureStorageLabels();
            EnsureShopLabels();
            EnsureMechanicLabels();
        }

        private void EnsureStorageLabels()
        {
            if (storageStatusLabel == null)
            {
                foreach (Text t in GetComponentsInChildren<Text>(true))
                {
                    if (t.gameObject.name.Contains("Status")) { storageStatusLabel = t; break; }
                }
            }
        }

        private void EnsureShopLabels()
        {
            if (registerStatusLabel == null)
            {
                foreach (Text t in GetComponentsInChildren<Text>(true))
                {
                    if (t.gameObject.name.Contains("Status") || t.gameObject.name.Contains("RegStatus")) { registerStatusLabel = t; break; }
                }
            }
            if (cashRegisterDrawer == null)
            {
                foreach (var m in FindObjectsByType<ScriptedMover>(FindObjectsSortMode.None))
                {
                    if (m != null && m.houseNumber == 3 && (m.actionNumber == 9 || m.name.IndexOf("Register", System.StringComparison.OrdinalIgnoreCase) >= 0 || m.name.IndexOf("Drawer", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        cashRegisterDrawer = m;
                        break;
                    }
                }
            }

            if (cashRegisterDrawer == null)
            {
                EnsureCashRegisterDrawerCreated();
            }
        }

        public void EnsureCashRegisterDrawerCreated()
        {
            if (cashRegisterDrawer != null) return;

            GameObject regGO = null;
            foreach (var go in NarinScope.AllObjects())
            {
                if (go != null && go.scene.name != null && go.name.Equals("Cash Register", System.StringComparison.OrdinalIgnoreCase))
                {
                    regGO = go;
                    break;
                }
            }

            if (regGO == null)
                regGO = NarinScope.Find("Cash Register");

            if (regGO != null)
            {
                Transform existingDrawer = regGO.transform.Find("Cash_Register_Drawer");
                GameObject drawerGO;
                if (existingDrawer != null)
                {
                    drawerGO = existingDrawer.gameObject;
                }
                else
                {
                    drawerGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    drawerGO.name = "Cash_Register_Drawer";
                    drawerGO.transform.SetParent(regGO.transform, false);
                    drawerGO.transform.localPosition = new Vector3(0f, 0f, -0.2f);
                    drawerGO.transform.localRotation = Quaternion.identity;
                    drawerGO.transform.localScale = new Vector3(0.85f, 0.85f, 0.35f);

                    Renderer r = drawerGO.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.material.color = new Color(0.22f, 0.24f, 0.28f);
                    }
                }

                ScriptedMover sm = drawerGO.GetComponent<ScriptedMover>();
                if (sm == null)
                {
                    sm = drawerGO.AddComponent<ScriptedMover>();
                    sm.houseNumber = 3;
                    sm.actionNumber = 9;
                    sm.openPositionOffset = new Vector3(-0.65f, 0f, 0f);
                    sm.speed = 3.5f;
                }
                cashRegisterDrawer = sm;
            }
        }

        private void EnsureMechanicLabels()
        {
            if (mechanicStatusLabel == null)
            {
                foreach (Text t in GetComponentsInChildren<Text>(true))
                {
                    if (t.gameObject.name.Contains("Status") || t.gameObject.name.Contains("MechStatus")) { mechanicStatusLabel = t; break; }
                }
            }
        }

        private void PlayRegisterBeep()
        {
            if (_registerAudio == null)
            {
                _registerAudio = gameObject.AddComponent<AudioSource>();
                _registerAudio.spatialBlend = 0f;
            }
            _registerAudio.PlayOneShot(ProceduralAudioGenerator.CreateChime(880f, 1174.66f, 0.25f), 0.7f);
        }

        private void PlayCashRegisterSound()
        {
            if (_registerAudio == null)
            {
                _registerAudio = gameObject.AddComponent<AudioSource>();
                _registerAudio.spatialBlend = 0f;
            }
            _registerAudio.PlayOneShot(ProceduralAudioGenerator.CreateCashRegisterSound(), 0.95f);
        }

        // --- HOUSE 1: STORAGE UNIT UI ---
        public void OnStorageLockDoor()
        {
            EnsureStorageLabels();
            AudioSource.PlayClipAtPoint(ProceduralAudioGenerator.CreateLockLatchSound(), transform.position, 0.95f);

            var movers = Resources.FindObjectsOfTypeAll<ScriptedMover>();
            foreach (var mover in movers)
            {
                if (mover != null && mover.gameObject.scene.name != null && mover.houseNumber == 1)
                {
                    mover.isLocked = true;
                    mover.SetOpen(false);
                }
            }
            if (storageMainDoor != null)
            {
                storageMainDoor.isLocked = true;
                storageMainDoor.SetOpen(false);
            }
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(1, 9);

            if (storageStatusLabel != null)
                storageStatusLabel.text = "UNIT STATUS: <color=#FF4444>LOCKED</color>";
        }

        public void OnStorageUnlockDoor()
        {
            EnsureStorageLabels();
            AudioSource.PlayClipAtPoint(ProceduralAudioGenerator.CreateLockLatchSound(), transform.position, 0.95f);

            var movers = Resources.FindObjectsOfTypeAll<ScriptedMover>();
            foreach (var mover in movers)
            {
                if (mover != null && mover.gameObject.scene.name != null && mover.houseNumber == 1)
                {
                    mover.isLocked = false;
                    mover.SetOpen(true);
                }
            }
            if (storageMainDoor != null)
            {
                storageMainDoor.isLocked = false;
                storageMainDoor.SetOpen(true);
            }
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(1, 10);

            if (storageStatusLabel != null)
                storageStatusLabel.text = "UNIT STATUS: <color=#44FF44>UNLOCKED (OPEN)</color>";
        }

        // --- HOUSE 2: BOOMBOX UI ---
        private void EnsureBoomboxAudioSource()
        {
            if (_tracks == null || _tracks.Length == 0)
            {
                _tracks = new AudioClip[]
                {
                    ProceduralAudioGenerator.CreateRadioLoop(0),
                    ProceduralAudioGenerator.CreateRadioLoop(1)
                };
            }

            if (boomboxAudioSource == null)
            {
                boomboxAudioSource = gameObject.AddComponent<AudioSource>();
            }
            boomboxAudioSource.spatialBlend = 0.25f;
            boomboxAudioSource.loop = true;
            boomboxAudioSource.playOnAwake = false;
        }

        public void OnBoomboxPowerToggle()
        {
            EnsureBoomboxAudioSource();

            bool anyRadioPlaying = _boomboxPlaying || boomboxAudioSource.isPlaying;
            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (src != null && src.isPlaying && src.clip != null && src.clip.name.Contains("Radio"))
                {
                    anyRadioPlaying = true;
                    break;
                }
            }

            _boomboxPlaying = !anyRadioPlaying;
            if (_boomboxPlaying)
            {
                boomboxAudioSource.clip = _tracks[_currentTrack % _tracks.Length];
                boomboxAudioSource.Play();
                if (boomboxStatusLabel != null)
                    boomboxStatusLabel.text = $"RADIO ON - TRACK {_currentTrack + 1}";
            }
            else
            {
                StopAllRadioAudioSources();
                if (boomboxStatusLabel != null)
                    boomboxStatusLabel.text = "RADIO OFF";
            }

            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(2, 4);
        }

        public void OnBoomboxStopOff()
        {
            EnsureBoomboxAudioSource();
            _boomboxPlaying = false;
            StopAllRadioAudioSources();
            if (boomboxStatusLabel != null)
                boomboxStatusLabel.text = "RADIO OFF";

            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(2, 4);
        }

        private void StopAllRadioAudioSources()
        {
            if (boomboxAudioSource != null)
                boomboxAudioSource.Stop();

            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (src != null && src.clip != null && src.clip.name.Contains("Radio"))
                {
                    src.Stop();
                }
            }
        }

        public void OnBoomboxNextTrack()
        {
            EnsureBoomboxAudioSource();
            StopAllRadioAudioSources();

            _currentTrack = (_currentTrack + 1) % _tracks.Length;
            boomboxAudioSource.clip = _tracks[_currentTrack];
            _boomboxPlaying = true;
            boomboxAudioSource.Play();

            if (boomboxStatusLabel != null)
                boomboxStatusLabel.text = $"PLAYING TRACK {_currentTrack + 1}";

            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(2, 5);
        }

        // --- HOUSE 3: SHOP CASH REGISTER UI ---
        public void OnShopStartShopping()
        {
            EnsureShopLabels();
            _runningTotal = 0f;
            PlayRegisterBeep();
            if (registerStatusLabel != null)
                registerStatusLabel.text = "REGISTER OPEN! SCAN ITEMS ($0.00)";
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(3, 1);
        }

        public void OnShopAddBottlePrice(float price)
        {
            EnsureShopLabels();
            _runningTotal += price;
            PlayRegisterBeep();
            if (registerStatusLabel != null)
                registerStatusLabel.text = $"ADDED BOTTLE (+${price:F2}) | TOTAL: ${_runningTotal:F2}";
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(3, 2);
        }

        public void OnShopSelectGroceriesCategory()
        {
            OnShopAddBottlePrice(2.50f);
        }

        public void OnShopAddGroceryPrice(float price)
        {
            EnsureShopLabels();
            _runningTotal += price;
            PlayRegisterBeep();
            string itemLabel = price > 1.75f ? "CHIPS" : "LEEK";
            if (registerStatusLabel != null)
                registerStatusLabel.text = $"ADDED {itemLabel} (+${price:F2}) | TOTAL: ${_runningTotal:F2}";
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(3, 5);
        }

        public void OnShopPayAndConfirm()
        {
            EnsureShopLabels();
            PlayCashRegisterSound();
            if (registerStatusLabel != null)
                registerStatusLabel.text = $"PAID ${_runningTotal:F2} - THANK YOU!";

            if (cashRegisterDrawer == null)
            {
                EnsureCashRegisterDrawerCreated();
            }

            if (cashRegisterDrawer != null)
            {
                cashRegisterDrawer.isLocked = false;
                cashRegisterDrawer.SetOpen(true);
            }
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(3, 9);
        }

        // --- HOUSE 4: MECHANIC SHOP UI ---
        public void OnMechanicTestHorn()
        {
            EnsureMechanicLabels();
            if (carAudioSource == null)
            {
                carAudioSource = gameObject.AddComponent<AudioSource>();
                carAudioSource.spatialBlend = 0f;
            }
            AudioClip clip = customHornClip != null ? customHornClip : ProceduralAudioGenerator.CreateCarHorn(0.75f);
            carAudioSource.PlayOneShot(clip, 0.95f);
            if (mechanicStatusLabel != null)
                mechanicStatusLabel.text = "DIAGNOSTIC: <color=#44FF44>HORN HONK OK!</color>";
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(4, 9);
        }

        public void OnMechanicTestEngine()
        {
            EnsureMechanicLabels();
            if (carAudioSource == null)
            {
                carAudioSource = gameObject.AddComponent<AudioSource>();
                carAudioSource.spatialBlend = 0f;
            }
            AudioClip clip = customEngineRevClip != null ? customEngineRevClip : ProceduralAudioGenerator.CreateEngineRev(1.8f);
            carAudioSource.PlayOneShot(clip, 0.95f);
            if (mechanicStatusLabel != null)
                mechanicStatusLabel.text = "DIAGNOSTIC: <color=#44FF44>ENGINE V8 REV OK!</color>";
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(4, 10);
        }

        // --- HOUSE 5: BACKROOMS FLASHLIGHT UI ---
        public void OnFlashlightToggleSwitch()
        {
            if (flashlightBeam != null)
            {
                flashlightBeam.enabled = !flashlightBeam.enabled;
                if (flashlightStatusLabel != null)
                    flashlightStatusLabel.text = flashlightBeam.enabled ? "BACKROOMS TERMINAL | LIGHT: ON" : "BACKROOMS TERMINAL | LIGHT: OFF";
            }
            if (VillageActionManager.Instance != null)
                VillageActionManager.Instance.CompleteAction(5, 4);
        }

        public void OnDrinkAlmondWaterHelper()
        {
            AlmondWaterBottle[] bottles = FindObjectsByType<AlmondWaterBottle>(FindObjectsSortMode.None);
            foreach (var b in bottles)
            {
                if (b != null && b.gameObject.activeInHierarchy)
                {
                    b.DrinkBottle();
                    if (flashlightStatusLabel != null)
                        flashlightStatusLabel.text = $"DRANK ALMOND WATER ({AlmondWaterBottle.TotalDrunkCount}/5)";
                    return;
                }
            }

            // Fallback if all scene bottles were already consumed
            AlmondWaterBottle.DrinkStaticFallback(transform.position);
            if (flashlightStatusLabel != null)
                flashlightStatusLabel.text = $"DRANK ALMOND WATER ({AlmondWaterBottle.TotalDrunkCount}/5)";
        }
    }
}
