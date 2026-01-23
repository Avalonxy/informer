using System;
using System.IO;
using System.Windows.Forms;

namespace InformerInstaller
{
    class BrowseFolder
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                // Инициализация Windows Forms (не требуется для диалога, но на всякий случай)
                // Application.EnableVisualStyles();
                // Application.SetCompatibleTextRenderingDefault(false);
                
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Выберите папку для установки Informer:";
                    dialog.ShowNewFolderButton = true;
                    
                    // Устанавливаем начальную папку, если передана
                    if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                    {
                        string initialPath = args[0].Trim('"');
                        // Убираем \Informer из пути, если есть
                        if (initialPath.EndsWith("\\Informer", StringComparison.OrdinalIgnoreCase))
                        {
                            initialPath = initialPath.Substring(0, initialPath.Length - 9);
                        }
                        if (Directory.Exists(initialPath))
                        {
                            dialog.SelectedPath = initialPath;
                        }
                    }
                    
                    // Показываем диалог
                    DialogResult result = dialog.ShowDialog();
                    
                    if (result == DialogResult.OK && !string.IsNullOrEmpty(dialog.SelectedPath))
                    {
                        string selectedPath = dialog.SelectedPath;
                        // Убираем завершающий слэш, если есть
                        selectedPath = selectedPath.TrimEnd('\\');
                        
                        // Добавляем \Informer если его нет
                        if (!selectedPath.EndsWith("\\Informer", StringComparison.OrdinalIgnoreCase))
                        {
                            selectedPath = selectedPath + "\\Informer";
                        }
                        
                        // Записываем результат во временный файл для чтения WiX
                        string tempFile = Path.Combine(Path.GetTempPath(), "InformerInstallPath.txt");
                        try
                        {
                            File.WriteAllText(tempFile, selectedPath, System.Text.Encoding.UTF8);
                            // Также выводим в stdout для совместимости
                            Console.Out.WriteLine(selectedPath);
                            return 0;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Ошибка записи файла: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Показываем ошибку пользователю
                MessageBox.Show("Ошибка при выборе папки: " + ex.Message + "\n\n" + ex.StackTrace, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.Error.WriteLine("Error: " + ex.Message);
                Console.Error.WriteLine("StackTrace: " + ex.StackTrace);
                return 1;
            }
            
            return 1; // Отменено
        }
    }
}
