﻿using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using SebbyLib;

namespace OneKeyToWin_AIO_Sebby
{
    class Ashe
    {
        private Menu Config = Program.Config;
        public static Orbwalking.Orbwalker Orbwalker = Program.Orbwalker;
        public Spell Q, W, E, R;
        public float QMANA = 0, WMANA = 0, EMANA = 0, RMANA = 0;
        public Obj_AI_Hero Player { get { return ObjectManager.Player; }}

        private void LoadMenuOKTW()
        {
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw only ready spells", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("wRange", "W range", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("farmQ", "Lane clear Q", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("farmW", "Lane clear W", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("Mana", "LaneClear Mana", true).SetValue(new Slider(80, 100, 30)));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("jungleQ", "Jungle clear Q", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("jungleW", "Jungle clear W", true).SetValue(true));

            Config.SubMenu(Player.ChampionName).SubMenu("Q Config").AddItem(new MenuItem("harasQ", "Harass Q", true).SetValue(true));

            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu(Player.ChampionName).SubMenu("W Config").SubMenu("Harras W").AddItem(new MenuItem("haras" + enemy.ChampionName, enemy.ChampionName).SetValue(true));

            Config.SubMenu(Player.ChampionName).SubMenu("E Config").AddItem(new MenuItem("autoE", "Auto E", true).SetValue(true));
            
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("autoR", "Auto R", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("Rkscombo", "R KS combo R + W + AA", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("autoRaoe", "Auto R aoe", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("autoRinter", "Auto R OnPossibleToInterrupt", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("useR", "Semi-manual cast R key", true).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press))); //32 == space
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu(Player.ChampionName).SubMenu("R Config").SubMenu("GapCloser R").AddItem(new MenuItem("GapCloser" + enemy.ChampionName, enemy.ChampionName).SetValue(false));
        }

        public void LoadOKTW()
        {
            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W, 1260);
            E = new Spell(SpellSlot.E, 2500);
            R = new Spell(SpellSlot.R, 3000f);

            W.SetSkillshot(0.25f, 20f , 1500f, true, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.25f, 299f, 1400f, false, SkillshotType.SkillshotLine);
            R.SetSkillshot(0.25f, 130f, 1600f, false, SkillshotType.SkillshotLine);
            LoadMenuOKTW();

            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Orbwalking.BeforeAttack += BeforeAttack;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget +=Interrupter2_OnInterruptableTarget;
        }

        private void Interrupter2_OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (Config.Item("autoRinter", true).GetValue<bool>() && R.IsReady() && sender.IsValidTarget(R.Range))
                R.Cast(sender);
        }

