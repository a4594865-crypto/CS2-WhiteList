using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;

namespace WhiteList;

public partial class WhiteList
{
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

        // 2. 第一層攔截：如果現在權限已經讀到了 (來自 CSS 的 admins.json)，直接放行
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

        // 3. 核心修改：非同步執行檢查，並給予權限加載的寬限期
        Task.Run(async () =>
        {
            // 執行白名單檢查 (檔案或資料庫)
            bool isWhitelisted = await IsWhiteListed(ip != null ? [.. whitelistOptions, ip] : whitelistOptions);

            // 如果玩家不在白名單中，我們不急著踢，先等 1.5 秒讓 CSS 完成 admins.json 的讀取
            if ((isWhitelisted && Config.UseAsBlacklist) || (!isWhitelisted && !Config.UseAsBlacklist))
            {
                // 給予權限系統緩衝時間
                await Task.Delay(1500);

                // 回到伺服器主執行緒進行最終權限確認
                Server.NextFrame(() =>
                {
                    if (player == null || !player.IsValid) return;

                    // 最終確認：這時候 CSS 應該已經從 admins.json 載入完畢
                    if (AdminManager.PlayerHasPermissions(player, Config.Commands.ImmunityPermission))
                    {
                        Logger.LogInformation($"[WhiteList] 攔截誤踢：管理員 {name} 的權限已從 admins.json 載入。");
                        return;
                    }

                    // 如果 1.5 秒後還是沒權限且不在白名單，才踢除
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
