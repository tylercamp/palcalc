using PalCalc.Model;
using PalCalc.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Solver
{
    public interface IPalSourceTreeSelection
    {
        public string SerializedId { get; }

        public bool Matches(CachedSaveGame source, PalInstance pal);

        private static ILogger logger = Log.ForContext<IPalSourceTreeSelection>();
        public static IPalSourceTreeSelection SingleFromId(CachedSaveGame source, string serializedId)
        {
            if (serializedId.Split("=") is [var type, var value])
            {
                switch (type)
                {
                    case "PLAYER":
                        var player = source.PlayersById.GetValueOrDefault(value);
                        if (player == null)
                        {
                            logger.Warning("Unrecognized player with ID '{PlayerID}' from selection serialized-id '{RawID}'", value, serializedId);
                            return null;
                        }
                        
                        return new SourceTreePlayerSelection(player);

                    case "GUILD":
                        var guild = source.Guilds.FirstOrDefault(g => g.Id == value);
                        if (guild == null)
                        {
                            logger.Warning("Unrecognized guild with ID '{GuildID}' from selection serialized-id '{RawID}'", value, serializedId);
                            return null;
                        }

                        return new SourceTreeGuildSelection(guild);
                }
            }
            else if (serializedId == "ANY")
            {
                return new SourceTreeAllSelection();
            }

            logger.Warning("Unrecognized pal source tree serialized-id format: '{id}'", serializedId);

            return null;
        }
    }

    public class SourceTreeAllSelection : IPalSourceTreeSelection
    {
        public string SerializedId => "ANY";

        public bool Matches(CachedSaveGame source, PalInstance pal) => true;
    }

    /// <summary>
    /// Filters pals based on the owner ID (for party/palbox pals) or the owner's guild ID (for base pals)
    /// </summary>
    public class SourceTreePlayerSelection(PlayerInstance player) : IPalSourceTreeSelection
    {
        public PlayerInstance ModelObject => player;
        public string SerializedId => $"PLAYER={player.PlayerId}";

        public bool Matches(CachedSaveGame source, PalInstance pal)
        {
            var guildId = source.GuildsByPlayerId[ModelObject.PlayerId]?.Id;

            var bases = source.BasesByGuildId.GetValueOrDefault(guildId, []);

            var baseContainerIds = bases.Select(b => b.Container.Id).ToList();
            var cageContainerIds = source.PalContainers
                .OfType<ViewingCageContainer>()
                .Where(c => bases.Any(b => b.Id == c.BaseId))
                .Select(c => c.Id)
                .ToList();

            return pal.Location.Type switch
            {
                LocationType.PlayerParty => pal.OwnerPlayerId == ModelObject.PlayerId,
                LocationType.DimensionalPalStorage => pal.Location.ContainerId == ModelObject.DimensionalPalStorageContainerId,
                LocationType.Palbox => pal.OwnerPlayerId == ModelObject.PlayerId,
                LocationType.Base => baseContainerIds.Contains(pal.Location.ContainerId),
                LocationType.ViewingCage => cageContainerIds.Contains(pal.Location.ContainerId),
                // (Pals in custom containers aren't expected to have an "owner", and are handled separately in MainWindowViewModel anyway)
                LocationType.Custom => false,
                // (Mostly the same content for GPS; the original pal owner might not even be in this save file)
                LocationType.GlobalPalStorage => false,
                _ => throw new NotImplementedException()
            };
        }
    }

    public class SourceTreeGuildSelection(GuildInstance guild) : IPalSourceTreeSelection
    {
        public GuildInstance ModelObject => guild;
        public string SerializedId => $"GUILD={ModelObject.Id}";

        public bool Matches(CachedSaveGame source, PalInstance pal) => source.GuildsByPlayerId.GetValueOrDefault(pal.OwnerPlayerId)?.Id == guild.Id;
    }
}
