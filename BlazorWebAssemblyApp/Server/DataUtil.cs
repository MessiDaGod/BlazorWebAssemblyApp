// joeshakely
using System;
using Stl.IO;

namespace BlazorWebAssemblyApp.Server
{
    public class DataUtil
    {
        public DataUtil()
        {
        }

        /// <summary>
        /// Appends _v1 or _v2 or _v3 etc. to file name if the file already exists.
        /// </summary>sa
        /// <param name="dbName"></param>
        /// <returns>full path of the file name</returns>
        public static string AddVersionToDb(string dbName)
        {
            var appDir = FilePath.GetApplicationDirectory();
            var fullPath = Path.Combine(appDir, dbName);
            if (!File.Exists(fullPath))
                return fullPath;

            if (IsFileLocked(fullPath)) {
                string suffix = "_v";
                int i = 1;
                bool Exists = false;
                while (!Exists) {
                    string version = $"{suffix}{i}";
                    string newFieFull = Path.Combine(Path.GetDirectoryName(fullPath) ?? "", Path.GetFileNameWithoutExtension(fullPath)) + version + Path.GetExtension(fullPath);
                    if (!File.Exists(newFieFull)) {
                        dbName = newFieFull;
                        Exists = !Exists;
                    }
                    i++;
                }
            }
            return dbName;
        }

        protected static bool IsFileLocked(string filePath)
        {
            try {
                using (FileStream stream = new FileInfo(filePath).Open(FileMode.Open, FileAccess.Read, FileShare.None)) {
                    stream.Close();
                }
            } catch (IOException) {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            } catch (Exception) {
                return true;
            }
            //file is not locked
            return false;
        }
    }
}

