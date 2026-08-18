using UnityEngine;

namespace PengooinLabs.ReplayMod
{
    public class UI
    {

        private static GUIStyle? _blackBgStyle = null;

        public static GUIStyle blackBgStyle
        {
            get
            {
                if (_blackBgStyle == null)
                {
                    _blackBgStyle = new GUIStyle();
                    _blackBgStyle.fontSize = UI.centeredTextFontSize;
                    _blackBgStyle.normal.textColor = Color.white;
                    _blackBgStyle.alignment = TextAnchor.MiddleCenter;
                    _blackBgStyle.normal.background = null;
                }
                _blackBgStyle.normal.background ??= UI.getTexture2D(Color.black);
                return _blackBgStyle;
            }
        }


        private static GUIStyle? _sliderLabelStyle = null;
        private static GUIStyle sliderLabelStyle
        {
            get
            {
                if (_sliderLabelStyle == null)
                {
                    _sliderLabelStyle = new GUIStyle();
                    _sliderLabelStyle.fontSize = Vars.menuFontSize;
                    _sliderLabelStyle.alignment = TextAnchor.MiddleLeft;
                    _sliderLabelStyle.padding = new RectOffset(10, 10, 0, 0);
                }
                return _sliderLabelStyle;
            }
        }

        private static GUIStyle? _sliderValueStyle = null;
        private static GUIStyle sliderValueStyle
        {
            get
            {
                if (_sliderValueStyle == null)
                {
                    _sliderValueStyle = new GUIStyle(sliderLabelStyle);
                    _sliderValueStyle.alignment = TextAnchor.MiddleRight;
                    _sliderValueStyle.padding = new RectOffset(0, 10, 0, 0);
                }
                return _sliderValueStyle!;
            }
        }

        private static GUIStyle? _sliderBarStyle = null;
        private static GUIStyle sliderBarStyle
        {
            get
            {
                if (_sliderBarStyle == null)
                {
                    _sliderBarStyle = new GUIStyle(GUI.skin.horizontalSlider);
                    _sliderBarStyle.normal.background = null;
                }
                _sliderBarStyle.normal.background ??= UI.getTexture2D(Colors.sliderBarColor);
                return _sliderBarStyle!;
            }
        }

        private static GUIStyle? _sliderThumbStyle = null;
        private static GUIStyle sliderThumbStyle
        {
            get
            {
                if (_sliderThumbStyle == null)
                {
                    _sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
                    _sliderThumbStyle.normal.background = null;
                    _sliderThumbStyle.hover.background = null;
                    _sliderThumbStyle.active.background = null;
                }
                _sliderThumbStyle.normal.background ??= UI.getTexture2D(Colors.sliderThumbColor);
                _sliderThumbStyle.hover.background ??= UI.getTexture2D(Colors.sliderThumbHoverColor);
                _sliderThumbStyle.active.background ??= UI.getTexture2D(Colors.sliderThumbHoverColor);
                return _sliderThumbStyle!;
            }
        }

        private static GUIStyle? _sectionStyle;
        public static GUIStyle sectionStyle
        {
            get
            {
                if (_sectionStyle == null)
                {
                    _sectionStyle = new GUIStyle();
                    _sectionStyle.fontSize = Vars.sectionFontSize;
                    _sectionStyle.normal.textColor = Colors.menuSectionTextColor;
                    _sectionStyle.alignment = TextAnchor.MiddleCenter;
                }
                return _sectionStyle;
            }
        }

        private static GUIStyle? _hintStyle = null;
        public static GUIStyle hintStyle
        {
            get
            {
                if (_hintStyle == null)
                {
                    _hintStyle = new GUIStyle();
                    _hintStyle.normal.textColor = Colors.hintColor;
                    _hintStyle.alignment = TextAnchor.MiddleCenter;
                    _hintStyle.padding = new RectOffset(10, 10, 15, 15);

                }
                return _hintStyle;
            }
        }

        private static GUIStyle? _replayItemStyle = null;