        private void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (R.IsReady() )
            {
                var Target = gapcloser.Sender;
                if (Target.IsValidTarget(800) && Config.Item("GapCloser" + Target.ChampionName).GetValue<bool>())
                {
                    R.Cast(Target.ServerPosition, true);
                    Program.debug("AGC " + Target.ChampionName);
                }
            }
        }

        private void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            LogicQ();
        }

        private void Game_OnUpdate(EventArgs args)
        {

            if (R.IsReady())
            {
                if (Config.Item("useR", true).GetValue<KeyBind>().Active)
                {
                    var t = TargetSelector.GetTarget(2000, TargetSelector.DamageType.Physical);
                    if (t.IsValidTarget())
                        R.Cast(t, true, true);
                }
            }
            if (Program.LagFree(1))
            {
                SetMana();
            }

            if (Program.LagFree(3) && W.IsReady() && !Player.IsWindingUp)
                LogicW();

            if (Program.LagFree(4) && R.IsReady())
                LogicR();
        }

        private void LogicR()
        {
            if (Config.Item("autoR", true).GetValue<bool>())
            {
                foreach (var target in Program.Enemies.Where(target => target.IsValidTarget(R.Range) && OktwCommon.ValidUlt(target)))
                {
                    var rDmg = OktwCommon.GetKsDamage(target, R);
                    if (Program.Combo && target.CountEnemiesInRange(250) > 2 && Config.Item("autoRaoe", true).GetValue<bool>())
                        Program.CastSpell(R, target);
                    if(Program.Combo && target.IsValidTarget(W.Range)  && Config.Item("Rkscombo", true).GetValue<bool>() &&  Player.GetAutoAttackDamage(target) * 5 + rDmg + W.GetDamage(target) > target.Health && target.HasBuffOfType(BuffType.Slow) && !OktwCommon.IsSpellHeroCollision(target, R))
                        Program.CastSpell(R, target);
                    if (rDmg > target.Health && target.CountAlliesInRange(600) == 0 && target.Distance(Player.Position) > 1000)
                    {
                        if (!OktwCommon.IsSpellHeroCollision(target, R))
                            Program.CastSpell(R, target);
                    }
                }
            }

            if (Player.HealthPercent < 50)
            {
                foreach (var enemy in Program.Enemies.Where(enemy => enemy.IsValidTarget(300) && enemy.IsMelee && Config.Item("GapCloser" + enemy.ChampionName).GetValue<bool>() && !OktwCommon.ValidUlt(enemy)))
                {
                    R.Cast(enemy);
                    Program.debug("R Meele");
                }
            }
        }

        private void LogicQ()
        {
            if (Orbwalker.GetTarget() == null)
                return;
            var target = Orbwalker.GetTarget();
            if (target.IsValid && target is Obj_AI_Hero)
            {
                if (Program.Combo && (Player.Mana > RMANA + QMANA || target.Health <  5 * Player.GetAutoAttackDamage(Player)))
                    Q.Cast();
                else if (Program.Farm && (Player.Mana > RMANA + QMANA + WMANA) && Config.Item("harasQ", true).GetValue<bool>())
                    Q.Cast();
            }
        }

        private void LogicW()
        {
            var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical);
            if (Player.CountEnemiesInRange(700) > 0)
                t = TargetSelector.GetTarget(700, TargetSelector.DamageType.Physical);
            if (t.IsValidTarget())
            {
                SebbyLib.Prediction.SkillshotType CoreType2 = SebbyLib.Prediction.SkillshotType.SkillshotLine;

                var predInput2 = new SebbyLib.Prediction.PredictionInput
                {
                    Aoe = false,
                    Collision = W.Collision,
                    Speed = W.Speed,
                    Delay = W.Delay,
                    Range = W.Range,
                    From = Player.ServerPosition,
                    Radius = W.Width,
                    Unit = t,
                    Type = CoreType2
                };
                var poutput2 = SebbyLib.Prediction.Prediction.GetPrediction(predInput2);

                if (poutput2.Hitchance >= SebbyLib.Prediction.HitChance.High)
                {
                    if (Program.Combo && Player.Mana > RMANA + WMANA)
                        W.Cast(poutput2.CastPosition);
                    else if (Program.Farm && Config.Item("haras" + t.ChampionName).GetValue<bool>() && Player.Mana > RMANA + WMANA + QMANA + WMANA && OktwCommon.CanHarras())
                        W.Cast(poutput2.CastPosition);
                }
                
                if(OktwCommon.GetKsDamage(t, W) > t.Health)
                {
                    W.Cast(t);
                    W.Cast(poutput2.CastPosition);
                }

                if (!Program.None && Player.Mana > RMANA + WMANA)
                {
                    foreach (var enemy in Program.Enemies.Where(enemy => enemy.IsValidTarget(W.Range) && !OktwCommon.CanMove(enemy)))
                        W.Cast(t);
                }
            }
        }

        private void SetMana()
        {
            if ((Config.Item("manaDisable", true).GetValue<bool>() && Program.Combo) || Player.HealthPercent < 20)
            {
                QMANA = 0;
                WMANA = 0;
                EMANA = 0;
                RMANA = 0;
                return;
            }

            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;

            if (!R.IsReady())
                RMANA = WMANA - Player.PARRegenRate * W.Instance.Cooldown;
            else
                RMANA = R.Instance.ManaCost;
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("wRange", true).GetValue<bool>())
            {
                if (Config.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (W.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
            }
        }
        
    }
}
