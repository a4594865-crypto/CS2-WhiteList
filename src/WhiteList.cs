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

    RegisterListener<OnClientAuthorized>(OnClientAuthorized);

    AddCommand($"css_{Config.Commands.Add}", "Add to list", Add);
    AddCommand($"css_{Config.Commands.Remove}", "Remove from list", Remove);
    
    // 這裡保留您原本設定檔定義的切換指令
    AddCommand($"css_{Config.Commands.Toggle}", "Toggle Whitelist", ToggleWhitelist);
    
    // 新增：明確註冊 css_whitelist，這樣玩家就能在聊天欄用 !whitelist
    // CommandUsage.ALL 代表玩家(CLIENT)與伺服器控制台(SERVER)都能用
    AddCommand("css_whitelist", "Toggle whitelist via chat or console", ToggleWhitelistUniversal);

    CheckVersion();

    if (Config.UseDatabase)
    {
      BuildDatabaseConnectionString();
      CheckDatabaseTables();
    }
    else
    {
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

  // 您原本的 Toggle 指令邏輯 (保留給設定檔指定的指令使用)
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

  private void ExecuteToggle(string name)
  {
    Config.Enabled = !Config.Enabled;
    string colorStatus = Config.Enabled ? "\x06開啟" : "\x02關閉";
    string plainStatus = Config.Enabled ? "開啟" : "關閉";
    
    Server.PrintToChatAll($"\x01[\x0B 管理員 \x01]  \x03{name}\x01 將白名單設定：{colorStatus}");
    Logger.LogInformation($"[WhiteList] {name} toggled whitelist to: {plainStatus}");
  }

  public FakeConVar<bool> Convar_isPluginEnabled = new("plugin_whitelist_enabled", "Enable WhiteList", true);
  public FakeConVar<bool> Convar_useAsBlacklist = new("plugin_whitelist_useasblacklist", "Use as blacklist", false);
}
