using UnityEngine;
using UnityEngine.EventSystems;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// A single palette tile. It is not a button: a part is taken from it by
    /// <b>dragging it onto the creature</b>, not by clicking.
    /// </summary>
    /// <remarks>
    /// Pressing starts the drag, and releasing ends it wherever the cursor happens to be — including
    /// outside the tile, which is why we catch <c>OnPointerUp</c> here and also close the drag in
    /// <c>OnEndDrag</c>. Without <c>IDragHandler</c> the event system does not send drag events at all.
    /// </remarks>
    public class CreaturePaletteItem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private CreatureEditorController _controller;
        private int _partId;
        private bool _mirrored;
        private bool _dragging;

        public void Bind(CreatureEditorController controller, int partId, bool mirrored)
        {
            _controller = controller;
            _partId = partId;
            _mirrored = mirrored;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_controller == null) return;

            _dragging = true;
            _controller.BeginPaletteDrag(_partId, _mirrored);
        }

        public void OnPointerUp(PointerEventData eventData) => Finish();

        public void OnBeginDrag(PointerEventData eventData) { }

        // Has to exist, otherwise the EventSystem will not treat the tile as draggable.
        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData) => Finish();

        private void Finish()
        {
            if (!_dragging) return;

            _dragging = false;
            _controller?.EndPaletteDrag();
        }
    }
}
