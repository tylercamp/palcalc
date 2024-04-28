using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Xbox
{
    // copy of https://github.com/Tom60chat/Xbox-Live-Save-Exporter/blob/main/Xbox%20Live%20Save%20Exporter.Shared/BinaryReaderHelper.cs
    internal class BinaryReaderHelper
    {
        /// <summary>
        /// Reads a unicode string from a binary file.
        /// </summary>
        /// <param name="reader">The reader that will be used to read the Unicode string.</param>
        /// <param name="count">The number of characters in the string.</param>
        /// <returns>The Unicode string.</returns>
        public static string ReadUnicodeString(BinaryReader reader, int count)
        {
            // Multiply count by two because each unicode char is two bytes, at least in the context of containers.index
            byte[] buff = reader.ReadBytes(count * 2);
            return Encoding.Unicode.GetString(buff);
        }

        /// <summary>
        /// Reads a unicode string from a binary file.
        /// </summary>
        /// <param name="reader">The reader that will be used to read the Unicode string.</param>
        /// <returns>The Unicode string.</returns>
        public static string ReadUnicodeString(BinaryReader reader)
        {
            List<byte> buff = new List<byte>();

            while (true)
            {
                byte[] currentBytes = reader.ReadBytes(2);

                if (currentBytes[0] == 0x0 && currentBytes[1] == 0x0)
                    return Encoding.Unicode.GetString(buff.ToArray());
                else
                {
                    buff.Add(currentBytes[0]);
                    buff.Add(currentBytes[1]);
                }
            }

            // Go back to a position after reading a empty byte
            reader.BaseStream.Position--;
        }
    }
}
