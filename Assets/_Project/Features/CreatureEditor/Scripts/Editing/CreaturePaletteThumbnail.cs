using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The part preview on a palette tile: a still photo, and a spinning model under the cursor.
    /// </summary>
    /// <remarks>
    /// <para>The image itself is a <see cref="RawImage"/> rather than an <c>Image</c>, because only it
    /// will take a <c>RenderTexture</c> without repacking it into a <c>Sprite</c> every frame.
    /// Switching between the photo and the live render is a single texture swap.</para>
    ///
    /// <para>The component sits on the <b>tile</b>, not on the image: the image has raycasting turned
    /// off so it does not eat the drag, so pointer events only reach here anyway. It sits alongside
    /// <see cref="CreaturePaletteItem"/> and stays out of its way — that one handles pressing and
    /// dragging, this one only hovering.</para>
    ///
    /// <para>The cursor leaving always gives the live preview back, including when the tile disappears
    /// mid-hover (switching tabs destroys the tiles under the cursor) — hence releasing in
    /// <c>OnDisable</c> as well.</para>
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class CreaturePaletteThumbnail : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private PartThumbnailRenderer _renderer;
        private CreaturePartDefinition _definition;
        private RawImage _image;
        private Texture _still;

        public void Bind(PartThumbnailRenderer renderer, CreaturePartDefinition definition, RawImage image, Texture still)
        {
            _renderer = renderer;
            _definition = definition;
            _image = image;
            _still = still;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_renderer == null || _image == null) return;

            RenderTexture live = _renderer.BeginPreview(_definition);
            if (live != null) _image.texture = live;
        }

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void OnDisable() => Release();

        private void Release()
        {
            if (_renderer == null || _image == null) return;

            // We only give it back if we are the ones holding it. The cursor can be over two tiles in
            // the same frame, and the exit from the previous one arrives AFTER the entry into the next —
            // unconditional teardown would kill the preview the neighbour has just taken over.
            if (_renderer.IsPreviewing(_definition)) _renderer.EndPreview();

            _image.texture = _still;
        }
    }
}
