using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Configuration;
using System.Xml;

namespace Informer
{
    [DesignerCategory("Code")]
    public class SettingsForm : Form
    {
        private TextBox txtWindowWidth;
        private TextBox txtWindowHeight;
        private TextBox txtFontName;
        private TextBox txtFontSize;
        private CheckBox chkFontBold;
        private CheckBox chkFontItalic;
        private CheckBox chkFontUnderline;
        private TextBox txtTextColor;
        private CheckBox chkShadowEnabled;
        private TextBox txtShadowColor;
        private TextBox txtShadowAlpha;
        private TextBox txtUpdateInterval;
        
        // Настройки Aspia
        private CheckBox chkAspiaEnabled;
        private TextBox txtAspiaNetworkPath;
        private TextBox txtAspiaNetworkSubnets;
        
        private Button btnSave;
        private Button btnCancel;
        private Button btnTextColorPicker;
        private Button btnShadowColorPicker;
        
        private string configFilePath;
        
        public SettingsForm()
        {
            InitializeComponent();
            
            // Убеждаемся, что настройки загружены
            try
            {
                ConfigurationManager.RefreshSection("appSettings");
                Settings.LoadSettings();
            }
            catch (Exception ex)
            {
                // Если не удалось загрузить, сбрасываем на значения по умолчанию
                try
                {
                    Settings.ResetToDefaults();
                }
                catch
                {
                    // Игнорируем ошибки сброса
                }
            }
            
            // Загружаем настройки в форму только после полной инициализации
            if (this.IsHandleCreated)
            {
                LoadSettings();
            }
            else
            {
                this.HandleCreated += (s, e) => LoadSettings();
            }
        }
        
