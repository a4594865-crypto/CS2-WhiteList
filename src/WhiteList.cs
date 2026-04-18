using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;

namespace WhiteList;

public partial class WhiteList : BasePlugin, IPluginConfig<Config>
{
  public override string ModuleName => "WhiteList";
  public override string ModuleDescription => "Allow or block players from a list on database or file";
  public override string ModuleAuthor => "1MaaaaaacK";
  public override string ModuleVersion => "1.0.3";
  public static int ConfigVersion => 3;
  public string[] WhiteListValues = [];

  public override void Load(bool hotReload)
  {
    if (Config.UsePrivateFeature)
    {
      if (hotReload)
      {
        Config.Enabled = Convar_isPluginEnabled.Value;
        Config.UseAsBlacklist = Convar_useAsBlacklist.Value;
      }
      Convar_isPluginEnabled.ValueChanged += (_, value) => { Config.Enabled = value; };
      Convar_useAsBlacklist.ValueChanged += (_, value) => { Config.UseAsBlacklist = value; };
    }

    // 註冊監聽器，實作位於 Events.cs
    RegisterListener<OnClientAuthorized>(OnClientAuthorized);

    // 註冊指令
    AddCommand($"css_{Config.Commands.Add}", "Add to list", Add);
    AddCommand($"css_{Config.Commands.Remove}", "Remove from list", Remove);
    
    // 1. 註冊設定檔定義的切換指令
    AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist", ToggleWhitelist);
    
    // 2. 新增：註冊 css_whitelist，支援聊天欄位 !whitelist 與伺服器控制台指令
    AddCommand("css_whitelist", "Toggle whitelist via chat or console", ToggleWhitelistUniversal);

    CheckVersion();

    if (Config.UseDatabase)
    {
      BuildDatabaseConnectionString();
      CheckDatabaseTables();
    }
    else
    {
      // 呼叫位於 Utility.cs 中的 CheckFile 方法
      CheckFile();
    }
  }

  // 萬用切換邏輯：支援玩家與控制台
  [CommandHelper(whoCanExecute: CommandUsage.ALL)]
  public void ToggleWhitelistUniversal(CCSPlayerController? player, CommandInfo command)
  {
    // 如果是玩家執行的，檢查權限
    if (player != null && !AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
    {
      command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
      return;
    }

    string setterName = (player == null) ? "控制台" : player.PlayerName;
    ExecuteToggle(setterName);
  }

  // 原本的 Toggle 指令邏輯 (保留給設定檔指定的指令使用)
  [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
  public void ToggleWhitelist(CCSPlayerController? player, CommandInfo command)
  {
    if (player == null) return;
    if (!AdminManager.PlayerHasPermissions(player, Config.Commands.TogglePermission))
    {
        command.ReplyToCommand($"{Localizer["Prefix"]} {Localizer["MissingCommandPermission"]}");
        return;
    }
    ExecuteToggle(player.PlayerName);
  }

  // 核心執行邏輯：包含自動重新載入檔案
  private void ExecuteToggle(string setterName)
  {
    Config.Enabled = !Config.Enabled;
    
    // 關鍵修正：當白名單變更為「開啟」時，重新讀取 whitelist.txt 以套用新 ID
    if (Config.Enabled && !Config.UseDatabase)
    {
        CheckFile();
        Logger.LogInformation("[WhiteList] 偵測到白名單開啟，已自動重新載入 whitelist.txt");
    }

    string status = Config.Enabled ? "\x06開啟" : "\x02關閉";
    
    // 全服廣播通知
    Server.PrintToChatAll($"\x01[\x0B 管理員 \x01]  \x03{setterName}\x01 將白名單設定：{status}");
    Logger.LogInformation($"Admin {setterName} toggled Whitelist to: {Config.Enabled}");
  }

  public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
  public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
