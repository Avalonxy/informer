using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace Informer
{
    /// <summary>
    /// Экспорт данных для Aspia - сохранение информации о пользователе и IP адресе в JSON файл на сетевом диске
    /// </summary>
    public static class AspiaExporter
    {
        private static string lastUserName = "";
        private static string lastIpAddress = "";
        private static readonly object exportLock = new object();
        private static bool isExporting = false;

        /// <summary>
        /// Проверяет изменения в сетевых настройках и обновляет файл Aspia при необходимости
        /// </summary>
        public static void CheckAndUpdate()
        {
            // Проверяем включен ли экспорт
            if (!Settings.AspiaEnabled || string.IsNullOrEmpty(Settings.AspiaNetworkPath))
                return;

            // Проверяем, не идет ли уже экспорт
            if (isExporting)
                return;

            // Получаем текущие данные
            string currentUser = Environment.UserName;
            string currentIp = GetPrimaryIpAddress();

            // Проверяем изменения или первый запуск
            bool isFirstRun = string.IsNullOrEmpty(lastUserName);
            bool hasChanges = currentUser != lastUserName || currentIp != lastIpAddress;

            if (isFirstRun || hasChanges)
            {
                // Обновляем кэш
                lastUserName = currentUser;
                lastIpAddress = currentIp;

                // Экспортируем асинхронно
                System.Threading.ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        isExporting = true;
                        ExportToAspia(currentUser, Environment.MachineName, currentIp);
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку для диагностики (только в Debug режиме)
                        #if DEBUG
                        System.Diagnostics.Debug.WriteLine("AspiaExporter error: " + ex.Message);
                        #endif
                    }
                    finally
                    {
                        isExporting = false;
                    }
                });
            }
        }

        /// <summary>
        /// Получает основной IP адрес из настроенных подсетей (первый активный IPv4)
        /// </summary>
        private static string GetPrimaryIpAddress()
        {
            try
            {
                // Получаем список подсетей из настроек
                string[] allowedSubnets = Settings.AspiaNetworkSubnets;
                
                // Если подсети не настроены в конфиге, возвращаем 0.0.0.0
                if (allowedSubnets == null || allowedSubnets.Length == 0)
                {
                    return "0.0.0.0";
                }
                
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
                                string ipString = ip.Address.ToString();
                                // Фильтруем только адреса из настроенных подсетей
                                foreach (string subnet in allowedSubnets)
                                {
                                    if (ipString.StartsWith(subnet))
                                    {
                                        return ipString;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
            return "0.0.0.0";
        }

        /// <summary>
        /// Экспортирует данные в JSON файл Aspia с умным обновлением существующих записей
        /// </summary>
        private static void ExportToAspia(string userName, string computerName, string ipAddress)
        {
            try
            {
                // Пытаемся обновить существующий файл или создать новый
                string json = UpdateOrCreateJson(computerName, userName, ipAddress, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));

                // Сохраняем на сетевой диск
                SaveToNetworkFile(json);
            }
            catch
            {
                // Игнорируем ошибки при экспорте
            }
        }

        /// <summary>
        /// Обновляет существующий JSON файл или создает новый, добавляя/обновляя запись текущего компьютера
        /// </summary>
        private static string UpdateOrCreateJson(string computerName, string userName, string ipAddress, string lastUpdate)
        {
            try
            {
                string filePath = Settings.AspiaNetworkPath;
                
                // Пытаемся прочитать существующий файл
                if (File.Exists(filePath))
                {
                    try
                    {
                        // Пробуем прочитать существующий JSON
                        string existingJson = File.ReadAllText(filePath, Encoding.UTF8);
                        
                        // Парсим и обновляем запись текущего компьютера
                        string updatedJson = UpdateComputerInJson(existingJson, computerName, userName, ipAddress, lastUpdate);
                        
                        if (updatedJson != null)
                        {
                            return updatedJson;
                        }
                    }
                    catch (IOException)
                    {
                        // Файл открыт другим процессом - пропускаем обновление
                        return null;
                    }
                    catch
                    {
                        // Ошибка парсинга или другой проблемы - создаем новый
                    }
                }
                
                // Создаем новый JSON с одной записью
                return GenerateJson(computerName, userName, ipAddress, lastUpdate);
            }
            catch
            {
                // В случае ошибки генерируем простой JSON для текущего компьютера
                return GenerateJson(computerName, userName, ipAddress, lastUpdate);
            }
        }

        /// <summary>
        /// Обновляет запись компьютера в существующем JSON файле, удаляя все дубликаты
        /// </summary>
        private static string UpdateComputerInJson(string existingJson, string computerName, string userName, string ipAddress, string lastUpdate)
        {
            try
            {
                // Парсим существующий JSON - ищем формат массива или объекта
                // Формат 1: { "computers": [{...}, {...}] }
                // Формат 2: [{...}, {...}]
                // Формат 3: { "COMPUTER-01": {...}, "COMPUTER-02": {...} }
                
                // Ищем запись текущего компьютера
                string computerNameEscaped = EscapeJson(computerName ?? "");
                
                // Если текущий IP = 0.0.0.0, пытаемся найти правильный IP из существующих записей
                if (ipAddress == "0.0.0.0")
                {
                    string existingIp = FindExistingIpForComputer(existingJson, computerNameEscaped);
                    if (!string.IsNullOrEmpty(existingIp) && existingIp != "0.0.0.0")
                    {
                        ipAddress = existingIp;
                    }
                }
                
                // Удаляем ВСЕ записи с таким же computerName и добавляем одну новую (только если IP валидный)
                return RemoveAllEntriesAndAddNew(existingJson, computerNameEscaped, userName, ipAddress, lastUpdate);
            }
            catch
            {
                // Ошибка парсинга - возвращаем null для создания нового файла
                return null;
            }
        }

        /// <summary>
        /// Находит существующий IP адрес для указанного компьютера в JSON
        /// </summary>
        private static string FindExistingIpForComputer(string json, string computerName)
        {
            try
            {
                string trimmedJson = json.Trim();
                if (!trimmedJson.StartsWith("["))
                    return null;
                
                // Ищем запись с таким computerName и извлекаем IP
                int computerNameIndex = trimmedJson.IndexOf("\"computerName\":\"" + computerName + "\"", StringComparison.OrdinalIgnoreCase);
                if (computerNameIndex == -1)
                    return null;
                
                // Ищем начало объекта для этой записи
                int objectStart = trimmedJson.LastIndexOf('{', computerNameIndex);
                if (objectStart == -1)
                    return null;
                
                // Ищем конец объекта
                int objectEnd = trimmedJson.IndexOf('}', objectStart);
                if (objectEnd == -1)
                    return null;
                
                // Извлекаем объект
                string entry = trimmedJson.Substring(objectStart, objectEnd - objectStart + 1);
                
                // Ищем IP адрес в этой записи
                int ipIndex = entry.IndexOf("\"ipAddress\":\"", StringComparison.OrdinalIgnoreCase);
                if (ipIndex == -1)
                    return null;
                
                int ipStart = ipIndex + "\"ipAddress\":\"".Length;
                int ipEnd = entry.IndexOf('"', ipStart);
                if (ipEnd == -1)
                    return null;
                
                string foundIp = entry.Substring(ipStart, ipEnd - ipStart);
                return foundIp;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Удаляет все записи с указанным computerName и добавляет одну новую запись (только если IP валидный)
        /// </summary>
        private static string RemoveAllEntriesAndAddNew(string json, string computerName, string userName, string ipAddress, string lastUpdate)
        {
            try
            {
                string trimmedJson = json.Trim();
                
                // Работаем только с форматом массива [{...}, {...}]
                if (!trimmedJson.StartsWith("["))
                {
                    // Если не массив, создаем новый
                    return GenerateJson(Environment.MachineName, userName, ipAddress, lastUpdate);
                }
                
                // Удаляем открывающую и закрывающую скобки массива
                trimmedJson = trimmedJson.TrimStart('[').TrimEnd(']').Trim();
                
                // Разбиваем на отдельные записи
                List<string> entries = new List<string>();
                StringBuilder currentEntry = new StringBuilder();
                int braceCount = 0;
                bool inString = false;
                
                for (int i = 0; i < trimmedJson.Length; i++)
                {
                    char c = trimmedJson[i];
                    bool isEscaped = (i > 0 && trimmedJson[i - 1] == '\\');
                    
                    if (c == '"' && !isEscaped)
                    {
                        inString = !inString;
                        currentEntry.Append(c);
                    }
                    else if (!inString)
                    {
                        if (c == '{')
                        {
                            braceCount++;
                            currentEntry.Append(c);
                        }
                        else if (c == '}')
                        {
                            braceCount--;
                            currentEntry.Append(c);
                            if (braceCount == 0)
                            {
                                // Завершили объект
                                string entry = currentEntry.ToString().Trim();
                                if (!string.IsNullOrWhiteSpace(entry))
                                {
                                    // Проверяем, не является ли это записью нашего компьютера
                                    if (!entry.Contains("\"computerName\":\"" + computerName + "\""))
                                    {
                                        entries.Add(entry);
                                    }
                                }
                                currentEntry.Clear();
                                // Пропускаем запятую после объекта, если есть
                                while (i + 1 < trimmedJson.Length && (trimmedJson[i + 1] == ',' || char.IsWhiteSpace(trimmedJson[i + 1])))
                                {
                                    i++;
                                }
                            }
                            else
                            {
                                currentEntry.Append(c);
                            }
                        }
                        else if (c == ',' && braceCount == 0)
                        {
                            // Запятая между объектами - игнорируем
                        }
                        else
                        {
                            currentEntry.Append(c);
                        }
                    }
                    else
                    {
                        currentEntry.Append(c);
                    }
                }
                
                // Добавляем оставшуюся запись, если есть
                string lastEntry = currentEntry.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(lastEntry) && !lastEntry.Contains("\"computerName\":\"" + computerName + "\""))
                {
                    entries.Add(lastEntry);
                }
                
                // Генерируем новую запись только если IP валидный (не 0.0.0.0)
                string newEntry = null;
                if (ipAddress != "0.0.0.0" && !string.IsNullOrEmpty(ipAddress))
                {
                    newEntry = GenerateSingleEntry(computerName, userName, ipAddress, lastUpdate);
                }
                
                // Собираем новый JSON массив
                StringBuilder result = new StringBuilder();
                result.AppendLine("[");
                
                // Добавляем все существующие записи (без дубликатов нашего компьютера)
                for (int i = 0; i < entries.Count; i++)
                {
                    result.Append(entries[i]);
                    if (i < entries.Count - 1 || !string.IsNullOrWhiteSpace(newEntry))
                        result.AppendLine(",");
                    else
                        result.AppendLine();
                }
                
                // Добавляем новую запись текущего компьютера только если IP валидный
                if (!string.IsNullOrWhiteSpace(newEntry))
                {
                    result.Append(newEntry);
                    result.AppendLine();
                }
                
                result.Append("]");
                
                return result.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Добавляет новую запись компьютера в JSON
        /// </summary>
        private static string AddComputerEntry(string json, string computerName, string userName, string ipAddress, string lastUpdate)
        {
            try
            {
                string newEntry = GenerateSingleEntry(computerName, userName, ipAddress, lastUpdate);
                string trimmedJson = json.Trim();
                
                // Определяем формат JSON
                if (trimmedJson.StartsWith("["))
                {
                    // Массив - добавляем в конец
                    trimmedJson = trimmedJson.TrimEnd(']').TrimEnd();
                    if (!trimmedJson.EndsWith("["))
                        trimmedJson += ",";
                    return trimmedJson + newEntry + "]";
                }
                else if (trimmedJson.StartsWith("{"))
                {
                    // Объект - добавляем как поле
                    trimmedJson = trimmedJson.TrimEnd('}').TrimEnd();
                    if (!trimmedJson.EndsWith("{") && !trimmedJson.EndsWith(","))
                        trimmedJson += ",";
                    return trimmedJson + "\"" + computerName + "\":" + newEntry + "}";
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Генерирует JSON запись для одного компьютера
        /// </summary>
        private static string GenerateSingleEntry(string computerName, string userName, string ipAddress, string lastUpdate)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendFormat("  \"computerName\": \"{0}\",\n", EscapeJson(computerName ?? ""));
            sb.AppendFormat("  \"userName\": \"{0}\",\n", EscapeJson(userName ?? ""));
            sb.AppendFormat("  \"ipAddress\": \"{0}\",\n", EscapeJson(ipAddress ?? ""));
            sb.AppendFormat("  \"lastUpdate\": \"{0}\"\n", EscapeJson(lastUpdate ?? ""));
            sb.Append("  }");
            return sb.ToString();
        }

        /// <summary>
        /// Генерирует новый JSON файл из параметров
        /// </summary>
        private static string GenerateJson(string computerName, string userName, string ipAddress, string lastUpdate)
        {
            try
            {
                // Создаем массив с одной записью
                var sb = new StringBuilder();
                sb.AppendLine("[");
                sb.Append(GenerateSingleEntry(computerName, userName, ipAddress, lastUpdate));
                sb.AppendLine();
                sb.AppendLine("]");
                return sb.ToString();
            }
            catch
            {
                // В случае ошибки возвращаем минимальный JSON
                return "[{\"error\":\"Failed to generate JSON\"}]";
            }
        }

        /// <summary>
        /// Экранирует специальные символы для JSON
        /// </summary>
        private static string EscapeJson(string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                    return "";

                return value.Replace("\\", "\\\\")
                           .Replace("\"", "\\\"")
                           .Replace("\n", "\\n")
                           .Replace("\r", "\\r")
                           .Replace("\t", "\\t");
            }
            catch
            {
                // В случае ошибки возвращаем пустую строку
                return "";
            }
        }

        /// <summary>
        /// Сохраняет JSON на сетевой диск с обработкой ошибок доступа и прав доступа
        /// </summary>
        private static void SaveToNetworkFile(string json)
        {
            // Если json == null, значит файл открыт или недоступен - пропускаем сохранение
            if (json == null)
                return;

            try
            {
                string filePath = Settings.AspiaNetworkPath;
                
                // Проверяем что путь указан
                if (string.IsNullOrEmpty(filePath))
                    return;

                // Проверяем доступность сетевого пути
                string directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory))
                {
                    // Если директория не определена, пробуем использовать сам путь как директорию
                    directory = filePath;
                    filePath = Path.Combine(directory, "aspia.json");
                }

                // Проверяем существование директории и права доступа
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        try
                        {
                            Directory.CreateDirectory(directory);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Нет прав на создание директории - пропускаем экспорт
                            return;
                        }
                        catch
                        {
                            // Другие ошибки - пропускаем экспорт
                            return;
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Нет прав на доступ к директории - пропускаем экспорт
                    return;
                }
                catch
                {
                    // Недоступен сетевой путь - пропускаем экспорт
                    return;
                }

                // Записываем файл с повторными попытками при ошибках доступа
                int maxRetries = 5;
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        // Используем временный файл с уникальным именем для избежания конфликтов
                        string tempFile = filePath + "." + Environment.MachineName + ".tmp";
                        
                        // Удаляем старый временный файл если есть
                        try
                        {
                            if (File.Exists(tempFile))
                                File.Delete(tempFile);
                        }
                        catch { }
                        
                        // Записываем во временный файл
                        File.WriteAllText(tempFile, json, Encoding.UTF8);
                        
                        // Пытаемся атомарно заменить основной файл
                        if (File.Exists(filePath))
                        {
                            // Используем FileShare.Read для чтения другими процессами
                            File.Replace(tempFile, filePath, null, true);
                        }
                        else
                        {
                            File.Move(tempFile, filePath);
                        }
                        
                        // Успешно записали
                        return;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Нет прав на запись - прекращаем попытки
                        return;
                    }
                    catch (IOException)
                    {
                        // Файл занят другим процессом - ждем и повторяем
                        if (i < maxRetries - 1)
                        {
                            Thread.Sleep(200 * (i + 1)); // Увеличиваем задержку с каждой попыткой
                        }
                    }
                    catch
                    {
                        // Другие ошибки - прекращаем попытки после небольшой задержки
                        if (i < maxRetries - 1)
                        {
                            Thread.Sleep(100);
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Нет прав доступа к сетевому диску - игнорируем
            }
            catch
            {
                // Игнорируем другие ошибки доступа к сетевому диску
            }
        }
    }
}

