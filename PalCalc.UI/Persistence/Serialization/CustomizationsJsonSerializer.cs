using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Dto;
using System.Linq;

namespace PalCalc.UI.Persistence.Serialization
{

    internal static class CustomizationsJsonSerializer
    {
        public static SaveCustomizationsDto FromCurrentJson(string json) =>
            JsonConvert.DeserializeObject<SaveCustomizationsDto>(json)
            ?? throw new JsonSerializationException("Save customizations document was empty.");

        public static string ToJson(SaveCustomizationsDto value) => JsonConvert.SerializeObject(value);

        public static SaveCustomizationsDto ToDto(SaveCustomizations value) => new()
        {
            CustomContainers = (value?.CustomContainers ?? [])
                .Select(container => new CustomContainerDto
                {
                    Label = container.Label,
                    Contents = (container.Contents ?? []).Select(ToDto).ToList(),
                })
                .ToList(),
        };

        public static SaveCustomizations ToRuntime(SaveCustomizationsDto value, PalDB db) => new()
        {
            CustomContainers = value.CustomContainers.Select(container => new CustomContainer
            {
                Label = container.Label,
                Contents = container.Contents.Select(instance => FromDto(instance, db, container.Label)).ToList(),
            }).ToList(),
        };

        internal static PalInstanceSnapshotDto ToDto(PalInstance value) => new()
        {
            InternalName = value.Pal.InternalName,
            Location = new PalLocationDto
            {
                ContainerId = value.Location?.ContainerId,
                Type = value.Location?.Type ?? LocationType.Custom,
                Index = value.Location?.Index ?? 0,
            },
            Gender = value.Gender,
            PassiveSkills = (value.PassiveSkills ?? []).Select(passive => passive.InternalName).ToList(),
            ActiveSkills = (value.ActiveSkills ?? []).Select(skill => skill.InternalName).ToList(),
            EquippedActiveSkills = (value.EquippedActiveSkills ?? []).Select(skill => skill.InternalName).ToList(),
            OwnerPlayerId = value.OwnerPlayerId,
            NickName = value.NickName,
            Level = value.Level,
            InstanceId = value.InstanceId,
            IV_HP = value.IV_HP,
            IV_Melee = value.IV_Melee,
            IV_Shot = value.IV_Shot,
            IV_Defense = value.IV_Defense,
            IsOnExpedition = value.IsOnExpedition,
        };

        internal static PalInstance FromDto(PalInstanceSnapshotDto value, PalDB db) => FromDto(value, db, value.Location.ContainerId);

        private static PalInstance FromDto(PalInstanceSnapshotDto value, PalDB db, string owningContainerId) => new()
        {
            Pal = value.InternalName.InternalToPal(db),
            Location = new PalLocation
            {
                ContainerId = owningContainerId,
                Type = owningContainerId != null ? LocationType.Custom : value.Location.Type,
                Index = value.Location.Index,
            },
            Gender = value.Gender,
            PassiveSkills = value.PassiveSkills.Select(name => name.InternalToStandardPassive(db)).ToList(),
            ActiveSkills = value.ActiveSkills.Select(name => name.ToActive(db)).ToList(),
            EquippedActiveSkills = value.EquippedActiveSkills.Select(name => name.ToActive(db)).ToList(),
            OwnerPlayerId = value.OwnerPlayerId,
            NickName = value.NickName,
            Level = value.Level,
            InstanceId = value.InstanceId,
            IV_HP = value.IV_HP,
            IV_Melee = value.IV_Melee,
            IV_Shot = value.IV_Shot,
            IV_Defense = value.IV_Defense,
            IsOnExpedition = value.IsOnExpedition,
        };
    }

}