        public static GUIStyle replayItemStyle
        {
            get
            {
                if (_replayItemStyle == null)
                {
                    _replayItemStyle = new GUIStyle(emptyButtonStyle);
                    _replayItemStyle.alignment = TextAnchor.MiddleLeft;
                    _replayItemStyle.fontSize = Vars.menuFontSize;
                    _replayItemStyle.hover.background = null;
                    _replayItemStyle.normal.textColor = Color.gray;
                    _replayItemStyle.hover.textColor = Colors.gold;
                    _replayItemStyle.active.textColor = Colors.gold;
                    _replayItemStyle.padding = new RectOffset(10, 10, 0, 0);
                }

                _replayItemStyle.hover.background ??= UI.getTexture2D(Colors.transparent);
                return _replayItemStyle;
            }
        }

        private static GUIStyle? _emptyButtonStyle = null;

        public static GUIStyle emptyButtonStyle
        {
            get
            {
                if (_emptyButtonStyle == null)
                {
                    _emptyButtonStyle = new GUIStyle(GUI.skin.label);
                    _emptyButtonStyle.margin = Replay.zeroRectOffset;
                    _emptyButtonStyle.padding = Replay.zeroRectOffset;
                    _emptyButtonStyle.alignment = TextAnchor.MiddleCenter;
                }

                return _emptyButtonStyle;
            }
        }

        private static GUIStyle? _backgroundStyle = null;

        public static GUIStyle backgroundStyle
        {
            get
            {
                _backgroundStyle ??= new GUIStyle();
                _backgroundStyle.normal.background ??= UI.getTexture2D(Colors.panelBgColor);
                return _backgroundStyle;
            }
        }

        private static GUIStyle? _navButtonStyle = null;
        public static GUIStyle navButtonStyle
        {
            get
            {
                if (_navButtonStyle == null)
                {
                    _navButtonStyle = new GUIStyle(emptyButtonStyle);
                    _navButtonStyle.normal.textColor = Color.gray;
                    _navButtonStyle.hover.textColor = Colors.gold;
                    _navButtonStyle.hover.background = null;
                    _navButtonStyle.alignment = TextAnchor.MiddleCenter;
                }

                _navButtonStyle.hover.background ??= UI.getTexture2D(Colors.transparent);
                return _navButtonStyle;
            }
        }

        private static float _sectionHeight = 0;
        public static float sectionHeight
        {
            get
            {
                if (_sectionHeight == 0)
                {
                    _sectionHeight = Tools.getStyleLineHeight(UI.sectionStyle) + 20;
                }
                return _sectionHeight;
            }
        }

