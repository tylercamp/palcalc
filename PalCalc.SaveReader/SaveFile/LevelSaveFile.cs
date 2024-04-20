using PalCalc.Model;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.SaveFile.Support.Level;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public class LevelSaveFile : ISaveFile
    {
        public LevelSaveFile(string folderPath) : base(folderPath) { }

        public override string FileName => "Level.sav";

        public List<PalInstance> ReadPalInstances(PalDB db)
        {
            var containerVisitor = new PalContainerVisitor();
            var instanceVisitor = new PalInstanceVisitor();
            VisitGvas(containerVisitor, instanceVisitor);

            var containerTypeById = new Dictionary<string, LocationType>()
            {
                { containerVisitor.CollectedContainers[0].Id, LocationType.PlayerParty },
                { containerVisitor.CollectedContainers[1].Id, LocationType.Palbox },
            };

            foreach (var container in containerVisitor.CollectedContainers.Skip(2))
                containerTypeById.Add(container.Id, LocationType.Base);

            return instanceVisitor.Result
                .Select(gvasInstance =>
                {
                    var sanitizedCharId = gvasInstance.CharacterId.Replace("Boss_", "", StringComparison.InvariantCultureIgnoreCase);
                    var pal = db.Pals.FirstOrDefault(p => p.InternalName.ToLower() == sanitizedCharId.ToLower());

                    if (pal == null)
                    {
                        // skip unrecognized pals
                        // TODO - log warning
                        return null;
                    }

                    var traits = gvasInstance.Traits
                        .Select(name =>
                        {
                            var trait = db.Traits.FirstOrDefault(t => t.InternalName == name);
                            // TODO - log warning for unrecognized trait
                            return trait ?? new UnrecognizedTrait(name);
                        })
                        .ToList();

                    return new PalInstance()
                    {
                        Pal = pal,

                        Gender = gvasInstance.Gender.Contains("Female") ? PalGender.FEMALE : PalGender.MALE,
                        Traits = traits,
                        Location = new PalLocation()
                        {
                            Type = containerTypeById[gvasInstance.ContainerId.ToString()],
                            Index = gvasInstance.SlotIndex,
                        }
                    };
                })
                .SkipNull()
                .ToList();
        }
    }
}
