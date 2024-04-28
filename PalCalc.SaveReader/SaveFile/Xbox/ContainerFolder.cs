using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Xbox
{
    // copy of https://github.com/Tom60chat/Xbox-Live-Save-Exporter/blob/main/Xbox%20Live%20Save%20Exporter.Shared/Models/ContainerFolder.cs
    public class ContainerFolder
    {
        #region Constructors
        public ContainerFolder(string name, byte id, Guid guid, string path)
        {
            Name = name;
            Id = id;
            Guid = guid;
            Path = path;
        }
        #endregion

        #region Properties
        /// <summary> Folder name </summary>
        public string Name { get; private set; }
        /// <summary> Id of the container inside the folder </summary>
        public byte Id { get; private set; }
        /// <summary> Folder GUID </summary>
        public Guid Guid { get; private set; }
        /// <summary> Folder path </summary>
        public string Path { get; private set; }
        #endregion
    }
}
