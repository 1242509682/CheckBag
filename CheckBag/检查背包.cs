using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using static System.Net.Mime.MediaTypeNames;

namespace CheckBag
{
    [ApiVersion(2, 1)]
    public class CheckBag : TerrariaPlugin
    {
        public override string Name => "检查背包(超进度物品检测)";
        public override string Author => "hufang360 修改：羽学";
        public override string Description => "定时检查玩家背包，删除违禁物品，满足次数封禁对应玩家。";
        public override Version Version => new Version(1, 9, 0, 0);

        string SaveDir = Path.Combine(TShock.SavePath, "检查背包");
        Config _config;
        int Count = -1;

        public CheckBag(Main game) : base(game)
        {
        }

        public static class PlayerItemSlotID
        {
            public static readonly int Inventory0;

            public static readonly int InventoryMouseItem;

            public static readonly int Armor0;

            public static readonly int Dye0;

            public static readonly int Misc0;

            public static readonly int MiscDye0;

            public static readonly int Bank1_0;

            public static readonly int Bank2_0;

            public static readonly int TrashItem;

            public static readonly int Bank3_0;

            public static readonly int Bank4_0;

            public static readonly int Loadout1_Armor_0;

            public static readonly int Loadout1_Dye_0;

            public static readonly int Loadout2_Armor_0;

            public static readonly int Loadout2_Dye_0;

            public static readonly int Loadout3_Armor_0;

            public static readonly int Loadout3_Dye_0;

            public static bool[] CanRelay;

            public static int _nextSlotId;

            static PlayerItemSlotID()
            {
                CanRelay = new bool[0];
                Inventory0 = AllocateSlots(58, canNetRelay: true);
                InventoryMouseItem = AllocateSlots(1, canNetRelay: true);
                Armor0 = AllocateSlots(20, canNetRelay: true);
                Dye0 = AllocateSlots(10, canNetRelay: true);
                Misc0 = AllocateSlots(5, canNetRelay: true);
                MiscDye0 = AllocateSlots(5, canNetRelay: true);
                Bank1_0 = AllocateSlots(40, canNetRelay: false);
                Bank2_0 = AllocateSlots(40, canNetRelay: false);
                TrashItem = AllocateSlots(1, canNetRelay: false);
                Bank3_0 = AllocateSlots(40, canNetRelay: false);
                Bank4_0 = AllocateSlots(40, canNetRelay: false);
                Loadout1_Armor_0 = AllocateSlots(20, canNetRelay: true);
                Loadout1_Dye_0 = AllocateSlots(10, canNetRelay: true);
                Loadout2_Armor_0 = AllocateSlots(20, canNetRelay: true);
                Loadout2_Dye_0 = AllocateSlots(10, canNetRelay: true);
                Loadout3_Armor_0 = AllocateSlots(20, canNetRelay: true);
                Loadout3_Dye_0 = AllocateSlots(10, canNetRelay: true);
            }

            public static int AllocateSlots(int amount, bool canNetRelay)
            {
                int nextSlotId = _nextSlotId;
                _nextSlotId += amount;
                int num = CanRelay.Length;
                Array.Resize(ref CanRelay, num + amount);
                for (int i = num; i < _nextSlotId; i++)
                {
                    CanRelay[i] = canNetRelay;
                }

                return nextSlotId;
            }
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// 

        public override void Initialize()
        {
            if (!Directory.Exists(SaveDir))
            {
                Directory.CreateDirectory(SaveDir);
            }
            LoadConfig();

            Commands.ChatCommands.Add(new Command("cbag", CBCommand, "cbag", "检查背包") { HelpText = "检查背包" });
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
        }


        /// <summary>
        /// 加载配置文件
        /// </summary>
        void LoadConfig()
        {
            _config = Config.Load(Path.Combine(SaveDir, "检查背包.json"));
        }

