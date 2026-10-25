using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>
    /// Action: tidy up messy chairs. Every play, a random few of the chairs are scattered to random
    /// free floor spots near where they belong (turned, one maybe knocked over); the rest stay put.
    /// Setting a chair down near a free spot (a green square on the floor) slides it into place.
    /// Chairs are interchangeable: any chair fills any spot. Completes when every spot is filled.
    /// </summary>
    public class ArrangeChairsStep : QuestStep
    {
        [Tooltip("Chairs that can be messed up. Each starts in the spot with the same index.")]
        [SerializeField] private XRGrabInteractable[] m_Chairs;
        [Tooltip("Where chairs belong (same order as Chairs): where they stood in the model.")]
        [SerializeField] private Transform[] m_Spots;
        [Tooltip("Floor markers, one per spot, shown while the spot is empty during this step.")]
        [SerializeField] private GameObject[] m_SpotMarkers;

        [Header("Mess (random every play)")]
        [SerializeField] private int m_MessCount = 4;
        [Tooltip("How far (m) a messy chair ends up from its spot.")]
        [SerializeField] private Vector2 m_MessDistance = new Vector2(0.75f, 1.4f);
        [SerializeField, Range(0f, 1f)] private float m_KnockOverChance = 0.6f;

        [Header("Highlights (shown during this step)")]
        [Tooltip("Pulsing see-through shell drawn over chairs that are out of place.")]
        [SerializeField] private Material m_HighlightMaterial;
        [Tooltip("\"Move me!\" markers, placed above out-of-place chairs (one each, as many as there are).")]
        [SerializeField] private Transform[] m_MoveMarkers;
        [Tooltip("Marker height above the top of the chair (m).")]
        [SerializeField] private float m_MarkerHeight = 0.3f;

        [Header("Snapping")]
        [Tooltip("A released chair closer than this (m, horizontally) to a free spot snaps into it.")]
        [SerializeField] private float m_SnapDistance = 0.45f;
        [SerializeField] private float m_SnapTime = 0.25f;

        private XRGrabInteractable[] m_InSpot;
        private bool[] m_Sliding;
        private int m_Required;
        private GameObject[] m_Glows;
        private Material m_GlowMaterial; // runtime copy, so pulsing never touches the asset
        private Color m_GlowColor;

        public override Transform GuideTarget
        {
            get
            {
                if (m_Chairs == null || m_Spots == null) return base.GuideTarget;
                foreach (XRGrabInteractable chair in m_Chairs)
                {
                    if (chair != null && GrabUtil.IsHeldByHand(chair))
                    {
                        int spot = NearestFreeSpot(chair.transform.position, float.MaxValue);
                        return spot >= 0 ? m_Spots[spot] : base.GuideTarget;
                    }
                }
                foreach (XRGrabInteractable chair in m_Chairs)
                {
                    if (chair != null && SpotOf(chair) < 0) return chair.transform;
                }
                return base.GuideTarget;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            int count = m_Chairs != null && m_Spots != null ? Mathf.Min(m_Chairs.Length, m_Spots.Length) : 0;
            m_InSpot = new XRGrabInteractable[m_Spots != null ? m_Spots.Length : 0];
            m_Sliding = new bool[m_Chairs != null ? m_Chairs.Length : 0];

            // Every chair starts in its own spot, resting still.
            for (int i = 0; i < count; i++)
            {
                if (m_Chairs[i] == null || m_Spots[i] == null) continue;
                m_InSpot[i] = m_Chairs[i];
                Rigidbody rb = m_Chairs[i].GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }
            Scatter(count);
            BuildGlows();
            ShowMarkers();
            UpdateHighlights();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_GlowMaterial != null) Destroy(m_GlowMaterial);
        }

        protected override void OnBegin()
        {
            ShowMarkers();
            UpdateProgress();
        }

        protected override void OnEnd() => ShowMarkers();

        // ------------------------------------------------------------------
        // Mess
        // ------------------------------------------------------------------
        private void Scatter(int count)
        {
            // Pick which chairs get messy: shuffle the indices, take the first few.
            int[] order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            Physics.SyncTransforms();
            int knockedOver = Random.value < m_KnockOverChance ? 0 : -1; // the first messy chair, sometimes
            m_Required = 0;
            for (int k = 0; k < count && m_Required < m_MessCount; k++)
            {
                int i = order[k];
                if (m_Chairs[i] == null || m_Spots[i] == null) continue;
                if (TryMessUp(m_Chairs[i], m_Spots[i], m_Required == knockedOver))
                {
                    m_InSpot[i] = null;
                    m_Required++;
                }
            }
        }

        /// <summary>Move a chair to a random clear spot on the floor nearby. False if none was found.</summary>
        private bool TryMessUp(XRGrabInteractable chair, Transform spot, bool knockOver)
        {
            Collider[] own = chair.GetComponentsInChildren<Collider>();
            Bounds size = own.Length > 0 ? own[0].bounds : new Bounds(chair.transform.position, Vector3.one * 0.45f);
            foreach (Collider col in own) col.enabled = false; // don't collide with itself while testing

            float radius = Mathf.Max(size.extents.x, size.extents.z);
            bool found = false;
            Vector3 pos = spot.position;
            Quaternion rot = spot.rotation;
            for (int attempt = 0; attempt < 40 && !found; attempt++)
            {
                Vector2 dir2 = Random.insideUnitCircle.normalized;
                Vector3 dir = new Vector3(dir2.x, 0f, dir2.y);
                pos = spot.position + dir * Random.Range(m_MessDistance.x, m_MessDistance.y);
                float yaw = Random.Range(35f, 150f) * (Random.value < 0.5f ? -1f : 1f);
                rot = Quaternion.AngleAxis(yaw, Vector3.up) * spot.rotation;

                // Far enough from every spot that it won't snap back by itself.
                if (NearestSpotDistance(pos) < m_SnapDistance + 0.15f) continue;
                // A clear path from its spot (no walking through walls, tables or the counter)...
                if (Physics.Linecast(spot.position, pos, ~0, QueryTriggerInteraction.Ignore)) continue;
                // ...and room to stand, just above the floor.
                Vector3 center = new Vector3(pos.x, size.center.y + 0.05f, pos.z);
                Vector3 half = new Vector3(radius, Mathf.Max(size.extents.y - 0.04f, 0.05f), radius) * 0.95f;
                if (Physics.CheckBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;

                if (knockOver) rot = Quaternion.AngleAxis(90f, Vector3.Cross(Vector3.up, dir)) * rot;
                found = true;
            }

            foreach (Collider col in own) col.enabled = true;
            if (!found) return false;

            // Lying on its side, the chair's half-depth is its new height above the floor.
            if (knockOver) pos.y = size.min.y + radius + 0.03f;
            chair.transform.SetPositionAndRotation(pos, rot);
            Rigidbody rb = chair.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
            Physics.SyncTransforms(); // later chairs avoid this one
            return true;
        }

        private float NearestSpotDistance(Vector3 position)
        {
            float best = float.MaxValue;
            foreach (Transform s in m_Spots)
            {
                if (s == null) continue;
                Vector3 d = s.position - position;
                d.y = 0f;
                best = Mathf.Min(best, d.magnitude);
            }
            return best;
        }

        // ------------------------------------------------------------------
        // Highlights: glowing shell + "Move me!" on chairs that are out of place
        // ------------------------------------------------------------------
        private void BuildGlows()
        {
            m_Glows = new GameObject[m_Chairs != null ? m_Chairs.Length : 0];
            if (m_HighlightMaterial == null || m_MoveMarkers == null || m_MoveMarkers.Length == 0)
                Debug.LogWarning("[Quest] Chair highlights aren't set up. Run 'VR Village/Setup Cafe Quest' again.", this);
            if (m_HighlightMaterial == null) return;
            m_GlowMaterial = new Material(m_HighlightMaterial);
            m_GlowColor = m_GlowMaterial.HasProperty("_BaseColor") ? m_GlowMaterial.GetColor("_BaseColor") : Color.yellow;
            for (int i = 0; i < m_Glows.Length; i++)
            {
                MeshFilter source = m_Chairs[i] != null ? m_Chairs[i].GetComponent<MeshFilter>() : null;
                if (source == null || source.sharedMesh == null) continue;
                var go = new GameObject("Highlight");
                go.transform.SetParent(m_Chairs[i].transform, false);
                go.transform.localScale = Vector3.one * 1.04f; // a hair bigger, so it sits on top of the chair
                go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                var mats = new Material[source.sharedMesh.subMeshCount];
                for (int m = 0; m < mats.Length; m++) mats[m] = m_GlowMaterial;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                m_Glows[i] = go;
            }
        }

        private void LateUpdate() => UpdateHighlights();

        private void UpdateHighlights()
        {
            if (m_Chairs == null || m_Glows == null) return;
            if (m_GlowMaterial != null)
            {
                Color c = m_GlowColor;
                c.a = m_GlowColor.a * (0.55f + 0.45f * Mathf.Sin(Time.time * 4f));
                m_GlowMaterial.SetColor("_BaseColor", c);
            }

            Camera cam = Camera.main;
            int marker = 0;
            for (int i = 0; i < m_Chairs.Length; i++)
            {
                XRGrabInteractable chair = m_Chairs[i];
                bool outOfPlace = IsActive && chair != null && SpotOf(chair) < 0 && !m_Sliding[i] && !GrabUtil.IsHeldByHand(chair);
                if (m_Glows[i] != null && m_Glows[i].activeSelf != outOfPlace) m_Glows[i].SetActive(outOfPlace);
                if (!outOfPlace || m_MoveMarkers == null || marker >= m_MoveMarkers.Length) continue;

                Transform t = m_MoveMarkers[marker++];
                if (t == null) continue;
                Renderer r = chair.GetComponent<Renderer>();
                Bounds b = r != null ? r.bounds : new Bounds(chair.transform.position, Vector3.one * 0.5f);
                t.position = new Vector3(b.center.x, b.max.y + m_MarkerHeight, b.center.z);
                if (cam != null)
                {
                    Vector3 away = t.position - cam.transform.position; // readable side (-Z) toward the player
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(away, Vector3.up);
                }
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
            if (m_MoveMarkers == null) return;
            for (; marker < m_MoveMarkers.Length; marker++)
            {
                if (m_MoveMarkers[marker] != null && m_MoveMarkers[marker].gameObject.activeSelf)
                    m_MoveMarkers[marker].gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // Snapping
        // ------------------------------------------------------------------
        private void Update()
        {
            if (m_Chairs == null || m_Spots == null) return;
            bool changed = false;
            for (int i = 0; i < m_Chairs.Length; i++)
            {
                XRGrabInteractable chair = m_Chairs[i];
                if (chair == null) continue;
                int spot = SpotOf(chair);

                if (GrabUtil.IsHeldByHand(chair))
                {
                    if (spot >= 0) { m_InSpot[spot] = null; changed = true; } // picked back up
                    continue;
                }
                if (spot >= 0 || m_Sliding[i]) continue;

                Rigidbody rb = chair.GetComponent<Rigidbody>();
                // Dropped after being in a spot: the grab restored "kinematic", so let it fall again.
                if (rb != null && rb.isKinematic) rb.isKinematic = false;

                int free = NearestFreeSpot(chair.transform.position, m_SnapDistance);
                if (free < 0) continue;
                m_InSpot[free] = chair;
                changed = true;
                StartCoroutine(SlideInto(i, free));
            }

            if (changed)
            {
                ShowMarkers();
                if (IsActive) UpdateProgress();
            }
        }

        private IEnumerator SlideInto(int chairIndex, int spotIndex)
        {
            XRGrabInteractable chair = m_Chairs[chairIndex];
            Transform spot = m_Spots[spotIndex];
            Rigidbody rb = chair.GetComponent<Rigidbody>();
            m_Sliding[chairIndex] = true;
            if (rb != null) rb.isKinematic = true;

            Vector3 fromPos = chair.transform.position;
            Quaternion fromRot = chair.transform.rotation;
            for (float t = 0f; t < m_SnapTime; t += Time.deltaTime)
            {
                if (GrabUtil.IsHeldByHand(chair)) // grabbed again mid-slide
                {
                    m_Sliding[chairIndex] = false;
                    yield break;
                }
                float k = Mathf.SmoothStep(0f, 1f, t / m_SnapTime);
                chair.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, spot.position, k),
                    Quaternion.Slerp(fromRot, spot.rotation, k));
                yield return null;
            }
            chair.transform.SetPositionAndRotation(spot.position, spot.rotation);
            m_Sliding[chairIndex] = false;
            SynthAudio.PlayAt(SynthSound.ChairThunk, spot.position);
        }

        private void UpdateProgress()
        {
            int free = 0;
            foreach (XRGrabInteractable c in m_InSpot) if (c == null) free++;
            int total = Mathf.Max(m_Required, free);
            if (free == 0)
            {
                SetProgress("<color=#6EE787>All tidy!</color>");
                Complete();
                return;
            }
            SetProgress($"<color=#E6E6EA>Chairs in place: <b>{total - free}/{total}</b></color>\n" +
                        "<color=#FFC83D>Glowing</color><color=#8A8A92><i> chairs are out of place. Set each one on a </i></color>" +
                        "<color=#6EE787>green ghost chair</color>");
        }

        private void ShowMarkers()
        {
            if (m_SpotMarkers == null) return;
            for (int i = 0; i < m_SpotMarkers.Length; i++)
            {
                if (m_SpotMarkers[i] == null) continue;
                bool free = m_InSpot == null || i >= m_InSpot.Length || m_InSpot[i] == null;
                m_SpotMarkers[i].SetActive(IsActive && free);
            }
        }

        private int SpotOf(XRGrabInteractable chair)
        {
            for (int s = 0; s < m_InSpot.Length; s++)
            {
                if (m_InSpot[s] == chair) return s;
            }
            return -1;
        }

        private int NearestFreeSpot(Vector3 position, float maxDistance)
        {
            int best = -1;
            float bestDist = maxDistance;
            for (int s = 0; s < m_Spots.Length; s++)
            {
                if (m_Spots[s] == null || m_InSpot[s] != null) continue;
                Vector3 d = m_Spots[s].position - position;
                d.y = 0f;
                if (d.magnitude < bestDist)
                {
                    bestDist = d.magnitude;
                    best = s;
                }
            }
            return best;
        }
    }
}
