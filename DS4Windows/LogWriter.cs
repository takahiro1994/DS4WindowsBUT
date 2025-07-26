/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using System.IO;

namespace DS4WinWPF
{
    public class LogWriter
    {
        private string filename;
        private List<LogItem> logCol;

        public LogWriter(string filename, List<LogItem> col)
        {
            this.filename = filename;
            logCol = col;
        }

        public void Process()
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                DS4Windows.AppLogger.LogToGui("LogWriter: Invalid filename provided", true);
                return;
            }

            if (logCol == null || logCol.Count == 0)
            {
                DS4Windows.AppLogger.LogToGui("LogWriter: No log items to write", false);
                return;
            }

            List<string> outputLines = new List<string>();
            foreach(LogItem item in logCol)
            {
                if (item != null)
                {
                    outputLines.Add($"{item.Datetime}: {item.Message}");
                }
            }

            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(filename);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (StreamWriter stream = new StreamWriter(filename, false, System.Text.Encoding.UTF8))
                {
                    foreach(string line in outputLines)
                    {
                        stream.WriteLine(line);
                    }
                }

                DS4Windows.AppLogger.LogToGui($"Log exported successfully to: {filename}", false);
            }
            catch (UnauthorizedAccessException ex)
            {
                DS4Windows.AppLogger.LogToGui($"LogWriter: Access denied writing to {filename}. {ex.Message}", true);
            }
            catch (DirectoryNotFoundException ex)
            {
                DS4Windows.AppLogger.LogToGui($"LogWriter: Directory not found for {filename}. {ex.Message}", true);
            }
            catch (IOException ex)
            {
                DS4Windows.AppLogger.LogToGui($"LogWriter: IO error writing to {filename}. {ex.Message}", true);
            }
            catch (Exception ex)
            {
                DS4Windows.AppLogger.LogToGui($"LogWriter: Unexpected error writing to {filename}. {ex.Message}", true);
            }
        }
    }
}