        /// <summary>
        /// 处理指令
        /// </summary>
        void CBCommand(CommandArgs args)
        {
            TSPlayer op = args.Player;
            void Help()
            {
                List<string> lines = new()
                {
                    "/cbag ban，列出封禁记录",
                    "/cbag item，列出违规物品",
                    "/cbag reload，重载配置",
                };
                op.SendInfoMessage(string.Join("\n", lines));
            }

            if (args.Parameters.Count == 0)
            {
                op.SendErrorMessage("语法错误，输入 /cbag help 查询用法");
                return;
            }

            switch (args.Parameters[0].ToLowerInvariant())
            {
                // 帮助
                case "help":
                case "h":
                case "帮助":
                    Help();
                    break;

                // 查看封禁
                case "ban":
                case "b":
                    Ban.ListBans(args);
                    break;

                // 物品
                case "item":
                case "i":
                    ListItems(args);
                    break;

                // 重载配置
                case "reload":
                case "r":
                    LoadConfig();
                    args.Player.SendSuccessMessage("[检查背包]重载配置完成");
                    break;

                default:
                    op.SendErrorMessage("语法不正确！输入/cbag help查询用法");
                    break;
            }
        }


        /// <summary>
        /// 世界更新（有玩家时，每秒更新60次）
        /// </summary>
        void OnGameUpdate(EventArgs args)
        {
            // 如果计数器不为-1且计数器小于检测间隔的分钟数，则增加计数器并返回
            if (Count != -1 && Count < _config.检测间隔 * 60)
            {
                Count++;
                return;
            }
            Count = 0;

            // 对每个在线并活跃的玩家执行以下操作
            TShock.Players.Where(p => p != null && p.Active).ToList().ForEach(p =>
            {
                // 如果玩家未登录或者玩家组具有“owner”权限或者玩家组具有“免检背包”权限，则返回
                if (!p.IsLoggedIn || p.Group.HasPermission("owner") || p.Group.HasPermission("免检背包")) // 添加“免检背包”权限检查
                    return;

                // 清除玩家的物品
                ClearPlayersItem(p);
            });
        }

        /// <summary>
        /// 检查玩家背包
        /// </summary>
        /// <param name="items"> 需要清空的物品 </param>
        /// <param name="op"> 需要被清理的玩家 </param>
        public void ClearPlayersItem( TSPlayer op)