        private void InitializeComponent()
        {
            this.Text = "Настройки Informer";
            this.Size = new Size(600, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            configFilePath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            
            int yPos = 10;
            int labelWidth = 200;
            int controlWidth = 300;
            int spacing = 30;
            
            // Заголовок "Основные настройки"
            Label lblMainSettings = new Label();
            lblMainSettings.Text = "Основные настройки";
            lblMainSettings.Font = new Font("Arial", 10, FontStyle.Bold);
            lblMainSettings.Location = new Point(10, yPos);
            lblMainSettings.Size = new Size(400, 20);
            this.Controls.Add(lblMainSettings);
            yPos += 25;
            
            // Размер окна
            AddLabeledControl("Ширина окна:", 10, yPos, labelWidth, out Label lblWidth);
            txtWindowWidth = new TextBox();
            txtWindowWidth.Location = new Point(220, yPos);
            txtWindowWidth.Size = new Size(controlWidth, 23);
            this.Controls.Add(txtWindowWidth);
            yPos += spacing;
            
            AddLabeledControl("Высота окна:", 10, yPos, labelWidth, out Label lblHeight);
            txtWindowHeight = new TextBox();
            txtWindowHeight.Location = new Point(220, yPos);
            txtWindowHeight.Size = new Size(controlWidth, 23);
            this.Controls.Add(txtWindowHeight);
            yPos += spacing;
            
            // Настройки шрифта
            AddLabeledControl("Имя шрифта:", 10, yPos, labelWidth, out Label lblFontName);
            txtFontName = new TextBox();
            txtFontName.Location = new Point(220, yPos);
            txtFontName.Size = new Size(controlWidth, 23);
            this.Controls.Add(txtFontName);
            yPos += spacing;
            
            AddLabeledControl("Размер шрифта:", 10, yPos, labelWidth, out Label lblFontSize);
            txtFontSize = new TextBox();
            txtFontSize.Location = new Point(220, yPos);
            txtFontSize.Size = new Size(controlWidth, 23);
            this.Controls.Add(txtFontSize);
            yPos += spacing;
            
            chkFontBold = new CheckBox();
            chkFontBold.Text = "Жирный";
            chkFontBold.Location = new Point(220, yPos);
            chkFontBold.Size = new Size(100, 23);
            this.Controls.Add(chkFontBold);
            
            chkFontItalic = new CheckBox();
            chkFontItalic.Text = "Курсив";
            chkFontItalic.Location = new Point(330, yPos);
            chkFontItalic.Size = new Size(100, 23);
            this.Controls.Add(chkFontItalic);
            
            chkFontUnderline = new CheckBox();
            chkFontUnderline.Text = "Подчеркнутый";
            chkFontUnderline.Location = new Point(440, yPos);
            chkFontUnderline.Size = new Size(120, 23);
            this.Controls.Add(chkFontUnderline);
            yPos += spacing;
            
            // Цвет текста
            AddLabeledControl("Цвет текста:", 10, yPos, labelWidth, out Label lblTextColor);
            txtTextColor = new TextBox();
            txtTextColor.Location = new Point(220, yPos);
            txtTextColor.Size = new Size(200, 23);
            this.Controls.Add(txtTextColor);
            
            btnTextColorPicker = new Button();
            btnTextColorPicker.Text = "...";
            btnTextColorPicker.Location = new Point(430, yPos);
            btnTextColorPicker.Size = new Size(40, 23);
            btnTextColorPicker.Click += BtnTextColorPicker_Click;
            this.Controls.Add(btnTextColorPicker);
            yPos += spacing;
            
            // Тень
            chkShadowEnabled = new CheckBox();
            chkShadowEnabled.Text = "Включить тень";
            chkShadowEnabled.Location = new Point(10, yPos);
            chkShadowEnabled.Size = new Size(200, 23);
            this.Controls.Add(chkShadowEnabled);
            yPos += spacing;
            
            AddLabeledControl("Цвет тени:", 10, yPos, labelWidth, out Label lblShadowColor);
            txtShadowColor = new TextBox();
            txtShadowColor.Location = new Point(220, yPos);
            txtShadowColor.Size = new Size(200, 23);
            this.Controls.Add(txtShadowColor);
            
            btnShadowColorPicker = new Button();
            btnShadowColorPicker.Text = "...";
            btnShadowColorPicker.Location = new Point(430, yPos);
            btnShadowColorPicker.Size = new Size(40, 23);
            btnShadowColorPicker.Click += BtnShadowColorPicker_Click;
            this.Controls.Add(btnShadowColorPicker);
            yPos += spacing;
            
            AddLabeledControl("Прозрачность тени:", 10, yPos, labelWidth, out Label lblShadowAlpha);
            txtShadowAlpha = new TextBox();
            txtShadowAlpha.Location = new Point(220, yPos);
            txtShadowAlpha.Size = new Size(controlWidth, 23);
            this.Controls.Add(txtShadowAlpha);
            yPos += spacing;
            
            AddLabeledControl("Интервал обновления (мс):", 10, yPos, labelWidth, out Label lblUpdateInterval);
            txtUpdateInterval = new TextBox();
            txtUpdateInterval.Location = new Point(220, yPos);
            txtUpdateInterval.Size = new Size(controlWidth, 23);
            this.Controls.Add(txtUpdateInterval);
            yPos += 40;
            
            // Заголовок "Настройки Aspia"
            Label lblAspiaSettings = new Label();
            lblAspiaSettings.Text = "Настройки экспорта в Aspia";
            lblAspiaSettings.Font = new Font("Arial", 10, FontStyle.Bold);
            lblAspiaSettings.Location = new Point(10, yPos);
            lblAspiaSettings.Size = new Size(400, 20);
            this.Controls.Add(lblAspiaSettings);
            yPos += 25;
            
            chkAspiaEnabled = new CheckBox();
            chkAspiaEnabled.Text = "Включить экспорт в Aspia";
            chkAspiaEnabled.Location = new Point(10, yPos);
            chkAspiaEnabled.Size = new Size(200, 23);
            this.Controls.Add(chkAspiaEnabled);
            yPos += spacing;
            
            AddLabeledControl("Путь к JSON файлу:", 10, yPos, labelWidth, out Label lblAspiaPath);
            txtAspiaNetworkPath = new TextBox();
            txtAspiaNetworkPath.Location = new Point(220, yPos);
            txtAspiaNetworkPath.Size = new Size(controlWidth, 23);
            this.Controls.Add(txtAspiaNetworkPath);
            yPos += spacing;
            
            AddLabeledControl("Подсети (через запятую):", 10, yPos, labelWidth, out Label lblSubnets);
            txtAspiaNetworkSubnets = new TextBox();
            txtAspiaNetworkSubnets.Location = new Point(220, yPos);
            txtAspiaNetworkSubnets.Size = new Size(controlWidth, 23);
            txtAspiaNetworkSubnets.Multiline = false;
            this.Controls.Add(txtAspiaNetworkSubnets);
            yPos += 50;
            
            // Кнопки
            btnSave = new Button();
            btnSave.Text = "Сохранить";
            btnSave.Location = new Point(350, yPos);
            btnSave.Size = new Size(100, 30);
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
            
            btnCancel = new Button();
            btnCancel.Text = "Отмена";
            btnCancel.Location = new Point(460, yPos);
            btnCancel.Size = new Size(100, 30);
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);
        }
        
