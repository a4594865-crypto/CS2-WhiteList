using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;

namespace WhiteList;

// 使用 partial 關鍵字，這樣它會與 WhiteList.cs 組合在一起
public partial class WhiteList
{
    // 這裡的 OnClientAuthorized 必須是 private，且全專案只能有這一個實作
    private void OnClientAuthorized(int playerSlot, SteamID steamId)
    {
        // 1. 如果白名單開關沒開，直接放行
        if (!Config.Enabled) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player is null || player.IsBot) return;

        var name = player.PlayerName;
        var steamId64 = steamId.SteamId64.ToString();
        var ip = player.IpAddress?.Split(":")[0];
        var userId = player.UserId;

        // 2. 管理員豁免檢查 (來自 admins.json)
        if (AdminManager.PlayerHasPermissions(player, Config.Commands.ImmunityPermission))
        {
            Logger.LogInformation($"[WhiteList] 管理員 {name} 驗證成功，准許連線。");
            return;
        }

        List<string> whitelistOptions = [
            steamId64,
            steamId.SteamId3,
            steamId.SteamId2.Replace("STEAM_0", "STEAM_1")
        ];

        // 3. 非同步執行檢查
        Task.Run(async () =>
        {
            // 這裡會去檢查 WhiteListValues 陣列（該陣列已在 WhiteList.cs 透過修正後的 CheckFile 載入）
            bool isWhitelisted = await IsWhiteListed(ip != null ? [.. whitelistOptions, ip] : whitelistOptions);

            if ((isWhitelisted && Config.UseAsBlacklist) || (!isWhitelisted && !Config.UseAsBlacklist))
            {
                // 給予權限系統緩衝時間，避免誤踢剛連線的管理員
                await Task.Delay(1500);

                Server.NextFrame(() =>
                {
                    if (player == null || !player.IsValid) return;

                    // 再次確認管理員權限
                    if (AdminManager.PlayerHasPermissions(player, Config.Commands.ImmunityPermission))
                    {
                        return;
                    }

                    if (userId.HasValue)
                    {
                        Logger.LogWarning($"[WhiteList] 玩家 {name} ({steamId64}) 驗證失敗，執行踢除。");
                        // 呼叫踢人指令
                        Server.ExecuteCommand($"kickid {userId.Value} \"Whitelist Blocked\"");
                    }
                });
            }
        });
    }
}
