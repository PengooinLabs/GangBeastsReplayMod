using UnityEngine;

namespace PengooinLabs.ReplayMod
{
    public class Colors
    {
        public static Color halfTransparentBlack = new Color(0, 0, 0, .5f);
        public static Color transparent = new Color(0, 0, 0, 0);
        public static Color greenHoverColor = Color.green;
        public static Color gold = new Color(1, 215f / 255f, 0);
        public static Color playingGreenColor = new Color(85f / 255f, 211f / 255f, 85f / 255f);
        public static Color hintColor = new Color(192f / 255f, 192f / 255f, 192f / 255f);
        public static Color projectLinkTextColor = Color.white;
        public static Color projectLinkHoverColor = gold;
        public static Color menuSectionTextColor = Color.white;
        public static Color noRed = new Color(246f / 255f, 89f / 255f, 89f / 255f);
        public static Color panelBgColor = new Color(0, 0, 0, 0.9f);
        public static Color menuOptionColor = Color.gray;
        public static Color hoveredMenuOptionColor = Color.white;
        public static Color sliderBarColor = Color.gray;
        public static Color sliderThumbColor = Color.white;
        public static Color sliderThumbHoverColor = gold;
        public static Color burgerActiveColor = gold;
        public static Color clickableLabelColor = Colors.burgerActiveColor;
    }

    public class Vars
    {
        public static int playerButtonsFontSize = 16;
        public static float menuWidth = 500f;
        public static float helpMenuWidth = 1000f;
        public static int menuFontSize = 16;
        public static int sectionFontSize = 18;
        public static int projectLinkFontSize = 14;
        public static int menuOffsetLeft = 10;
        public static int menuOffsetBottom = 10;
        public static int menuOffsetTop = 10;
    }
}
