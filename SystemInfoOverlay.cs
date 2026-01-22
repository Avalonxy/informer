using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Configuration;
using System.Management;

namespace Informer
{
    public class SystemInfoOverlay : Form
    {
        private Timer updateTimer;
        private FileSystemWatcher configWatcher;
        private Timer configCheckTimer;
        private DateTime lastConfigWriteTime;
        
        // Кэш для медленных операций
        private string cachedSystemInfo = "";
        private DateTime lastUpdateTime = DateTime.MinValue;
        private readonly object cacheLock = new object();
        private bool isUpdating = false;
        
        // Анимация загрузки
        private Timer loadingAnimationTimer;
        private int loadingDotsCount = 0;
        private int loadingPulseAlpha = 255;
        private bool loadingPulseDirection = false; // false = уменьшение, true = увеличение
        
        // Иконка в трее
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        public SystemInfoOverlay()
        {
            try
            {
                // Загрузка настроек
                ConfigurationManager.RefreshSection("appSettings");
                Settings.LoadSettings();
            }
            catch
            {
                // Если не удалось загрузить настройки, используем значения по умолчанию
            }

            // Настройка формы
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = false;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
            this.StartPosition = FormStartPosition.Manual;
            
            // Проверяем размер окна (должен быть больше 0)
            int width = Settings.WindowWidth > 0 ? Settings.WindowWidth : 300;
            int height = Settings.WindowHeight > 0 ? Settings.WindowHeight : 200;
            this.Size = new Size(width, height);

            // Позиционирование в правом нижнем углу
            Screen screen = Screen.PrimaryScreen;
            this.Location = new Point(
                screen.WorkingArea.Width - width,
                screen.WorkingArea.Height - height
            );
            
            // Явно показываем окно
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;

            // Обработчик отрисовки
            this.Paint += SystemInfoOverlay_Paint;
            
            // Инициализация иконки в трее
            InitializeTrayIcon();

            // Настройка таймера для обновления данных
            updateTimer = new Timer();
            updateTimer.Interval = Settings.UpdateInterval;
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
            
            // Таймер для анимации загрузки
            loadingAnimationTimer = new Timer();
            loadingAnimationTimer.Interval = 300; // Обновление каждые 300мс
            loadingAnimationTimer.Tick += LoadingAnimationTimer_Tick;
            loadingAnimationTimer.Start();

            // FileSystemWatcher для автообновления настроек
            string configPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                try
                {
                    configWatcher = new FileSystemWatcher(Path.GetDirectoryName(configPath), Path.GetFileName(configPath));
                    configWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    configWatcher.Changed += (s, e) =>
                    {
                        // Используем таймер для задержки вместо Thread.Sleep
                        System.Threading.Timer delayTimer = null;
                        delayTimer = new System.Threading.Timer((state) =>
                        {
                            delayTimer?.Dispose();
                            if (this.IsHandleCreated && !this.IsDisposed)
                            {
                                this.BeginInvoke((MethodInvoker)delegate {
                                    try
                                    {
                                        Settings.LoadSettings();
                                        InvalidateCache();
                                        this.Invalidate();
                                    }
                                    catch { }
                                });
                            }
                        }, null, 200, System.Threading.Timeout.Infinite);
                    };
                    configWatcher.EnableRaisingEvents = true;

                    // Таймер для проверки изменений в конфиге (резервный способ)
                    try
                    {
                        lastConfigWriteTime = File.GetLastWriteTime(configPath);
                    }
                    catch
                    {
                        lastConfigWriteTime = DateTime.MinValue;
                    }
                    configCheckTimer = new Timer();
                    configCheckTimer.Interval = 2000; // 2 секунды
                    configCheckTimer.Tick += (s, e) =>
                    {
                        try
                        {
                            if (File.Exists(configPath))
                            {
                                DateTime currentWriteTime = File.GetLastWriteTime(configPath);
                                if (currentWriteTime != lastConfigWriteTime)
                                {
                                    lastConfigWriteTime = currentWriteTime;
                                    ConfigurationManager.RefreshSection("appSettings");
                                    Settings.LoadSettings();
                                    InvalidateCache();
                                    this.Invalidate();
                                }
                            }
                        }
                        catch { }
                    };
                    configCheckTimer.Start();
                }
                catch
                {
                    // Если FileSystemWatcher не работает, оставляем только таймер
                }
            }
            
