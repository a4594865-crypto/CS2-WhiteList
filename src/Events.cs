using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;

namespace WhiteList;

public partial class WhiteList
{
    private void OnClientAuthorized(int playerSlot, SteamID steamId)
    {
        if (!Config.Enabled) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player is null || player.IsBot) return;

        var name = player.PlayerName;
        var steamId64 = steamId.SteamId64.ToString();
        var ip = player.IpAddress?.Split(":")[0];
        var userId = player.UserId;

        if (AdminManager.PlayerHasPermissions(player, Config.Commands.ImmunityPermission))
        {
            Logger.LogInformation($"[WhiteList] 管理員 {name} 立即驗證成功，准許連線。");
            return;
        }

        List<string> whitelistOptions = [
            steamId64,
            steamId.SteamId3,
            steamId.SteamId2.Replace("STEAM_0", "STEAM_1")
        ];

        Task.Run(async () =>
        {
            bool isWhitelisted = await IsWhiteListed(ip != null ? [.. whitelistOptions, ip] : whitelistOptions);

            if ((isWhitelisted && Config.UseAsBlacklist) || (!isWhitelisted && !Config.UseAsBlacklist))
            {
                await Task.Delay(1500);

                Server.NextFrame(() =>
                {
                    if (player == null || !player.IsValid) return;

                    if (AdminManager.PlayerHasPermissions(player, Config.Commands.ImmunityPermission))
                    {
                        Logger.LogInformation($"[WhiteList] 攔截誤踢：管理員 {name} 的權限已從 admins.json 載入。");
                        return;
                    }

                    if (userId.HasValue)
                    {
                        Logger.LogWarning($"[WhiteList] 玩家 {name} 驗證失敗且非管理員，執行踢除。");
                        KickPlayer(userId.Value, name, steamId64);
                    }
                });
            }
        });
    }
}