        {

            Player plr = op.TPlayer;
            Dictionary<int, int> dict = new();
            List<Item> list = new();
            list.AddRange(plr.inventory); // 背包,钱币/弹药,手持
            list.Add(plr.trashItem); // 垃圾桶
            list.AddRange(plr.armor); // 装备,时装
            list.AddRange(plr.dye); // 染料
            list.AddRange(plr.miscEquips); // 工具栏
            list.AddRange(plr.miscDyes); // 工具栏染料
            list.AddRange(plr.bank.item); // 储蓄罐
            list.AddRange(plr.bank2.item); // 保险箱
            list.AddRange(plr.bank3.item); // 护卫熔炉
            list.AddRange(plr.bank4.item); // 虚空保险箱
            for (int i = 0; i < plr.Loadouts.Length; i++)
            {
                // 装备1,装备2,装备3
                list.AddRange(plr.Loadouts[i].Armor); // 装备,时装
                list.AddRange(plr.Loadouts[i].Dye); // 染料
            }
            list.RemoveAll(i => i.IsAir); //移除所有的空白物品
            list.Where(item => item != null && item.active).ToList().ForEach(item =>
            {
                if (dict.ContainsKey(item.netID))
                {
                    dict[item.netID] += item.stack;
                }
                else
                {
                    dict.Add(item.netID, item.stack);
                }
            });

            bool Check(List<ItemData> li, bool isCurrent)
            {
                ItemData data = null;
                foreach (var d in li)
                {
                    var id = d.id;
                    var stack = d.数量;
                    if (dict.ContainsKey(id) && dict[id] >= stack)
                    {
                        data = d;
                        break;
                    }
                }

                //string logFilePath = "log " + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                //if (!File.Exists(logFilePath))
                //{
                //    File.Create(logFilePath).Close();
                //}

                if (data != null)
                {
                    var name = op.Name;
                    var id = data.id;
                    var stack = data.数量;
                    var max = _config.警告次数;
                    var num = Ban.Trigger(name);
                    string itemName = Lang.GetItemNameValue(id);
                    string itemDesc = stack > 1 ? $"{itemName}x{stack}" : itemName;
                    string opDesc = isCurrent ? "拥有超进度物品" : "拥有";
                    var desc = $"{opDesc}[i/s{stack}:{id}]{itemDesc}";
                    if (num < max)
                    {
                        string tips = stack > 1 ? "请减少数量" : "请销毁";
                        TSPlayer.All.SendSuccessMessage($"玩家[c/FFCCFF:{name}]被检测到{desc}，疑似作弊请注意！"); // 发送广播消息
                        Console.WriteLine($"玩家[{name}]被检测到{desc}，疑似作弊请注意！"); // 控制台输出

                        string logFolderPath = Path.Combine(TShock.SavePath, "检查背包", "检查日志"); //写入日志的路径
                        Directory.CreateDirectory(logFolderPath); // 创建日志文件夹

                        string logFileName = $"log {DateTime.Now.ToString("yyyy-MM-dd")}.txt"; //给日志名字加上日期

                        File.AppendAllLines(Path.Combine(logFolderPath, logFileName), new string[] { DateTime.Now.ToString("u") + $"玩家【{name}】被检测到{desc}，疑似作弊请注意！"}); //写入日志log

                        HashSet<int> checkedItems = new HashSet<int>(); // 用于存储已经检查过的物品类型
                        // 清除检测到的项目
                            for (int i = 0; i < plr.inventory.Length; i++)
                    {
                            if (plr.inventory[i].type == id &&  plr.inventory[i].stack >= stack)
                            {
                                plr.inventory[i].TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Inventory0 + i);
                            }
                    }
                        for (int i = 0; i < plr.bank4.item.Length; i++)
                    {
                            if (plr.bank4.item[i].type == id && plr.bank4.item[i].stack >= stack)
                            {
                                plr.bank4.item[i].TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Bank4_0 + i);
                            }
                    }
                            for (int i = 0; i < plr.bank3.item.Length; i++)
                    {
                            if (plr.bank3.item[i].type == id && plr.bank3.item[i].stack >= stack)
                            {
                                plr.bank3.item[i].TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Bank3_0 + i);
                            }
                    }
                            for (int i = 0; i < plr.bank2.item.Length; i++)
                    {
                            if (plr.bank2.item[i].type == id && plr.bank2.item[i].stack >= stack)
                            {
                                plr.bank2.item[i].TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Bank2_0 + i);
                            }
                    }
                        for (int i = 0; i < plr.bank.item.Length; i++)
                        {
                            if (plr.bank.item[i].type == id && plr.bank.item[i].stack >= stack)
                            {
                                plr.bank.item[i].TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Bank1_0 + i);
                            }
                        }
                            for (int i = 0; i < plr.armor.Length; i++)
                    {
                            if (plr.armor[i].type == id && plr.armor[i].stack >= stack)
                            {
                                plr.armor[i].TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Armor0 + i);
                            }
                    }
                            for (int i = 0; i < plr.dye.Length; i++)
                    {
                            if (plr.dye[i].type == id && plr.dye[i].stack >= stack)
                            {
                                plr.dye[i].TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Dye0 + i);
                            }
                    }
                            for (int i = 0; i < plr.miscDyes.Length; i++)
                    {
                            if (plr.miscDyes[i].type == id && plr.miscDyes[i].stack >= stack)
                            {
                                plr.miscDyes[i].TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.MiscDye0 + i);
                            }
                    }
                            for (int i = 0; i < plr.miscEquips.Length; i++)
                    {
                            if (plr.miscEquips[i].type == id && plr.miscEquips[i].stack >= stack)
                            {
                               plr.miscEquips[i].TurnToAir();
                               op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Misc0 + i);
                            }
                    }
                            //遍历垃圾桶
                            if (plr.trashItem.IsAir && plr.trashItem.type >= stack)
                            {
                                plr.trashItem.TurnToAir();
                                op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.TrashItem);
                            }
                            for (int i = 0; i < plr.Loadouts[0].Armor.Length; i++)
                            {
                                    if (plr.Loadouts[0].Armor[i].type == id && plr.Loadouts[0].Armor[i].stack >= stack)
                                    {
                                        plr.Loadouts[0].Armor[i].TurnToAir();
                                        op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Loadout1_Armor_0 + i);
                                    }
                            }
                            for (int i = 0; i < plr.Loadouts[0].Dye.Length; i++)
                            {
                                if (plr.Loadouts[0].Dye[i].type == id && plr.Loadouts[0].Dye[i].stack >= stack)
                                {
                                    plr.Loadouts[0].Dye[i].TurnToAir();
                                    op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Loadout1_Dye_0 + i);
                                }
                            }
                            for (int i = 0; i < plr.Loadouts[1].Armor.Length; i++)
                            {
                                if (plr.Loadouts[1].Armor[i].type == id && plr.Loadouts[1].Armor[i].stack >= stack)
                                {
                                    plr.Loadouts[1].Armor[i].TurnToAir();
                                    op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Loadout2_Armor_0 + i);
                                }
                            }
                            for (int i = 0; i < plr.Loadouts[1].Dye.Length; i++)
                            {
                                if (plr.Loadouts[1].Dye[i].type == id && plr.Loadouts[1].Dye[i].stack >= stack)
                                {
                                    plr.Loadouts[1].Dye[i].TurnToAir();
                                    op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Loadout2_Dye_0 + i);
                                }
                            }
                            for (int i = 0; i < plr.Loadouts[2].Armor.Length; i++)
                            {
                                if (plr.Loadouts[2].Armor[i].type == id && plr.Loadouts[2].Armor[i].stack >= stack)
                                {
                                    plr.Loadouts[2].Armor[i].TurnToAir();
                                    op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Loadout3_Armor_0 + i);
                                }
                            }
                            for (int i = 0; i < plr.Loadouts[2].Dye.Length; i++)
                            {
                                if (plr.Loadouts[2].Dye[i].type == id && plr.Loadouts[2].Dye[i].stack >= stack)
                                {
                                    plr.Loadouts[2].Dye[i].TurnToAir();
                                    op.SendData(PacketTypes.PlayerSlot, "", op.Index, PlayerItemSlotID.Loadout3_Dye_0 + i);
                                }
                            }
                        
                        //清理BUFF
                        for (int i = 0; i < 22; i++)
                        {
                            op.TPlayer.buffType[i] = 0;
                        }
                        op.SendData(PacketTypes.PlayerBuff, "", op.Index, 0f, 0f, 0f, 0);
                    }
                    else

                    {
                        Ban.Remove(name);
                        TSPlayer.All.SendInfoMessage($"{name}已被封禁！原因：{desc}。");
                        op.Disconnect($"你已被封禁！原因：{opDesc}{itemDesc}。");
                        Ban.AddBan(name, $"{opDesc}{itemDesc}", _config.封禁时长 * 60);
                        return false;
                    }
                }
                return true;
            }

            if (!Check(_config.全时期, false))
                return;
            Check(_config.Current(), true);
        }

        /// <summary>
        /// 列出违规物品
        /// </summary>
        public void ListItems(CommandArgs args)
        {
            static string FormatData(ItemData data)
            {
                var id = data.id;
                var stack = data.数量;
                var itemName = Lang.GetItemNameValue(id);
                var itemDesc = stack > 1 ? $"{itemName}x{stack}" : itemName;
                return $"[i/s{stack}:{id}]{itemDesc}";
            }

            var lines = new List<string>();

            var datas = _config.Current();
            var lines2 = datas.Select(d => FormatData(d)).ToList();
            lines.AddRange(WarpLines(lines2));
            if (datas.Count > 0)
            {
                lines[0] = "[c/FFCCFF:当前进度：]" + lines[0];
            }

            if (!lines.Any())
            {
                if (_config.IsEmpty())
                {
                    args.Player.SendInfoMessage("你未配置任何违规物品数据！");
                }
                else
                {
                    args.Player.SendInfoMessage("没有符合当前进度的物品！");
                }
                return;
            }

            if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out int pageNumber))
            {
                return;
            }
            PaginationTools.SendPage(args.Player, pageNumber, lines, new PaginationTools.Settings
            {
                HeaderFormat = "违规物品 ({0}/{1})：",
                FooterFormat = "输入/cbag i {{0}}查看更多".SFormat(Commands.Specifier)
            });
        }

        /// <summary>
        /// 将字符串换行
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="column">列数，1行显示多个</param>
        /// <returns></returns>
        static List<string> WarpLines(List<string> lines, int column = 15)
        {
            List<string> li1 = new();
            List<string> li2 = new();
            foreach (var line in lines)
            {
                if (li2.Count % column == 0)
                {
                    if (li2.Count > 0)
                    {
                        li1.Add(string.Join("\n", li2));
                        li2.Clear();
                    }
                }
                li2.Add(line);
            }
            if (li2.Any())
            {
                li1.Add(string.Join("\n", li2));
            }
            return li1;
        }

        /// <summary>
        /// 销毁
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            }
            base.Dispose(disposing);
          }
    }
}