using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.UI.Spells
{
    public sealed class SpellDragController
    {
        private readonly VisualElement _root;
        private readonly Func<int, bool> _isSlotUnlocked;
        private readonly Action<int> _onDropToSlotIndex;
        private readonly Action _onDragStarted;
        private readonly Action _onDragEnded;

        private VisualElement _ghost;
        private int _pointerId = -1;
        private bool _dragging;

        public SpellDragController(
            VisualElement root,
            Func<int, bool> isSlotUnlocked,
            Action<int> onDropToSlotIndex,
            Action onDragStarted = null,
            Action onDragEnded = null
        )
        {
            _root = root;
            _isSlotUnlocked = isSlotUnlocked;
            _onDropToSlotIndex = onDropToSlotIndex;
            _onDragStarted = onDragStarted;
            _onDragEnded = onDragEnded;
        }

        public void BeginDrag(PointerDownEvent evt, Sprite iconSprite)
        {
            if (_dragging)
                return;

            _dragging = true;
            _onDragStarted?.Invoke();
            _pointerId = evt.pointerId;
            _root.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);

            // ghost icon
            _ghost = new VisualElement
            {
                name = "SpellDragGhost",
                pickingMode = PickingMode.Ignore,
            };
            _ghost.style.position = Position.Absolute;
            _ghost.style.width = 64;
            _ghost.style.height = 64;
            _ghost.style.opacity = 0.9f;

            if (iconSprite != null)
                _ghost.style.backgroundImage = new StyleBackground(iconSprite);

            _root.Add(_ghost);
            MoveGhost(evt.position);

            _root.CapturePointer(_pointerId);
            _root.RegisterCallback<PointerMoveEvent>(OnMove);
            _root.RegisterCallback<PointerUpEvent>(OnUp);

            evt.StopPropagation();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId)
                return;

            MoveGhost(evt.position);
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId)
                return;

            // hit test
            int slotIndex = TryGetSlotIndexUnderPointer(evt.position);

            Cleanup();

            if (slotIndex >= 0 && _isSlotUnlocked(slotIndex))
                _onDropToSlotIndex(slotIndex);

            evt.StopPropagation();
        }

        private void MoveGhost(Vector2 pos)
        {
            if (_ghost == null || _root == null)
                return;

            // `pos` is in panel/world space; absolute positioning expects coordinates in the parent's local space.
            var local = _root.WorldToLocal(pos);

            float halfW = _ghost.resolvedStyle.width > 1f ? _ghost.resolvedStyle.width * 0.5f : 32f;
            float halfH =
                _ghost.resolvedStyle.height > 1f ? _ghost.resolvedStyle.height * 0.5f : 32f;

            _ghost.style.left = local.x - halfW;
            _ghost.style.top = local.y - halfH;
            _ghost.BringToFront();
        }

        private int TryGetSlotIndexUnderPointer(Vector2 pos)
        {
            if (_root?.panel == null)
                return -1;

            var picked = _root.panel.Pick(pos);
            if (picked == null)
                return -1;

            // Walk up parents until we hit "ActiveSpellSlotX"
            var ve = picked;
            while (ve != null)
            {
                if (!string.IsNullOrEmpty(ve.name) && ve.name.StartsWith("ActiveSpellSlot"))
                {
                    var suffix = ve.name.Substring("ActiveSpellSlot".Length);
                    if (int.TryParse(suffix, out int oneBased))
                        return oneBased - 1; // runtime slots are 0-based
                }
                ve = ve.parent;
            }

            return -1;
        }

        private void Cleanup()
        {
            bool wasDragging = _dragging;
            _dragging = false;

            if (_root != null && _pointerId != -1)
            {
                if (_root.HasPointerCapture(_pointerId))
                    _root.ReleasePointer(_pointerId);

                _root.UnregisterCallback<PointerMoveEvent>(OnMove);
                _root.UnregisterCallback<PointerUpEvent>(OnUp);
                _root.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            }

            if (_ghost != null)
                _ghost.RemoveFromHierarchy();

            _ghost = null;
            _pointerId = -1;

            if (wasDragging)
                _onDragEnded?.Invoke();
        }

        public void CancelDrag()
        {
            if (!_dragging)
                return;

            Cleanup();
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            // If we lose capture for any reason (tab hides, focus changes, etc.) — kill the drag.
            if (_dragging)
                Cleanup();
        }
    }
}