            // Инициализация кэша при запуске
            UpdateSystemInfoAsync();
            
            // Принудительно обновляем окно для показа "Загрузка..." сразу
            this.Invalidate();
            this.Update(); // Принудительно обновляем окно сразу
            
            // Инициализация экспорта в Aspia при запуске
            AspiaExporter.CheckAndUpdate();
        }

        private void SystemInfoOverlay_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                string systemInfo = GetSystemInfo();
                bool isLoading = string.IsNullOrEmpty(systemInfo);
                if (isLoading)
                {
                    // Если данных нет, показываем анимированную заглушку
                    string dots = new string('.', loadingDotsCount);
                    systemInfo = "Загрузка" + dots;
                }
                string[] lines = systemInfo.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                if (lines.Length == 0 || e == null || e.Graphics == null)
                {
                    // Если нет данных для отрисовки, все равно показываем окно
                    e.Graphics.Clear(Color.Black);
                    return;
                }
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                using (Font font = new Font(Settings.FontName, Settings.FontSize,
                    (Settings.FontBold ? FontStyle.Bold : FontStyle.Regular)
                    | (Settings.FontItalic ? FontStyle.Italic : FontStyle.Regular)
                    | (Settings.FontUnderline ? FontStyle.Underline : FontStyle.Regular)))
                {
                    int rightPadding = 10;
                    float maxWidth = 0;
                    foreach (string line in lines)
                    {
                        SizeF lineSize = e.Graphics.MeasureString(line, font);
                        if (lineSize.Width > maxWidth)
                            maxWidth = lineSize.Width;
                    }
                    float x = this.Width - maxWidth - rightPadding;
                    float y = 10;
                    foreach (string line in lines)
                    {
                        // Тень
                        if (Settings.ShadowEnabled && Settings.ShadowLayers > 0)
                        {
                            Color baseShadowColor = Settings.ShadowColor;
                            if (baseShadowColor.A != Settings.ShadowAlpha)
                                baseShadowColor = Color.FromArgb(Settings.ShadowAlpha, baseShadowColor.R, baseShadowColor.G, baseShadowColor.B);
                            using (SolidBrush shadowBrush = new SolidBrush(baseShadowColor))
                            {
                                for (int i = 1; i <= Settings.ShadowLayers; i++)
                                {
                                    int offset = Settings.ShadowOffset * i;
                                    e.Graphics.DrawString(line, font, shadowBrush, x + offset, y + offset);
                                }
                            }
                        }
                        // Текст (с пульсацией при загрузке)
                        Color textColor = Settings.TextColor;
                        if (isLoading && line.StartsWith("Загрузка"))
                        {
                            // Пульсация прозрачности для эффекта загрузки
                            textColor = Color.FromArgb(loadingPulseAlpha, textColor.R, textColor.G, textColor.B);
                        }
                        using (SolidBrush textBrush = new SolidBrush(textColor))
                        {
                            e.Graphics.DrawString(line, font, textBrush, x, y);
                        }
                        y += font.Height;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при отрисовке: " + ex.Message);
            }
        }

