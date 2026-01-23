using System;
using System.IO;

namespace InformerInstaller
{
    class ReadBrowseResult
    {
        static int Main(string[] args)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "InformerInstallPath.txt");
                
                if (File.Exists(tempFile))
                {
                    string path = File.ReadAllText(tempFile).Trim();
                    
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Записываем результат в реестр для чтения WiX через AppSearch
                        // Используем временный ключ реестра через Microsoft.Win32.Registry
                        try
                        {
                            Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Informer\Install");
                            if (regKey != null)
                            {
                                regKey.SetValue("SelectedPath", path, Microsoft.Win32.RegistryValueKind.String);
                                regKey.Close();
                            }
                        }
                        catch
                        {
                            // Игнорируем ошибки реестра
                        }
                        
                        // Удаляем временный файл
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch
                        {
                            // Игнорируем ошибки удаления
                        }
                        
                        // Также выводим в stdout для совместимости
                        Console.Out.WriteLine(path);
                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
            
            return 1;
        }
    }
}
