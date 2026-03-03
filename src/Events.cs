using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;

namespace WhiteList;

public partial class WhiteList
{
    private void OnClientAuthorized(int playerSlot, SteamID steamId)
    {
        // 1. 如果開關沒開，直接放行
        if (!Config.Enabled) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player is null || !player.IsValid || player.IsBot || player.UserId is null)
        {
            if (!Config.KickIfFailed) return;
            if (player != null && player.UserId != null)
                KickPlayer(player.UserId.Value, player.PlayerName, player.SteamID.ToString());
            return;
        }

        var ip = player.IpAddress?.Split(":")[0];
        var name = player.PlayerName;
        var steamId64 = steamId.SteamId64.ToString();
        var userId = player.UserId.Value;

        // 2. 第一層攔截：如果現在權限已經讀到了，直接放行
        if (AdminManager.PlayerHasPermissions(player, Config.Commands.ImmunityPermission))
        {
            Logger.LogInformation($"[WhiteList] 管理員 {name} 已透過權限豁免。");
            return;
        }

        List<string> whitelistOptions = [
            steamId64,
            steamId.SteamId3,
            steamId.SteamId2.Replace("STEAM_0", "STEAM_1")
        ];

        Task.Run(() =>
        {
            if (Config.SteamGroup.CheckIfMemberIsInGroup)
            {
                var groups = GetSteamGroupsId(steamId64);
                if (groups.Result is not null)
                {
                    whitelistOptions.AddRange(groups.Result);
                }
                else if (Config.KickIfFailed)
                {
                    // 這裡也加入保護，避免誤踢
                    Server.NextFrame(() => {
                        if (!AdminManager.PlayerHasPermissions(player, Config.Commands.ImmunityPermission))
                            KickPlayer(userId, name, steamId64);
                    });
                    return Task.FromResult(false);
                }
            }
            return IsWhiteListed(ip != null ? [.. whitelistOptions, ip] : whitelistOptions);
        }).ContinueWith(task =>
        {
            // 3. 第二層攔截：準備踢人前，再次回到伺服器主線程檢查權限
            // 這一步是為了解決「連線瞬間權限還沒加載好」的問題
            if ((task.Result && Config.UseAsBlacklist) || (!task.Result && !Config.UseAsBlacklist))
            {
                Server.NextFrame(() =>
                {
                    // 再次確認：這名玩家真的沒有豁免權限嗎？
                    if (AdminManager.PlayerHasPermissions(player, Config.Commands.ImmunityPermission))
                    {
                        Logger.LogInformation($"[WhiteList] 已攔截對管理員 {name} 的誤踢，權限加載成功。");
                        return;
                    }

                    // 確定不是管理員，才執行 Kick
                    KickPlayer(userId, name, steamId64);
                });
            }
        });
    }
}
