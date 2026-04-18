using System.Net.Http.Json;
using System.Text.Json;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;

namespace WhiteList;

public partial class WhiteList
{
  public async Task<bool> IsWhiteListed(List<string> value)
  {
    if (Config.UseDatabase)
    {
      IEnumerable<dynamic>? whitelisted = await GetFromDatabase(value);
      if (whitelisted != null && whitelisted.Any()) return true;
    }
    else
    {
      // 使用更嚴謹的比對
      if (WhiteListValues.Any(v => value.Contains(v))) return true;
    }
    return false;
  }

  public void KickPlayer(int userId, string name, string steamid64)
  {
    Server.NextFrame(() =>
    {
      Server.ExecuteCommand($"kickid {(ushort)userId} {Localizer["KickReason"].Value}");
      if (Config.SendMessageOnChatAfterKick) Server.PrintToChatAll(Localizer["KickMessageOnChat", name, steamid64].Value);
    });
  }

  public void CheckFile()
  {
    // 修正路徑讀取方式，確保跨平台穩定
    string path = Path.Combine(ModuleDirectory, "whitelist.txt");

    Task.Run(async () =>
    {
      if (!File.Exists(path))
      {
        await File.WriteAllTextAsync(path, "76561198119188837 //範例ID");
      }

      // 讀取所有行並徹底清除不可見字元與註解
      string[] lines = await File.ReadAllLinesAsync(path);
      
      WhiteListValues = lines
        .Select(line => line.Split("//")[0].Trim()) // 先切除註解再 Trim
        .Where(line => !string.IsNullOrWhiteSpace(line)) // 過濾掉空白行
        .ToArray();

      Logger.LogInformation($"[WhiteList] 檔案已讀取，載入 {WhiteListValues.Length} 個白名單項目。");
    });
  }
  
  // 其餘 GetSteamGroupsId, CheckVersion 等保持不變...
}