        public static void blackBgCenteredMessage(string text)
        {
            
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), text, blackBgStyle);
        }
        public static void horizontalSpacer(float width)
        {
            GUILayout.Label(GUIContent.none, new GUILayoutOption[] { GUILayout.Width(width), GUILayout.ExpandHeight(true) });
        }

        public static void horizontalLabel(string text, GUIStyle style, float width)
        {
            var options = new GUILayoutOption[] { GUILayout.Width(width), GUILayout.ExpandHeight(true) };
            GUILayout.Label(text, style, options);
        }

        public static bool horizontalButton(string text, GUIStyle style, float width)
        {
            var options = new GUILayoutOption[] { GUILayout.Width(width), GUILayout.ExpandHeight(true) };
            return GUILayout.Button(text, style, options);
        }

        public static void section(string text)
        {
            GUILayout.Label(text, UI.sectionStyle, GUILayout.Height(UI.sectionHeight));
        }

        private static float nativeSliderHeight = 11f;
        private static int optionHeight = 24;
        public static bool Option(string labelText, string valueText)
        {
            Rect row = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(optionHeight));

            bool hovered = Tools.isRectHovered(row);
            bool clicked = Tools.isRectClicked(row);

            sliderLabelStyle.normal.textColor = hovered ? Colors.hoveredMenuOptionColor : Colors.menuOptionColor;
            sliderValueStyle.normal.textColor = sliderLabelStyle.normal.textColor;
            GUI.Label(new Rect(row.x, row.y, row.width, row.height), labelText, sliderLabelStyle);
            GUI.Label(new Rect(row.x, row.y, row.width, row.height), valueText, sliderValueStyle);

            return clicked;
        }


        public static float SliderOption(string labelText, float value, float min, float max, int decimals)
        {
            // reserve row
            Rect row = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[] { GUILayout.Height(optionHeight), GUILayout.Width(Vars.menuWidth) });
            
            // label on the left
            sliderLabelStyle.normal.textColor = Colors.menuOptionColor;
            GUI.Label(new Rect(row.x, row.y, row.width, row.height), labelText, sliderLabelStyle);

            string valueText = (decimals != 0 ? value.ToString("F" + decimals) : ((int)value)+"");
            var valueWidth = sliderLabelStyle.CalcSize(new GUIContent(valueText)).x;

            int sliderWidth = 150;
            // value in front of the slider
            GUI.Label(new Rect(row.xMax - sliderWidth - valueWidth - 10, row.y, valueWidth, row.height), valueText, sliderLabelStyle);

            // slider on the right
            
            float paddingRight = 10;
            float topOffset = (float)Math.Ceiling((row.height - nativeSliderHeight) / 2f);
            Rect sliderRect = new Rect(row.xMax - sliderWidth - paddingRight, row.y + topOffset, sliderWidth, nativeSliderHeight);

            return GUI.HorizontalSlider(sliderRect, value, min, max, sliderBarStyle, sliderThumbStyle);
        }

        public static float Slider(float value, float minValue, float maxValue, float height, float width = 0)
        {
            // reserve space for outer Rect
            var outerRectOptions = (width > 0f) ?
                new[] { GUILayout.Width(width), GUILayout.Height(height) } :
                new[] { GUILayout.ExpandWidth(true), GUILayout.Height(height) };

            Rect outerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, outerRectOptions);

            // top offset of innerRect within outerRect
            float topOffset = (float)Math.Ceiling((height - nativeSliderHeight) / 2f);

            // create inner rect 
            Rect innerRect = new Rect(outerRect.x, outerRect.y + topOffset, outerRect.width, nativeSliderHeight);

            // draw the slider inside the vertically centered sliderRect
            return GUI.HorizontalSlider(innerRect, value, minValue, maxValue, sliderBarStyle, sliderThumbStyle);
        }


        public static bool isRepaintStep()
        {
            return Event.current.type == EventType.Repaint;
        }

        // textures
        private static Texture2D MakeTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply(false, false);
            return tex;
        }

        private static Dictionary<Color, Texture2D> cachedTexture2Ds = new();

        public static Texture2D getTexture2D(Color color)
        {
            if (!cachedTexture2Ds.ContainsKey(color) || cachedTexture2Ds[color] == null)
            {
                cachedTexture2Ds[color] = MakeTexture(color);
            }
            return cachedTexture2Ds[color];
        }

        public static int centeredTextFontSize = 30;
        private static GUIStyle? _centeredTextStyle = null;
        public static GUIStyle centeredTextStyle
        {
            get {
                if (_centeredTextStyle == null)
                {
                    _centeredTextStyle = new GUIStyle(Replay.labelStyle)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = centeredTextFontSize,
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return _centeredTextStyle;
            }
        }
        
        public static void drawCenteredText(string text)
        {
            UI.drawCenteredOutlineText(text, centeredTextStyle, Color.white, Color.black, 1);
        }

        public static void drawCenteredOutlineText(string text, GUIStyle style, Color textColor, Color outlineColor, int thickness)
        {
            // outline
            style.normal.textColor = outlineColor;

            for (int x = -thickness; x <= thickness; x++)
            {
                for (int y = -thickness; y <= thickness; y++)
                {
                    if (x == 0 && y == 0) continue;
                    GUI.Label(new Rect(x, y, Screen.width, Screen.height), text, style);
                }
            }

            // main text
            style.normal.textColor = textColor;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), text, style);
        }

        public static void GUILayoutOutlineLabel(
            string text,
            GUIStyle style,
            Color textColor,
            Color outlineColor,
            int thickness = 1,
            float _height = 0,
            float _width = 0
        ) {
            Vector2 size = style.CalcSize(new GUIContent(text));
            float height = _height > 0 ? _height : size.y;
            float width = _width > 0 ? _width : size.x;

            Rect rect = GUILayoutUtility.GetRect(
                new GUIContent(text),
                style,
                GUILayout.Width(width),
                GUILayout.Height(height)
            );

            OutlineLabel(rect, text, style, textColor, outlineColor, thickness);
        }

        public static void OutlineLabel(
            Rect rect,
            string text,
            GUIStyle style,
            Color textColor,
            Color outlineColor,
            int thickness = 1
        ) {
            style.normal.textColor = outlineColor;

            for (int x = -thickness; x <= thickness; x++)
            {
                for (int y = -thickness; y <= thickness; y++)
                {
                    if (x == 0 && y == 0) continue;
                    GUI.Label(
                        new Rect(
                            rect.x + x,
                            rect.y + y,
                            rect.width,
                            rect.height
                        ),
                        text,
                        style
                    );
                }
            }

            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
        }

        public static bool GUILayoutOutlineButton(
            string? id,
            string text,
            GUIStyle style,
            Color textColor,
            Color? hoverColor,
            Color outlineColor,
            int thickness,
            int _height
        ) {
            Vector2 size = style.CalcSize(new GUIContent(text));
            int height = _height > 0 ? _height : (int)size.y;

            Rect rect = GUILayoutUtility.GetRect(
                new GUIContent(text),
                style,
                GUILayout.Width(size.x),
                GUILayout.Height(height)
            );

            return OutlineButton(id, rect, text, style, textColor, hoverColor, outlineColor, thickness);
        }

        public static bool OutlineButton(
            string? id,
            Rect rect,
            string text,
            GUIStyle style,
            Color textColor,
            Color? _hoverColor,
            Color outlineColor,
            int thickness
        ) {
            var hoverColor = _hoverColor != null ? (Color)_hoverColor : textColor;
            var orgTextColor = style.normal.textColor;

            style.normal.textColor = outlineColor;

            for (int x = -thickness; x <= thickness; x++)
            {
                for (int y = -thickness; y <= thickness; y++)
                {
                    if (x == 0 && y == 0) continue;
                    GUI.Label(
                        new Rect(
                            rect.x + x,
                            rect.y + y,
                            rect.width,
                            rect.height
                        ),
                        text,
                        style
                    );
                }
            }

            style.normal.textColor = id != null && hovered.ContainsKey(id) ? hoverColor : textColor;
            var result = GUI.Button(rect, text, style);
            style.normal.textColor = orgTextColor;

            if (UI.isRepaintStep())
            {
                bool isHovered = Tools.isRectHovered(rect);

                if (id != null)
                {
                    if (isHovered && !hovered.ContainsKey(id))
                    {
                        hovered[id] = true;
                    }
                    else if (!isHovered && hovered.ContainsKey(id))
                    {
                        hovered.Remove(id);
                    }
                }
            }

            return result;
        }

        public static Dictionary<string, bool> hovered = new();

        public static void projectLink(string text)
        {
            if (GUILayout.Button(text, Menus.projectLinkStyle)) Application.OpenURL(Replay.PROJECT_PAGE_URL);
        }

        private static GUIStyle? _closeButtonStyle = null;
        public static GUIStyle closeButtonStyle
        {
            get
            {
                if (_closeButtonStyle == null)
                {
                    _closeButtonStyle = new GUIStyle(UI.emptyButtonStyle);
                    _closeButtonStyle.fontSize = Vars.projectLinkFontSize;
                    _closeButtonStyle.normal.textColor = Colors.projectLinkTextColor;
                    _closeButtonStyle.hover.textColor = Colors.projectLinkHoverColor;
                    _closeButtonStyle.alignment = TextAnchor.MiddleCenter;
                    _closeButtonStyle.padding = new RectOffset(10, 10, 10, 10);
                    _closeButtonStyle.normal.background = null;
                    _closeButtonStyle.hover.background = null;
                    _closeButtonStyle.active.background = null;
                }

                _closeButtonStyle.normal.background ??= UI.getTexture2D(Colors.transparent);
                _closeButtonStyle.hover.background ??= UI.getTexture2D(Colors.transparent);

                return _closeButtonStyle;
            }
        }
        public static bool closeButton()
        {
            return GUILayout.Button("Close", UI.closeButtonStyle);
        }
    }
}
