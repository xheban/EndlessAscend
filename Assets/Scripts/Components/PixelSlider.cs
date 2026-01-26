using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.UI
{
    [UxmlElement]
    public partial class PixelSlider : VisualElement
    {
        [UxmlAttribute("_min")]
        public float Min { get; set; } = 0f;

        [UxmlAttribute("_max")]
        public float Max { get; set; } = 100f;

        [UxmlAttribute("_value")]
        public float Value { get; set; } = 42f;

        private readonly Slider _slider;

        private VisualElement _tracker;
        private VisualElement _fill;
        private VisualElement _dragger;

        private bool _built;

        private IVisualElementScheduledItem _buildScheduled;
        private int _buildRetries;
        private const int MaxBuildRetries = 12;

        public event Action<float> ValueChanged;

        public PixelSlider()
        {
            AddToClassList("pixel-slider-root");
            _slider = new Slider(Min, Max) { value = Value };
            _slider.label = string.Empty;
            hierarchy.Add(_slider);
            // Keep slider values in sync even before visuals exist
            _slider.RegisterValueChangedCallback(e =>
            {
                Value = e.newValue;
                UpdateFill();
                ValueChanged?.Invoke(Value);
            });

            // Apply range/value whenever geometry changes (UXML attrs, layout, etc.)
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                Apply(); // keeps low/high/value correct
                EnsureBuilt(); // try to build fill when internals are ready
                UpdateFill();
            });

            // One-shot (with limited retries) to build internals once attached.
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _buildRetries = 0;
                ScheduleBuildRetry(startingInMs: 0);
            });
        }

        private void ScheduleBuildRetry(long startingInMs)
        {
            _buildScheduled?.Pause();
            _buildScheduled = schedule
                .Execute(() =>
                {
                    Apply();
                    EnsureBuilt();
                    UpdateFill();

                    if (_built)
                        return;

                    if (_buildRetries++ >= MaxBuildRetries)
                        return;

                    ScheduleBuildRetry(startingInMs: 16);
                })
                .StartingIn(startingInMs);
        }

        private void Apply()
        {
            _slider.lowValue = Min;
            _slider.highValue = Max;

            // Ensure the *native* slider and our Value stay consistent
            float clamped = Mathf.Clamp(Value, Min, Max);
            if (!Mathf.Approximately(_slider.value, clamped))
                _slider.SetValueWithoutNotify(clamped);

            Value = clamped;
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                if (_dragger == null)
                {
                    _dragger =
                        _slider.Q<VisualElement>(name: "unity-dragger")
                        ?? _slider.Q<VisualElement>(className: "unity-dragger");
                }

                return;
            }

            // Try both name and class, Unity themes can differ
            _tracker =
                _slider.Q<VisualElement>(name: "unity-tracker")
                ?? _slider.Q<VisualElement>(className: "unity-tracker");

            if (_tracker == null)
                return; // not ready yet, scheduler will retry

            _dragger =
                _slider.Q<VisualElement>(name: "unity-dragger")
                ?? _slider.Q<VisualElement>(className: "unity-dragger");

            // Create or fetch fill inside the tracker
            _fill = _tracker.Q<VisualElement>(name: "fill");
            if (_fill == null)
            {
                _fill = new VisualElement { name = "fill" };
                _fill.AddToClassList("slider-fill");

                // Insert first so it renders behind other tracker children
                _tracker.Insert(0, _fill);
            }

            _fill.style.position = Position.Absolute;
            _fill.style.left = 0;
            _fill.style.top = 0;
            _fill.style.bottom = 0;

            // Tracker must be a positioning parent + clip the fill
            _tracker.style.position = Position.Relative;
            _tracker.style.overflow = Overflow.Hidden;

            _built = true;
        }

        private void UpdateFill()
        {
            if (!_built)
            {
                EnsureBuilt();
                Apply();
            }

            if (!_built || _tracker == null || _fill == null)
                return;

            // If value is at minimum (e.g., volume 0), hide the fill entirely.
            if (Mathf.Approximately(Value, Min))
            {
                _fill.style.visibility = Visibility.Hidden;
                _fill.style.width = 0f;
                _fill.style.height = Length.Percent(100f);
                return;
            }

            _fill.style.visibility = Visibility.Visible;

            // Prefer filling to the CENTER of the dragger (thumb) for a consistent look.
            // worldBound is reliable once layout has run.
            if (_dragger == null)
            {
                _dragger =
                    _slider.Q<VisualElement>(name: "unity-dragger")
                    ?? _slider.Q<VisualElement>(className: "unity-dragger");
            }

            if (_dragger != null && _tracker.worldBound.width > 0f)
            {
                var trackerWorld = _tracker.worldBound;
                var draggerWorld = _dragger.worldBound;

                float draggerCenterX = draggerWorld.xMin + (draggerWorld.width * 0.5f);
                float widthPx = draggerCenterX - trackerWorld.xMin;
                widthPx = Mathf.Clamp(widthPx, 0f, trackerWorld.width);

                _fill.style.width = widthPx;
            }
            else
            {
                float t = Mathf.InverseLerp(Min, Max, Value);
                _fill.style.width = Length.Percent(t * 100f);
            }

            _fill.style.height = Length.Percent(100f);
        }

        public void SetValueWithoutNotify(float value)
        {
            SetValue(value, notify: false);
        }

        public void SetValue(float value, bool notify = true)
        {
            float clamped = Mathf.Clamp(value, Min, Max);
            Value = clamped;

            if (!Mathf.Approximately(_slider.value, clamped))
                _slider.SetValueWithoutNotify(clamped);

            EnsureBuilt();
            UpdateFill();

            if (notify)
                ValueChanged?.Invoke(Value);
        }
    }
}
