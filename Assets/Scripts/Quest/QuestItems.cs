using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRVillage
{
    /// <summary>Something that decides which item a later step is about (e.g. "the can you picked").</summary>
    public interface IQuestItemSource
    {
        XRGrabInteractable ChosenItem { get; }
    }

    public static class QuestItems
    {
        /// <summary>The fixed item if set, otherwise whatever the source step chose.</summary>
        public static XRGrabInteractable Resolve(XRGrabInteractable fixedItem, MonoBehaviour source)
        {
            if (fixedItem != null) return fixedItem;
            return source is IQuestItemSource s ? s.ChosenItem : null;
        }

        /// <summary>Mouth position: a little below and in front of the eyes.</summary>
        public static Vector3 Mouth(Transform head) => head.position - head.up * 0.09f + head.forward * 0.06f;

        /// <summary>Scale an object down along its thinnest local axis (e.g. an emptied snack bag).</summary>
        public static Vector3 FlattenedScale(GameObject go, float factor)
        {
            Vector3 scale = go.transform.localScale;
            Renderer r = go.GetComponentInChildren<Renderer>();
            if (r == null) return scale * factor;
            Vector3 size = r.localBounds.size;
            int axis = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
            scale[axis] *= factor;
            return scale;
        }
    }
}
