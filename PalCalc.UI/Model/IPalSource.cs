using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    public interface IPalSource
    {
        List<PalInstance> Filter(CachedSaveGame save);
    }

    public class PlayerPalSource : IPalSource
    {
        public PlayerPalSource(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }

        public List<PalInstance> Filter(CachedSaveGame save) => save.OwnedPals.Where(p => p.OwnerPlayerId == PlayerId).ToList();
    }

    public class GuildPalSource : IPalSource
    {
        public GuildPalSource(string guildId)
        {
            GuildId = guildId;
        }

        public string GuildId { get; }

        public List<PalInstance> Filter(CachedSaveGame save)
        {
            var playerIds = save.Guilds.Single(g => g.Id == GuildId).MemberIds;
            return save.OwnedPals.Where(p => playerIds.Contains(p.OwnerPlayerId)).ToList();
        }
    }

    public class AllPalsPalSource : IPalSource
    {
        public List<PalInstance> Filter(CachedSaveGame save) => save.OwnedPals;
    }
}
