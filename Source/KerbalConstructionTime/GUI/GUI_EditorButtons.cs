using UniLinq;
using ToolbarControl_NS;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static GUIStyle _staffButtonToggle;
        private static Texture2D _staffButtonBackground;
        private static GUIContent _staffButtonContent;
        private static GUIContent _staffButtonNormalContent;
        private static GUIContent _staffButtonHoverContent;
        private static Rect _staffButtonRect;
        private static float _staffButtonScale;

        private static Texture2D _staffButtonNormalTex;
        private static Texture2D _staffButtonHoverTex;

        public static bool StaffButtonVisible = true;

        internal static void InitStaffToggle()
        {
            _staffButtonToggle = new GUIStyle(HighLogic.Skin.button)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
            _staffButtonToggle.normal = _staffButtonToggle.hover;
            _staffButtonToggle.active = _staffButtonToggle.hover;

            _staffButtonBackground = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref _staffButtonBackground, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/bckg");

            _staffButtonToggle.normal.background = _staffButtonBackground;
            _staffButtonToggle.hover.background = _staffButtonBackground;
            _staffButtonToggle.onHover.background = _staffButtonBackground;
            _staffButtonToggle.active.background = _staffButtonBackground;
            _staffButtonToggle.onActive.background = _staffButtonBackground;

            _staffButtonNormalTex = new Texture2D(2, 2);
            _staffButtonHoverTex = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref _staffButtonNormalTex, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/KCT_staff_normal");
            ToolbarControl.LoadImageFromFile(ref _staffButtonHoverTex, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/KCT_staff_hover");

            PositionAndSizeStaffButtonIcon();
        }

        private static void PositionAndSizeStaffButtonIcon()
        {
            Texture2D normalTex = Texture2D.Instantiate(_staffButtonNormalTex);
            Texture2D hoverTex = Texture2D.Instantiate(_staffButtonHoverTex);

            int offset = 0;
            bool steamPresent = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "KSPSteamCtrlr");
            bool mechjebPresent = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "MechJeb2");
            if (steamPresent)
                offset = 46;
            if (mechjebPresent)
                offset = 140;
            _staffButtonScale = GameSettings.UI_SCALE;

            _staffButtonRect = new Rect(Screen.width - (216 + offset) * _scale, 0, 42 * _scale, 38 * _scale);
            {
                TextureScale.Bilinear(normalTex, (int)(_staffButtonNormalTex.width * _scale), (int)(_staffButtonNormalTex.height * _scale));
                TextureScale.Bilinear(hoverTex, (int)(_staffButtonHoverTex.width * _scale), (int)(_staffButtonHoverTex.height * _scale));
            }
            _staffButtonNormalContent = new GUIContent("", normalTex, "");
            _staffButtonHoverContent = new GUIContent("", hoverTex, "");
        }

        private static void CreateStaffButtonToggle()
        {
            if (_staffButtonRect.Contains(Mouse.screenPos))
                _staffButtonContent = _staffButtonHoverContent;
            else
                _staffButtonContent = _staffButtonNormalContent;

            if (_staffButtonScale != GameSettings.UI_SCALE)
                PositionAndSizeStaffButtonIcon();

            GUIStates.ShowPersonnelWindow = GUI.Toggle(_staffButtonRect, GUIStates.ShowPersonnelWindow, _staffButtonContent, _staffButtonToggle);
        }
    }
}
