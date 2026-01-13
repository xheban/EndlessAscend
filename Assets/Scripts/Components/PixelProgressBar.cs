using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.UI
{
    public enum PixelBarFill
    {
        Red,
        Blue,
        Green,
        Orange,
    }

    [UxmlElement]
    public partial class PixelProgressBar : VisualElement
    {
        // -------- UXML attributes (with "_" prefix) --------
        [UxmlAttribute("_min")]
        public float Min { get; set; } = 0f;

        [UxmlAttribute("_max")]
        public float Max { get; set; } = 100f;

        [UxmlAttribute("_value")]
        public float Value { get; set; } = 0f;

        [UxmlAttribute("_fill")]
        public PixelBarFill Fill { get; set; } = PixelBarFill.Red;

        [UxmlAttribute("_showText")]
        public bool ShowText { get; set; } = true;

        [UxmlAttribute("_showLabel")]
        public bool ShowLabel { get; set; } = true;

        [UxmlAttribute("_showValues")]
        public bool ShowValues { get; set; } = true;

        [UxmlAttribute("_labelText")]
        public string LabelText { get; set; } = "";

        [UxmlAttribute("_separator")]
        public string Separator { get; set; } = " / ";

        [UxmlAttribute("_textSize")]
        public int TextSize { get; set; } = 16;

        // Optional explicit sizing (0 = not set here)
        [UxmlAttribute("_widthPx")]
        public float WidthPx { get; set; } = 0f;

        [UxmlAttribute("_heightPx")]
        public float HeightPx { get; set; } = 0f;

        // -------- Internal elements --------
        private readonly VisualElement _fillMask;
        private readonly VisualElement _fill;

        private readonly VisualElement _textRow;
        private readonly Label _label;
        private readonly Label _current;
        private readonly Label _sep;
        private readonly Label _max;

        private PixelBarFill _appliedFill;
        private bool _built;

        public PixelProgressBar()
        {
            AddToClassList("pixel-bar");
            style.flexShrink = 0;

            // Mask (cropping window). IMPORTANT: its final size is defined in USS:
            // left:4px; right:4px; top:3px; bottom:3px; overflow:hidden;
            _fillMask = new VisualElement { name = "FillMask" };
            _fillMask.AddToClassList("pixel-bar__mask");
            hierarchy.Add(_fillMask);

            // Fill lives inside the mask and we scale its width by %.
            _fill = new VisualElement { name = "Fill" };
            _fill.AddToClassList("pixel-bar__fill");
            _fillMask.Add(_fill);

            // Centered overlay text container
            _textRow = new VisualElement { name = "TextRow" };
            _textRow.AddToClassList("pixel-bar__text-row");
            hierarchy.Add(_textRow);

            _label = new Label { name = "LabelText" };
            _label.AddToClassList("pixel-bar__text-label");
            _textRow.Add(_label);

            _current = new Label { name = "CurrentText" };
            _current.AddToClassList("pixel-bar__text-value");
            _textRow.Add(_current);

            _sep = new Label { name = "SepText" };
            _sep.AddToClassList("pixel-bar__text-sep");
            _textRow.Add(_sep);

            _max = new Label { name = "MaxText" };
            _max.AddToClassList("pixel-bar__text-value");
            _textRow.Add(_max);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _built = true;
                ApplyOptionalSize();
                ApplyFillVariant(force: true);
                Refresh();
            });

            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (!_built)
                    return;
                Refresh();
            });
        }

        // -------- Runtime API (optional convenience) --------

        public void SetRange(float min, float max)
        {
            Min = min;
            Max = max;
            Refresh();
        }

        public void SetValue(float value)
        {
            Value = value;
            Refresh();
        }

        public void SetValue(float value, float max)
        {
            Value = value;
            Max = max;
            Refresh();
        }

        public void SetNormalized(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            float v = Mathf.Lerp(Min, Max, t01);
            SetValue(v);
        }

        // -------- Internals --------

        private void ApplyOptionalSize()
        {
            if (WidthPx > 0.01f)
                style.width = WidthPx;
            if (HeightPx > 0.01f)
                style.height = HeightPx;
        }

        private void ApplyFillVariant(bool force)
        {
            if (!force && _appliedFill == Fill)
                return;

            if (!force)
                RemoveFromClassList(FillClassName(_appliedFill));

            AddToClassList(FillClassName(Fill));
            _appliedFill = Fill;
        }

        private void Refresh()
        {
            ApplyOptionalSize();
            ApplyFillVariant(force: false);

            // Avoid divide-by-zero
            float range = Mathf.Max(0.0001f, Max - Min);
            float t = Mathf.Clamp01((Value - Min) / range);

            // ✅ Key change:
            // - Do NOT resize the mask in code (that breaks right/left anchoring).
            // - The mask is a fixed "inner window" defined by USS:
            //   left:4px; right:4px; top:3px; bottom:3px;
            // - We scale the FILL width instead.
            _fill.style.width = Length.Percent(t * 100f);
            _fill.style.height = Length.Percent(100f);

            RefreshText();
        }

        private void RefreshText()
        {
            _textRow.style.display = ShowText ? DisplayStyle.Flex : DisplayStyle.None;
            if (!ShowText)
                return;

            // Apply text size uniformly
            _label.style.fontSize = TextSize;
            _current.style.fontSize = TextSize;
            _sep.style.fontSize = TextSize;
            _max.style.fontSize = TextSize;

            // Static label
            bool labelOn = ShowLabel && !string.IsNullOrEmpty(LabelText);
            _label.style.display = labelOn ? DisplayStyle.Flex : DisplayStyle.None;
            if (labelOn)
                _label.text = LabelText;

            // Values
            bool valuesOn = ShowValues;
            _current.style.display = valuesOn ? DisplayStyle.Flex : DisplayStyle.None;
            _sep.style.display = valuesOn ? DisplayStyle.Flex : DisplayStyle.None;
            _max.style.display = valuesOn ? DisplayStyle.Flex : DisplayStyle.None;

            if (valuesOn)
            {
                _current.text = Mathf.FloorToInt(Value).ToString();
                _sep.text = Separator;
                _max.text = Mathf.FloorToInt(Max).ToString();
            }
        }

        private static string FillClassName(PixelBarFill f) =>
            f switch
            {
                PixelBarFill.Red => "fill-red",
                PixelBarFill.Blue => "fill-blue",
                PixelBarFill.Green => "fill-green",
                PixelBarFill.Orange => "fill-orange",
                _ => "fill-red",
            };
    }
}
