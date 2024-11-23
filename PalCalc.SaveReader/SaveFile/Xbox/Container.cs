using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PalCalc.SaveReader.SaveFile.Xbox
{
    // copy of https://github.com/Tom60chat/Xbox-Live-Save-Exporter/blob/main/Xbox%20Live%20Save%20Exporter.Shared/Models/Container.cs
    public class Container
    {
        private static ILogger logger = Log.ForContext<Container>();

        #region Constructors
        public Container(string friendlyName, string packageFullName, string id, Guid guid, IList<ContainerFolder> folders, string path)
        {
            FriendlyName = friendlyName;
            PackageFullName = packageFullName;
            Id = id;
            Guid = guid;
            Folders = folders;
            Path = path;
        }
        #endregion

        #region Properties
        /// <summary> The friendly name of the package </summary>
        public string FriendlyName { get; private set; }
        /// <summary> The full name of the package</summary>
        public string PackageFullName { get; private set; }
        /// <summary> The id of the sub container</summary>
        public string Id { get; private set; }
        /// <summary> Unknown GUID </summary>
        public Guid Guid { get; private set; }
        /// <summary> List of folders </summary>
        public IList<ContainerFolder> Folders { get; private set; }
        /// <summary> Path to the container index file </summary>
        public string Path { get; private set; }
        #endregion

        #region Methods
        /// <summary>
        /// Try to parse the index container
        /// </summary>
        /// <!--<param name="path">Path to the index container</param>-->
        /// <param name="sFile">Index container file</param>
        /// <returns>The container and its folders</returns>
        //public static Container TryParse(string path)
        public static Container TryParse(string path)
        {
            List<ContainerFolder> folders = new List<ContainerFolder>();

            try
            {
                // Open a filestream with the user selected file
                //FileStream stream = new FileStream(path, FileMode.Open);
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Create a binary reader that will be used to read the file
                BinaryReader reader = new BinaryReader(stream);

                // Grab the type
                int type = reader.ReadInt32();

                int numFolders = reader.ReadInt32();

                string friendlyName = BinaryReaderHelper.ReadUnicodeString(reader, reader.ReadInt32());

                string[] name = BinaryReaderHelper.ReadUnicodeString(reader, reader.ReadInt32()).Split('!');
                string packageFullName = name[0];
                string id = name[1];

                // Skip unknown value
                reader.ReadBytes(0xc);

                // Unknown guid
                Guid guid = new Guid(BinaryReaderHelper.ReadUnicodeString(reader, reader.ReadInt32()));

                if (type == 0xe)
                {
                    // 8 padding bytes
                    reader.ReadBytes(8);
                }

                // Loop through every folder in the index, and print info about it
                for (int i = 0; i < numFolders; i++)
                {
                    string folderName = BinaryReaderHelper.ReadUnicodeString(reader, reader.ReadInt32());

                    string secondName = BinaryReaderHelper.ReadUnicodeString(reader, reader.ReadInt32());

                    // Skip unknown value
                    string UnknownValue = BinaryReaderHelper.ReadUnicodeString(reader, reader.ReadInt32());

                    byte containerId = reader.ReadByte();

                    // Skip unknown data
                    reader.ReadBytes(4);

                    // The guid folder that the files reside in
                    byte[] guid1 = reader.ReadBytes(4);
                    Array.Reverse(guid1);
                    byte[] guid2 = reader.ReadBytes(2);
                    Array.Reverse(guid2);
                    byte[] guid3 = reader.ReadBytes(2);
                    Array.Reverse(guid3);
                    byte[] guid4 = reader.ReadBytes(2);
                    byte[] guid5 = reader.ReadBytes(6);

                    Guid folderGuid = new Guid(BitConverter.ToString(guid1).Replace("-", string.Empty) + "-" + BitConverter.ToString(guid2).Replace("-", string.Empty) + "-" + BitConverter.ToString(guid3).Replace("-", string.Empty) + "-" + BitConverter.ToString(guid4).Replace("-", string.Empty) + "-" + BitConverter.ToString(guid5).Replace("-", string.Empty));

                    // Skip unknown value
                    reader.ReadBytes(0x18);

                    string folderPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), folderGuid.ToString("N").ToUpper());

                    folders.Add(new ContainerFolder(folderName, containerId, folderGuid, folderPath));
                }

                reader.Dispose();
                stream.Dispose();

                return new Container(friendlyName, packageFullName, id, guid, folders, path);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "error reading container file {path}", path);
                return null;
            }
        }
        #endregion
    }
}
