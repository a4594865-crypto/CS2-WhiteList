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

      if (whitelisted != null && whitelisted.Any())
        return true;
    }
    else
    {
      // 修正比對邏輯，確保能正確在讀取到的清單中搜尋玩家 ID
      if (WhiteListValues.Any(v => value.Contains(v)))
        return true;
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
    // 使用 ModuleDirectory 獲取更穩定的路徑
    string path = Path.Combine(ModuleDirectory, "whitelist.txt");

    Task.Run(async () =>
    {
      if (!File.Exists(path))
      {
        await File.WriteAllTextAsync(
          path,
          @$"// Use '//' to have comments
// You can insert IP, STEAMID, STEAMID64 and STEAMID3
// 76561198119188837
          ");
      }

      // 讀取所有行
      string[] lines = await File.ReadAllLinesAsync(path, System.Text.Encoding.UTF8);

      // 核心修正：
      // 1. 先排除掉純註解行
      // 2. 切開 '//' 只取前半段 ID 部分
      // 3. 使用 Trim() 徹底移除包含 \r 在內的空白字元
      WhiteListValues = lines
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith("//") && !string.IsNullOrWhiteSpace(line))
        .Select(line => line.Split("//")[0].Trim())
        .ToArray();

      Logger.LogInformation($"[WhiteList] 成功載入 {WhiteListValues.Length} 個 ID。");
    });
  }

  public async Task<bool> SetToFile(string[] values, bool isInsert)
  {
    try
    {
      string path = Path.Combine(ModuleDirectory, "whitelist.txt");
      IEnumerable<string> result = isInsert
      ? WhiteListValues.Concat(values.Except(WhiteListValues))
      : WhiteListValues.Except(values);

      await File.WriteAllLinesAsync(path, result);
      WhiteListValues = result.ToArray();
      return true;
    }
    catch (Exception)
    {
      return false;
    }
  }

  public async Task<List<string>?> GetSteamGroupsId(string steamid)
  {
    try
    {
      using var httpClient = new HttpClient();
      JsonElement jsonData = await httpClient.GetFromJsonAsync<JsonElement>($"https://api.steampowered.com/ISteamUser/GetUserGroupList/v1/?key={Config.SteamGroup.Apikey}&steamid={steamid}");
      
      if (!jsonData.TryGetProperty("response", out var responseProperty) ||
          responseProperty.ValueKind != JsonValueKind.Object)
      {
        Logger.LogError("An error occurred: Response is null or not an object.");
        return null;
      }

      if (!responseProperty.GetProperty("success").GetBoolean())
      {
        return null;
      }
      List<string> groupsId = [];

      foreach (var group in responseProperty.GetProperty("groups").EnumerateArray())
      {
        string? groupId = group.GetProperty("gid").GetString();
        if (!string.IsNullOrEmpty(groupId))
          groupsId.Add(groupId);
      }
      return groupsId;
    }
    catch (Exception e)
    {
      Logger.LogError(e.Message);
      return null;
    }
  }

  internal class IRemoteVersion
  {
    public required string tag_name { get; set; }
  }

  public void CheckVersion()
  {
    Task.Run(async () =>
    {
      using HttpClient client = new();
      try
      {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WhiteList");
        HttpResponseMessage response = await client.GetAsync("https://api.github.com/repos/1Mack/CS2-WhiteList/releases/latest");

        if (response.IsSuccessStatusCode)
        {
          IRemoteVersion? toJson = JsonSerializer.Deserialize<IRemoteVersion>(await response.Content.ReadAsStringAsync());

          if (toJson == null)
          {
            Logger.LogWarning("Failed to check version1");
          }
          else
          {
            int comparisonResult = string.Compare(ModuleVersion, toJson.tag_name[1..]);

            if (comparisonResult < 0)
            {
              Logger.LogWarning("Plugin is outdated! Check https://github.com/1Mack/CS2-WhiteList/releases/latest");
            }
            else if (comparisonResult > 0)
            {
              Logger.LogInformation("Probably dev version detected");
            }
            else
            {
              Logger.LogInformation("Plugin is up to date");
            }
          }
        }
        else
        {
          Logger.LogWarning("Failed to check version2");
        }
      }
      catch (HttpRequestException ex)
      {
        Logger.LogError(ex, "Failed to connect to the version server.");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "An error occurred while checking version.");
      }
    });
  }
}