        private void LoadingAnimationTimer_Tick(object sender, EventArgs e)
        {
            // Анимация точек загрузки (0, 1, 2, 3 точки)
            loadingDotsCount = (loadingDotsCount + 1) % 4;
            
            // Пульсация прозрачности (от 180 до 255)
            if (loadingPulseDirection)
            {
                loadingPulseAlpha += 15;
                if (loadingPulseAlpha >= 255)
                {
                    loadingPulseAlpha = 255;
                    loadingPulseDirection = false;
                }
            }
            else
            {
                loadingPulseAlpha -= 15;
                if (loadingPulseAlpha <= 180)
                {
                    loadingPulseAlpha = 180;
                    loadingPulseDirection = true;
                }
            }
            
            // Обновляем окно только если идет загрузка
            lock (cacheLock)
            {
                if (string.IsNullOrEmpty(cachedSystemInfo))
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke((MethodInvoker)delegate { this.Invalidate(); });
                    }
                }
            }
        }
        
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // Обновляем кэш в фоновом потоке
            UpdateSystemInfoAsync();
            
            // Проверяем изменения в сетевых настройках для Aspia
            AspiaExporter.CheckAndUpdate();
            
            this.Invalidate();
        }
        
        private string GetSystemInfo()
        {
            lock (cacheLock)
            {
                // Если кэш свежий (обновлен менее 1 секунды назад), используем его
                if (!string.IsNullOrEmpty(cachedSystemInfo) && 
                    (DateTime.Now - lastUpdateTime).TotalSeconds < 1.0)
                {
                    return cachedSystemInfo;
                }
            }
            
            // Если кэш устарел, строим синхронно (но это должно быть редко)
            return BuildSystemInfo();
        }
        
        private void UpdateSystemInfoAsync()
        {
            // Предотвращаем параллельные обновления
            if (isUpdating) return;
            
            System.Threading.ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    isUpdating = true;
                    string newInfo = BuildSystemInfo();
                    
                    lock (cacheLock)
                    {
                        cachedSystemInfo = newInfo;
                        lastUpdateTime = DateTime.Now;
                    }
                    
                    // Обновляем UI
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke((MethodInvoker)delegate 
                        { 
                            this.Invalidate(); 
                            this.Update(); // Принудительно обновляем окно для показа загрузки
                        });
                    }
                }
                catch { }
                finally
                {
                    isUpdating = false;
                }
            });
        }
        
        private void InvalidateCache()
        {
            lock (cacheLock)
            {
                cachedSystemInfo = "";
                lastUpdateTime = DateTime.MinValue;
            }
        }

        private string BuildSystemInfo()
        {
            StringBuilder sb = new StringBuilder();

            // Имя пользователя и компьютера на отдельных строках
            sb.AppendLine("Пользователь: " + Environment.UserName);
            sb.AppendLine("Компьютер: " + Environment.MachineName);
            
            // Информация о домене
            try
            {
                string domainName = GetDomainName();
                if (domainName != "WORKGROUP" && domainName != Environment.MachineName)
                {
                    sb.AppendLine("Домен: " + domainName);
                    // Имя домен-контроллера (только если в домене)
                    string domainController = GetDomainController();
                    if (domainController != "Не найден" && domainController != "Не удалось определить")
                    {
                        // Форматируем имя длинного контроллера
                        if (domainController.Length > Settings.MaxLineLength)
                        {
                            domainController = domainController.Substring(0, Settings.MaxLineLength - 3) + "...";
                        }
                        sb.AppendLine("Контроллер: " + domainController);
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки при получении информации о домене
            }
            // Версия ОС с разрядностью
            try
            {
                sb.AppendLine(GetOSInfo());
            }
            catch (Exception)
            {
                sb.AppendLine("Windows");
            }
            // Время работы
            try
            {
                TimeSpan uptime = GetSystemUptime();
                sb.AppendLine(string.Format("Время работы: {0}д {1}ч {2}м", 
                            uptime.Days, uptime.Hours, uptime.Minutes));
            }
            catch (Exception)
            {
                sb.AppendLine("Время работы: Нет данных");
            }
            // IP адреса
            sb.AppendLine("Сеть:");
            try
            {
                var ipAddresses = GetIPAddresses();
                if (ipAddresses.Count > 0)
                {
                    foreach (var ip in ipAddresses)
                    {
                        string[] parts = ip.Split(':');
                        if (parts.Length == 2)
                        {
                            string interfaceName = parts[0].Trim();
                            string ipAddress = parts[1].Trim();
                            interfaceName = ShortenAdapterName(interfaceName);
                            sb.AppendLine(string.Format("  {0,-8} {1}", interfaceName + ":", ipAddress));
                        }
                        else
                        {
                            sb.AppendLine("  " + ip);
                        }
                    }
                }
                else
                {
                    sb.AppendLine("  Нет подключений");
                }
            }
            catch (Exception)
            {
                sb.AppendLine("  Ошибка получения IP");
            }
            // Информация о дисках
            sb.AppendLine("Диски:");
            try
            {
                bool disksFound = false;
                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    disksFound = true;
                    try
                    {
                        double totalGB = Math.Round(drive.TotalSize / 1073741824.0, 1);
                        double freeGB = Math.Round(drive.AvailableFreeSpace / 1073741824.0, 1);
                        int usedPercent = (int)Math.Round(100 - ((freeGB / totalGB) * 100));
                        sb.AppendLine(string.Format("  {0,-4} Своб: {1,4:0.0} из {2,4:0.0} ГБ ({3,2}%)", 
                                    drive.Name.TrimEnd('\\'), freeGB, totalGB, usedPercent));
                    }
                    catch
                    {
                        sb.AppendLine("  " + drive.Name.TrimEnd('\\') + " - нет доступа");
                    }
                }
                if (!disksFound)
                {
                    sb.AppendLine("  Нет доступных дисков");
                }
            }
            catch (Exception)
            {
                sb.AppendLine("  Ошибка получения информации");
            }
            return sb.ToString();
        }

        private void UpdateWindowSizeAndPosition()
        {
            try
            {
                using (Font font = new Font(Settings.FontName, Settings.FontSize,
                    (Settings.FontBold ? FontStyle.Bold : FontStyle.Regular)
                    | (Settings.FontItalic ? FontStyle.Italic : FontStyle.Regular)
                    | (Settings.FontUnderline ? FontStyle.Underline : FontStyle.Regular)))
                {
                    // Разбиваем текст на строки
                    string[] lines = BuildSystemInfo().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    
                    // Вычисляем необходимую высоту окна
                    int totalHeight = 20; // Отступ сверху и снизу
                    foreach (string line in lines)
                    {
                        totalHeight += (int)font.Height;
                    }
                    
                    // Устанавливаем размер окна
                    this.Size = new Size(Settings.WindowWidth, totalHeight);
                    
                    // Позиционируем окно в правом нижнем углу
                    Screen screen = Screen.PrimaryScreen;
                    this.Location = new Point(
                        screen.WorkingArea.Width - this.Width,
                        screen.WorkingArea.Height - this.Height
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при обновлении размера окна: " + ex.Message);
            }
        }

        private List<string> GetIPAddresses()
        {
            List<string> addresses = new List<string>();
            
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && 
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                         ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                addresses.Add(string.Format("{0}: {1}", ni.Name, ip.Address));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Добавляем запись об ошибке
                addresses.Add("Ошибка: Не удалось получить IP-адреса");
            }
            
            return addresses;
        }

        private string GetDomainName()
        {
            try
            {
                return Environment.UserDomainName;
            }
            catch (Exception)
            {
                return "Не в домене";
            }
        }

        private string GetDomainController()
        {
            Process process = null;
            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "nltest",
                        Arguments = "/dsgetdc:" + Environment.UserDomainName,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                
                // Используем WaitForExit с таймаутом вместо бесконечного ожидания
                if (!process.WaitForExit(2000)) // 2 секунды максимум
                {
                    try { process.Kill(); } catch { }
                    try { process.WaitForExit(1000); } catch { }
                    return "Таймаут";
                }

                // Поиск имени DC в выводе
                if (output.Contains("DC: \\\\"))
                {
                    int start = output.IndexOf("DC: \\\\") + 5;
                    int end = output.IndexOf('\n', start);
                    if (end > start)
                    {
                        return output.Substring(start, end - start).Trim();
                    }
                }
                return "Не найден";
            }
            catch (Exception)
            {
                return "Не удалось определить";
            }
            finally
            {
                // Освобождаем ресурсы процесса
                if (process != null)
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch { }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }

        private string GetOSInfo()
        {
            try
            {
                string productName = "";
                string buildNumber = "";
                string displayVersion = "";
                string osArch = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                
                // Сначала пытаемся получить правильное название через WMI (как systeminfo)
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Caption, BuildNumber FROM Win32_OperatingSystem"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string caption = obj["Caption"]?.ToString() ?? "";
                            string wmiBuild = obj["BuildNumber"]?.ToString() ?? "";
                            
                            if (!string.IsNullOrEmpty(caption))
                            {
                                // WMI Caption обычно правильный (например, "Microsoft Windows 11 Pro")
                                if (caption.Contains("Windows 11"))
                                {
                                    productName = "Windows 11";
                                }
                                else if (caption.Contains("Windows 10"))
                                {
                                    productName = "Windows 10";
                                }
                                else
                                {
                                    // Извлекаем версию из Caption
                                    productName = caption.Replace("Microsoft ", "").Trim();
                                }
                            }
                            
                            if (!string.IsNullOrEmpty(wmiBuild))
                            {
                                buildNumber = wmiBuild;
                            }
                            break; // Берем первый результат
                        }
                    }
                }
                catch { } // Если WMI не работает, используем реестр
                
                // Если WMI не дал результат, читаем из реестра
                if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(buildNumber))
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        if (key != null)
                        {
                            if (string.IsNullOrEmpty(productName))
                                productName = key.GetValue("ProductName") as string ?? "";
                            if (string.IsNullOrEmpty(buildNumber))
                                buildNumber = key.GetValue("CurrentBuildNumber") as string ?? "";
                            displayVersion = key.GetValue("DisplayVersion") as string ?? "";
                        }
                        else
                        {
                            return Environment.OSVersion.VersionString;
                        }
                    }
                }
                else
                {
                    // Получаем DisplayVersion из реестра для отображения версии (например, 25H2)
                    try
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                        {
                            if (key != null)
                            {
                                displayVersion = key.GetValue("DisplayVersion") as string ?? "";
                            }
                        }
                    }
                    catch { }
                }

                // Определяем версию Windows по номеру сборки (Windows 11 имеет build >= 22000)
                // Это приоритетнее, чем название из реестра
                int buildNum = 0;
                if (!string.IsNullOrEmpty(buildNumber) && int.TryParse(buildNumber, out buildNum))
                {
                    if (buildNum >= 22000)
                    {
                        productName = "Windows 11";
                    }
                    else if (buildNum >= 10240 && buildNum < 22000)
                    {
                        productName = "Windows 10";
                    }
                }
                
                // Если все еще не определили, используем fallback
                if (string.IsNullOrEmpty(productName))
                {
                    productName = "Windows";
                }
                
                // Создаем компактное представление
                StringBuilder osInfo = new StringBuilder();
                
                // Строка 1: Название Windows, версия и разрядность
                osInfo.Append(productName);
                if (!string.IsNullOrEmpty(displayVersion))
                    osInfo.Append(" " + displayVersion);
                osInfo.AppendLine(" (" + osArch + ")");
                
                // Строка 2: Номер сборки
                osInfo.AppendLine("Сборка: " + buildNumber);
                
                return osInfo.ToString().TrimEnd();
            }
            catch (Exception)
            {
                return Environment.OSVersion.VersionString;
            }
        }

        private TimeSpan GetSystemUptime()
        {
            try
            {
                // Используем WMI для получения реального времени работы системы
                using (var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string lastBootUpTime = obj["LastBootUpTime"]?.ToString();
                        if (!string.IsNullOrEmpty(lastBootUpTime))
                        {
                            DateTime lastBoot = ManagementDateTimeConverter.ToDateTime(lastBootUpTime);
                            return DateTime.Now - lastBoot;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback к Environment.TickCount (время работы процесса, но лучше чем ничего)
                long tickCount = Environment.TickCount;
                if (tickCount < 0)
                    tickCount = int.MaxValue + (uint)tickCount;
                return TimeSpan.FromMilliseconds(tickCount);
            }
            return TimeSpan.Zero;
        }

        // Вспомогательный метод для сокращения имени сетевого адаптера
        private string ShortenAdapterName(string adapterName)
        {
            // Простые замены для известных адаптеров
            if (adapterName.Contains("Ethernet"))
                return "LAN";
            else if (adapterName.Contains("Wi-Fi") || adapterName.Contains("Wireless"))
                return "Wi-Fi";
            else if (adapterName.Contains("Bluetooth"))
                return "BT";
            else if (adapterName.Contains("Virtual") || adapterName.Contains("VPN"))
                return "VPN";
            else if (adapterName.Contains("Loopback") || adapterName.Contains("localhost"))
                return "Loop";
            else if (adapterName.Contains("Microsoft") && adapterName.Contains("Adapter"))
                return "MS";
            else if (adapterName.Contains("VMware"))
                return "VM";
            else if (adapterName.Contains("Hyper-V"))
                return "HV";
            else if (adapterName.Contains("Ancillary"))
                return "Anc";
            
            // Обработка длинных имен - более агрессивное сокращение
            if (adapterName.Length > 15)
            {
                // Проверяем наличие спецсимволов
                int specialCharIndex = adapterName.IndexOfAny(new[] { '-', '_', '#', '(', '[' });
                if (specialCharIndex > 0 && specialCharIndex < 10)
                {
                    // Берем только часть до спецсимвола
                    return adapterName.Substring(0, specialCharIndex);
                }
                
                // Извлечь первые 6 символов
                return adapterName.Substring(0, 6) + "...";
            }
            
            return adapterName;
        }
        
        /// <summary>
        /// Инициализация иконки в системном трее
        /// </summary>
        private void InitializeTrayIcon()
        {
            try
            {
                // Создаем контекстное меню
                trayMenu = new ContextMenuStrip();
                
                // Пункт "Открыть конфиг"
                ToolStripMenuItem openConfigItem = new ToolStripMenuItem("Открыть конфиг");
                openConfigItem.Click += OpenConfigItem_Click;
                trayMenu.Items.Add(openConfigItem);
                
                // Пункт "Настройки" (будет форма настроек)
                ToolStripMenuItem settingsItem = new ToolStripMenuItem("Настройки");
                settingsItem.Click += SettingsItem_Click;
                trayMenu.Items.Add(settingsItem);
                
                trayMenu.Items.Add(new ToolStripSeparator());
                
                // Пункт "Закрыть"
                ToolStripMenuItem exitItem = new ToolStripMenuItem("Закрыть");
                exitItem.Click += ExitItem_Click;
                trayMenu.Items.Add(exitItem);
                
                // Создаем иконку в трее
                trayIcon = new NotifyIcon();
                trayIcon.Text = "Informer - Системная информация";
                trayIcon.Icon = GetTrayIcon();
                trayIcon.ContextMenuStrip = trayMenu;
                trayIcon.Visible = true;
                trayIcon.DoubleClick += TrayIcon_DoubleClick;
            }
            catch
            {
                // Игнорируем ошибки при создании иконки
            }
        }
        
        /// <summary>
        /// Получает иконку для трея (используем иконку приложения или создаем простую)
        /// </summary>
        private Icon GetTrayIcon()
        {
            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "Informer.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
            catch { }
            
            // Если иконка не найдена, используем системную иконку приложения
            return SystemIcons.Application;
        }
        
        /// <summary>
        /// Обработчик двойного клика по иконке в трее
        /// </summary>
        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            // При двойном клике можно показать/скрыть окно или открыть настройки
            // Пока просто открываем настройки
            ShowSettingsForm();
        }
        
        /// <summary>
        /// Обработчик клика "Открыть конфиг"
        /// </summary>
        private void OpenConfigItem_Click(object sender, EventArgs e)
        {
            try
            {
                string configPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                if (File.Exists(configPath))
                {
                    Process.Start("notepad.exe", configPath);
                }
                else
                {
                    MessageBox.Show("Файл конфигурации не найден:\n" + configPath, 
                        "Informer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при открытии конфига:\n" + ex.Message, 
                    "Informer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Обработчик клика "Настройки"
        /// </summary>
        private void SettingsItem_Click(object sender, EventArgs e)
        {
            ShowSettingsForm();
        }
        
        /// <summary>
        /// Показать форму настроек
        /// </summary>
        private void ShowSettingsForm()
        {
            try
            {
                using (SettingsForm settingsForm = new SettingsForm())
                {
                    if (settingsForm.ShowDialog() == DialogResult.OK)
                    {
                        // Настройки сохранены, обновляем кэш и перерисовываем
                        InvalidateCache();
                        this.Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при открытии формы настроек:\n" + ex.Message, 
                    "Informer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Обработчик клика "Закрыть"
        /// </summary>
        private void ExitItem_Click(object sender, EventArgs e)
        {
            // Скрываем иконку перед закрытием
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
            }
            Application.Exit();
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Останавливаем и освобождаем таймеры
                if (updateTimer != null)
                {
                    updateTimer.Stop();
                    updateTimer.Dispose();
                    updateTimer = null;
                }
                
                if (configCheckTimer != null)
                {
                    configCheckTimer.Stop();
                    configCheckTimer.Dispose();
                    configCheckTimer = null;
                }
                
                if (loadingAnimationTimer != null)
                {
                    loadingAnimationTimer.Stop();
                    loadingAnimationTimer.Dispose();
                    loadingAnimationTimer = null;
                }
                
                // Освобождаем FileSystemWatcher
                if (configWatcher != null)
                {
                    configWatcher.EnableRaisingEvents = false;
                    configWatcher.Dispose();
                    configWatcher = null;
                }
                
                // Освобождаем иконку в трее
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    trayIcon = null;
                }
                
                if (trayMenu != null)
                {
                    trayMenu.Dispose();
                    trayMenu = null;
                }
            }
            base.Dispose(disposing);
        }
    }
} 