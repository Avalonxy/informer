using System;
using System.Drawing;
using System.Configuration;
using System.Windows.Forms;
using System.Linq;

namespace Informer
{
    public static class Settings
    {
        // Настройки по умолчанию
        private static readonly Color defaultTextColor = Color.White;
        private static readonly Color defaultShadowColor = Color.FromArgb(128, 0, 0, 0);
        private static readonly float defaultFontSize = 32f;
        private static readonly string defaultFontName = "Arial";
        private static readonly int defaultWindowWidth = 600;
        private static readonly int defaultWindowHeight = 400;
        private static readonly int defaultUpdateInterval = 7200000;
        private static readonly int defaultDiskUpdateInterval = 7200000;
        private static readonly int defaultMaxLineLength = 60;
        private static readonly int defaultShadowOffset = 4;
        private static readonly bool defaultShadowEnabled = true;
        private static readonly int defaultShadowLayers = 1;
        private static readonly int defaultShadowAlpha = 128;
        
        // Свойства настроек
        public static Color TextColor { get; private set; }
        public static Color ShadowColor { get; private set; }
        public static float FontSize { get; private set; }
        public static string FontName { get; private set; }
        public static int WindowWidth { get; private set; }
        public static int WindowHeight { get; private set; }
        public static int UpdateInterval { get; private set; }
        public static int DiskUpdateInterval { get; private set; }
        public static int MaxLineLength { get; private set; }
        public static bool FontBold { get; private set; }
        public static bool FontItalic { get; private set; }
        public static bool FontUnderline { get; private set; }
        public static int ShadowOffset { get; private set; }
        public static bool ShadowEnabled { get; private set; }
        public static int ShadowLayers { get; private set; }
        public static int ShadowAlpha { get; private set; }
        
        // Настройки Aspia
        public static bool AspiaEnabled { get; private set; }
        public static string AspiaNetworkPath { get; private set; }
        public static string[] AspiaNetworkSubnets { get; private set; }
        
        // Загрузка настроек из конфигурационного файла
        public static void LoadSettings()
        {
            try
            {
                // Загрузка размеров окна
                WindowWidth = GetIntSetting("WindowWidth", defaultWindowWidth);
                WindowHeight = GetIntSetting("WindowHeight", defaultWindowHeight);
                UpdateInterval = GetIntSetting("UpdateInterval", defaultUpdateInterval);
                DiskUpdateInterval = GetIntSetting("DiskUpdateInterval", defaultDiskUpdateInterval);
                MaxLineLength = GetIntSetting("MaxLineLength", defaultMaxLineLength);

                // Загрузка настроек шрифта
                FontName = GetStringSetting("FontName", defaultFontName);
                FontSize = GetFloatSetting("FontSize", defaultFontSize);
                FontBold = GetBoolSetting("FontBold", false);
                FontItalic = GetBoolSetting("FontItalic", false);
                FontUnderline = GetBoolSetting("FontUnderline", false);

                // Загрузка цветов
                TextColor = GetColorSetting("TextColor", defaultTextColor);
                ShadowEnabled = GetBoolSetting("ShadowEnabled", defaultShadowEnabled);
                ShadowColor = GetColorSetting("ShadowColor", defaultShadowColor);
                ShadowAlpha = GetIntSetting("ShadowAlpha", defaultShadowAlpha);
                ShadowOffset = GetIntSetting("ShadowOffset", defaultShadowOffset);
                ShadowLayers = GetIntSetting("ShadowLayers", defaultShadowLayers);

                // Загрузка настроек Aspia
                AspiaEnabled = GetBoolSetting("AspiaEnabled", false);
                AspiaNetworkPath = GetStringSetting("AspiaNetworkPath", "");
                string subnetsString = GetStringSetting("AspiaNetworkSubnets", "");
                if (!string.IsNullOrEmpty(subnetsString))
                {
                    AspiaNetworkSubnets = subnetsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
                }
                else
                {
                    AspiaNetworkSubnets = null;
                }

                // Проверка валидности значений
                ValidateSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при загрузке настроек: " + ex.Message);
                ResetToDefaults();
            }
        }

        private static void ValidateSettings()
        {
            if (FontSize <= 0 || FontSize > 72)
            {
                Console.WriteLine("Некорректный размер шрифта (" + FontSize + "), сброс на значение по умолчанию: " + defaultFontSize);
                FontSize = defaultFontSize;
            }

            if (string.IsNullOrEmpty(FontName))
                FontName = defaultFontName;

            // Проверка существования шрифта
            try
            {
                using (Font testFont = new Font(FontName, FontSize))
                {
                    if (testFont.Name != FontName)
                    {
                        Console.WriteLine("Шрифт " + FontName + " не найден, используем шрифт по умолчанию: " + defaultFontName);
                        FontName = defaultFontName;
                    }
                }
            }
            catch (Exception)
            {
                FontName = defaultFontName;
            }
        }

        private static int GetIntSetting(string key, int defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (int.TryParse(value, out int result))
                return result;
            return defaultValue;
        }

        private static float GetFloatSetting(string key, float defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (float.TryParse(value, out float result))
                return result;
            return defaultValue;
        }

        private static string GetStringSetting(string key, string defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private static Color GetColorSetting(string key, Color defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            try
            {
                if (value.Contains(","))
                {
                    string[] parts = value.Split(',');
                    if (parts.Length == 4)
                    {
                        return Color.FromArgb(
                            int.Parse(parts[0]),
                            int.Parse(parts[1]),
                            int.Parse(parts[2]),
                            int.Parse(parts[3])
                        );
                    }
                }
                return Color.FromName(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static bool GetBoolSetting(string key, bool defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return value.Trim().ToLower() == "true" || value.Trim() == "1";
        }

        // Сброс настроек к значениям по умолчанию
        public static void ResetToDefaults()
        {
            TextColor = defaultTextColor;
            ShadowEnabled = defaultShadowEnabled;
            ShadowColor = defaultShadowColor;
            ShadowAlpha = defaultShadowAlpha;
            ShadowOffset = defaultShadowOffset;
            ShadowLayers = defaultShadowLayers;
            FontSize = defaultFontSize;
            FontName = defaultFontName;
            FontBold = false;
            FontItalic = false;
            FontUnderline = false;
            WindowWidth = defaultWindowWidth;
            WindowHeight = defaultWindowHeight;
            UpdateInterval = defaultUpdateInterval;
            DiskUpdateInterval = defaultDiskUpdateInterval;
            MaxLineLength = defaultMaxLineLength;
            AspiaEnabled = false;
            AspiaNetworkPath = "";
            AspiaNetworkSubnets = null;
        }
    }
} 