        private void AddLabeledControl(string labelText, int x, int y, int width, out Label label)
        {
            label = new Label();
            label.Text = labelText;
            label.Location = new Point(x, y);
            label.Size = new Size(width, 23);
            label.TextAlign = ContentAlignment.MiddleLeft;
            this.Controls.Add(label);
        }
        
        private void LoadSettings()
        {
            try
            {
                // Проверяем, что все элементы формы инициализированы
                if (txtWindowWidth == null || txtWindowHeight == null || txtFontName == null || 
                    txtFontSize == null || chkFontBold == null || chkFontItalic == null || 
                    chkFontUnderline == null || txtTextColor == null || chkShadowEnabled == null ||
                    txtShadowColor == null || txtShadowAlpha == null || txtUpdateInterval == null ||
                    chkAspiaEnabled == null || txtAspiaNetworkPath == null || txtAspiaNetworkSubnets == null)
                {
                    return; // Форма еще не полностью инициализирована
                }
                
                txtWindowWidth.Text = Settings.WindowWidth.ToString();
                txtWindowHeight.Text = Settings.WindowHeight.ToString();
                txtFontName.Text = Settings.FontName ?? "";
                txtFontSize.Text = Settings.FontSize.ToString();
                chkFontBold.Checked = Settings.FontBold;
                chkFontItalic.Checked = Settings.FontItalic;
                chkFontUnderline.Checked = Settings.FontUnderline;
                
                // Цвет текста - показываем имя или ARGB
                try
                {
                    Color textColor = Settings.TextColor;
                    if (textColor.IsKnownColor)
                    {
                        txtTextColor.Text = textColor.Name;
                    }
                    else
                    {
                        txtTextColor.Text = $"{textColor.A},{textColor.R},{textColor.G},{textColor.B}";
                    }
                }
                catch
                {
                    txtTextColor.Text = "White";
                }
                
                chkShadowEnabled.Checked = Settings.ShadowEnabled;
                
                // Цвет тени - показываем ARGB формат
                try
                {
                    Color shadowColor = Settings.ShadowColor;
                    txtShadowColor.Text = $"{shadowColor.A},{shadowColor.R},{shadowColor.G},{shadowColor.B}";
                }
                catch
                {
                    txtShadowColor.Text = "128,0,0,0";
                }
                
                txtShadowAlpha.Text = Settings.ShadowAlpha.ToString();
                txtUpdateInterval.Text = Settings.UpdateInterval.ToString();
                
                chkAspiaEnabled.Checked = Settings.AspiaEnabled;
                txtAspiaNetworkPath.Text = Settings.AspiaNetworkPath ?? "";
                txtAspiaNetworkSubnets.Text = Settings.AspiaNetworkSubnets != null ? string.Join(",", Settings.AspiaNetworkSubnets) : "";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке настроек: " + ex.Message + "\n\n" + ex.StackTrace, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnTextColorPicker_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            try
            {
                // Пытаемся распарсить цвет из текстового поля
                if (!string.IsNullOrEmpty(txtTextColor.Text))
                {
                    if (txtTextColor.Text.Contains(","))
                    {
                        string[] parts = txtTextColor.Text.Split(',');
                        if (parts.Length == 4 && int.TryParse(parts[0], out int a) && 
                            int.TryParse(parts[1], out int r) && int.TryParse(parts[2], out int g) && 
                            int.TryParse(parts[3], out int b))
                        {
                            colorDialog.Color = Color.FromArgb(a, r, g, b);
                        }
                    }
                    else
                    {
                        colorDialog.Color = Color.FromName(txtTextColor.Text);
                    }
                }
                else
                {
                    colorDialog.Color = Settings.TextColor;
                }
            }
            catch
            {
                colorDialog.Color = Settings.TextColor;
            }
            
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                if (colorDialog.Color.IsKnownColor)
                {
                    txtTextColor.Text = colorDialog.Color.Name;
                }
                else
                {
                    txtTextColor.Text = $"{colorDialog.Color.A},{colorDialog.Color.R},{colorDialog.Color.G},{colorDialog.Color.B}";
                }
            }
        }
        
