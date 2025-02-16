using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader
{
    public static class FileUtil
    {
        // file operations can take a while, and they can happen while Palworld is running + saving file contents.
        // save files are relatively small (typically ~10MB at most), prefer loading the whole file into memory up
        // front so we can close the handle ASAP.
        public static Stream ReadFileNonLocking(string filePath, int maxLength = 0)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (maxLength > 0)
                {
                    var buf = new byte[maxLength];
                    int numRead = 0, lastRead = 0;
                    do
                    {
                        numRead += lastRead;
                        lastRead = fs.Read(buf, numRead, buf.Length - numRead);
                    } while (numRead < maxLength && lastRead > 0);

                    return new MemoryStream(buf, 0, numRead);
                }
                else
                {
                    var ms = new MemoryStream(capacity: (int)fs.Length);
                    fs.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return ms;
                }
            }
        }
    }
}