        private void BtnShadowColorPicker_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            try
            {
                // Пытаемся распарсить цвет из текстового поля
                if (!string.IsNullOrEmpty(txtShadowColor.Text))
                {
                    if (txtShadowColor.Text.Contains(","))
                    {
                        string[] parts = txtShadowColor.Text.Split(',');
                        if (parts.Length == 4 && int.TryParse(parts[0], out int a) && 
                            int.TryParse(parts[1], out int r) && int.TryParse(parts[2], out int g) && 
                            int.TryParse(parts[3], out int b))
                        {
                            colorDialog.Color = Color.FromArgb(a, r, g, b);
                        }
                    }
                    else
                    {
                        colorDialog.Color = Color.FromName(txtShadowColor.Text);
                    }
                }
                else
                {
                    colorDialog.Color = Settings.ShadowColor;
                }
            }
            catch
            {
                colorDialog.Color = Settings.ShadowColor;
            }
            
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                txtShadowColor.Text = $"{colorDialog.Color.A},{colorDialog.Color.R},{colorDialog.Color.G},{colorDialog.Color.B}";
            }
        }
        
        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                SaveSettings();
                MessageBox.Show("Настройки сохранены. Изменения применятся автоматически.", 
                    "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении настроек: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
        
        private void SaveSettings()
        {
            if (!File.Exists(configFilePath))
            {
                throw new FileNotFoundException("Файл конфигурации не найден: " + configFilePath);
            }
            
            XmlDocument doc = new XmlDocument();
            doc.Load(configFilePath);
            
            XmlNode appSettingsNode = doc.SelectSingleNode("//appSettings");
            if (appSettingsNode == null)
            {
                throw new Exception("Не найден узел appSettings в конфигурации");
            }
            
            // Функция для обновления или добавления ключа
            Action<string, string> setKey = (key, value) =>
            {
                XmlNode node = appSettingsNode.SelectSingleNode($"add[@key='{key}']");
                if (node != null)
                {
                    XmlAttribute valueAttr = node.Attributes["value"];
                    if (valueAttr != null)
                    {
                        valueAttr.Value = value;
                    }
                    else
                    {
                        valueAttr = doc.CreateAttribute("value");
                        valueAttr.Value = value;
                        node.Attributes.Append(valueAttr);
                    }
                }
                else
                {
                    XmlElement newElement = doc.CreateElement("add");
                    XmlAttribute keyAttr = doc.CreateAttribute("key");
                    keyAttr.Value = key;
                    XmlAttribute valAttr = doc.CreateAttribute("value");
                    valAttr.Value = value;
                    newElement.Attributes.Append(keyAttr);
                    newElement.Attributes.Append(valAttr);
                    appSettingsNode.AppendChild(newElement);
                }
            };
            
            // Сохраняем настройки
            setKey("WindowWidth", txtWindowWidth.Text);
            setKey("WindowHeight", txtWindowHeight.Text);
            setKey("FontName", txtFontName.Text);
            setKey("FontSize", txtFontSize.Text);
            setKey("FontBold", chkFontBold.Checked.ToString().ToLower());
            setKey("FontItalic", chkFontItalic.Checked.ToString().ToLower());
            setKey("FontUnderline", chkFontUnderline.Checked.ToString().ToLower());
            
            // Цвет текста - сохраняем как имя цвета или ARGB
            if (txtTextColor.Text.Contains(","))
            {
                // Уже в формате ARGB
                setKey("TextColor", txtTextColor.Text);
            }
            else
            {
                // Имя цвета
                setKey("TextColor", txtTextColor.Text);
            }
            
            setKey("ShadowEnabled", chkShadowEnabled.Checked.ToString().ToLower());
            
            // Цвет тени - сохраняем как ARGB
            setKey("ShadowColor", txtShadowColor.Text);
            
            setKey("ShadowAlpha", txtShadowAlpha.Text);
            setKey("UpdateInterval", txtUpdateInterval.Text);
            
            // Настройки Aspia
            setKey("AspiaEnabled", chkAspiaEnabled.Checked.ToString().ToLower());
            setKey("AspiaNetworkPath", txtAspiaNetworkPath.Text);
            setKey("AspiaNetworkSubnets", txtAspiaNetworkSubnets.Text);
            
            // Сохраняем файл
            doc.Save(configFilePath);
            
            // Обновляем настройки в памяти
            ConfigurationManager.RefreshSection("appSettings");
            Settings.LoadSettings();
        }
    }
}
