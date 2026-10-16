using System;
using System.Collections.Generic;
using System.IO;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    /// <summary>Finite data-only acceptance checks for automated Editor tests.</summary>
    public static class ContentAcceptanceRunner
    {
        const float ComparisonTolerance = .0001f;
        public static ContentAcceptanceReport Run(ContentAcceptanceManifestAsset manifest)
        {
            var report = new ContentAcceptanceReport { Manifest = manifest == null ? "" : manifest.name,
                Utc = DateTime.UtcNow.ToString("O"), UnityVersion = UnityEngine.Application.unityVersion };
            string directory = null;
            try
            {
                Require(manifest != null && manifest.preset != null, "manifest.preset: assign an acceptance preset");
                var balance = manifest.preset.CreateBalance(); var content = balance.ResolveContent();
                var weapon = content.GetWeapon(manifest.newWeaponId); var module = content.GetModule(manifest.newModuleId); var chassis = content.GetChassis(manifest.newChassisId);
                Require(!weapon.LegacyKind.HasValue && module.Module == TowerModules.None && !chassis.Chassis.HasValue,
                    "manifest: A/C/D must reference new content IDs without legacy enum mappings");
                Equal(weapon.Stats.Cost, manifest.expectedWeaponCost, "A.weapon.cost"); Equal(weapon.Load, manifest.expectedWeaponLoad, "A.weapon.load");
                Equal(weapon.Stats.Range, manifest.expectedWeaponRange, "A.weapon.range"); Equal(weapon.Stats.ShotInterval, manifest.expectedWeaponInterval, "A.weapon.interval");
                Equal(weapon.UnlockGold, manifest.expectedWeaponUnlockGold, "A.weapon.unlockGold");
                directory = Path.Combine(Path.GetTempPath(), "PaperContentSmoke-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory); var path = Path.Combine(directory, "profile.json");
                report.IsolatedProfile = path;
                var profile = new WorkshopProfile(content, balance); profile.GrantGold(manifest.startingGold);
                int before = profile.Gold;
                Require(profile.Unlock(manifest.newWeaponId, out var reason, balance), "A.unlock: " + reason);
                Equal(before - profile.Gold, weapon.InitiallyUnlocked ? 0 : manifest.expectedWeaponUnlockGold, "A.unlock.payment");
                var basic = Authored("acceptance.weapon", "Acceptance weapon", manifest.baselineChassisId, manifest.newWeaponId, null, content);
                Equal(TowerCatalog.Cost(basic, balance, MaterialTier.Pencil), manifest.expectedAssemblyCost, "A.assembly.price");
                Require(profile.SaveBlueprint(basic, out reason, balance), "A.save: " + reason);
                Require(profile.AssignSlot(0, basic.Id, out reason, balance), "A.slot: " + reason);
                var store = new WorkshopProfileStore(path); store.Load(); Require(store.Save(profile).Success, "A.profile.write");
                var restarted = new WorkshopProfileStore(path).Load(); Require(restarted.CanWrite, "A.profile.restart: " + restarted.Message);
                var restored = restarted.Profile.GetLoadoutBlueprints()[0].WithContent(content);
                Require(restored.FirstWeaponId == manifest.newWeaponId && restored.Drawing.GeometryHash == basic.Drawing.GeometryHash, "A.profile.idAndDrawing");
                var preview = new TowerPreview(restored, balance, MaterialTier.Pencil);
                Require(preview.Valid, "A.preview: " + preview.Reason); Equal(preview.Cost, manifest.expectedAssemblyCost, "A.preview.price");
                var trial = TrialRunner.Run(restored, balance, manifest.trialScenario, manifest.trialBudget, manifest.seed);
                Require(trial.Copies > 0 && trial.Damage > 0, "A.trial: no real damage");
                var single = Probe(restored, balance, manifest, new[] { new Point2(manifest.probeTargetOffset.x, manifest.probeTargetOffset.y) });
                Require(single.Launches > 0 && single.TargetDamage[0] > 0, "A.attack: no real hit");
                report.Checks.Add("A: catalog, unlock payment, drawing, isolated save/restart, preview, trial and production hit");

                var variant = Authored("acceptance.geometry", "Acceptance geometry", manifest.baselineChassisId, manifest.variantWeaponId, null, content);
                if (content.GetWeapon(manifest.variantWeaponId).SupportsFootprint)
                    variant = variant.WithMortarFootprint(0, MortarFootprint.CreateStored(manifest.variantAspectRatio));
                Require(manifest.variantTargets != null && manifest.variantExpectedHits != null && manifest.variantTargets.Length == manifest.variantExpectedHits.Length,
                    "B.variantTargets: configure positions and expected hit mask");
                var positions = new Point2[manifest.variantTargets.Length];
                for (int i = 0; i < positions.Length; i++) positions[i] = new Point2(manifest.variantTargets[i].x, manifest.variantTargets[i].y);
                var geometry = Probe(variant, balance, manifest, positions);
                for (int i = 0; i < positions.Length; i++) Require((geometry.TargetDamage[i] > 0) == manifest.variantExpectedHits[i], "B.actualGeometry.target[" + i + "]");
                report.VariantDamage = geometry.TargetDamage;
                report.Checks.Add("B: configured variant actual hit geometry matches the data mask");

                Equal(module.UnlockGold, manifest.expectedModuleUnlockGold, "C.module.unlockGold");
                Require(profile.Unlock(manifest.newModuleId, out reason, balance), "C.unlock: " + reason);
                var modular = Authored("acceptance.module", "Acceptance module", manifest.baselineChassisId, manifest.newWeaponId,
                    new[] { new ModuleSelection("acceptance-slot", manifest.newModuleId) }, content);
                Equal(TowerCatalog.Cost(modular, balance, MaterialTier.Pencil), manifest.expectedModuleAssemblyCost, "C.assembly.price");
                Equal(TowerCatalog.WeaponRange(modular, balance, manifest.newWeaponId), manifest.expectedModuleRange, "C.range");
                Equal(TowerCatalog.ActionInterval(modular, balance, manifest.newWeaponId, TowerCatalog.FeedMultiplier(modular, balance)),
                    manifest.expectedModuleInterval, "C.interval");
                Require(profile.SaveBlueprint(modular, out reason, balance), "C.save: " + reason);
                Require(store.Save(profile).Success, "C.profile.write");
                var persisted = new WorkshopProfileStore(path).Load().Profile;
                bool foundModule = false;
                foreach (var page in persisted.Album) if (page.Id == modular.Id)
                    foundModule = page.ModuleSelections.Count == 1 && page.ModuleSelections[0].InstanceId == "acceptance-slot" && page.ModuleSelections[0].DefinitionId == manifest.newModuleId;
                Require(foundModule, "C.profile.moduleInstance");
                Require(new TowerPreview(modular, balance, MaterialTier.Pencil).Valid, "C.preview");
                Require(Probe(modular, balance, manifest, new[] { new Point2(manifest.probeTargetOffset.x, manifest.probeTargetOffset.y) }).Damage > 0, "C.actualAttack");
                report.Checks.Add("C: generic module modifiers, instance identity and restart without new bits");

                Require(profile.Unlock(manifest.newChassisId, out reason, balance), "D.unlock: " + reason);
                var based = Authored("acceptance.chassis", "Acceptance chassis", manifest.newChassisId, manifest.newWeaponId, null, content, manifest.newWeaponId);
                Require(chassis.MountCount == 2, "D.chassis.mountCount"); Equal(chassis.Capacity, manifest.expectedChassisCapacity, "D.chassis.capacity");
                Equal(chassis.BaseCost, manifest.expectedChassisBaseCost, "D.chassis.baseCost");
                Equal(TowerCatalog.Cost(based, balance, MaterialTier.Pencil), manifest.expectedChassisAssemblyCost, "D.assembly.price");
                Equal(TowerCatalog.Health(based, balance, MaterialTier.Pencil), manifest.expectedChassisHealth, "D.assembly.health");
                Require(profile.SaveBlueprint(based, out reason, balance), "D.save: " + reason);
                Require(store.Save(profile).Success, "D.profile.write");
                bool foundChassis = false;
                foreach (var page in new WorkshopProfileStore(path).Load().Profile.Album)
                    if (page.Id == based.Id) foundChassis = page.ChassisId == manifest.newChassisId && page.FirstWeaponId == manifest.newWeaponId &&
                        page.SecondWeaponId == manifest.newWeaponId && page.Drawing.GeometryHash == based.Drawing.GeometryHash;
                Require(foundChassis, "D.profile.restart");
                var previewChassis = new TowerPreview(based, balance, MaterialTier.Pencil); Require(previewChassis.Valid, "D.preview");
                Equal(previewChassis.Health, manifest.expectedChassisHealth, "D.preview.health");
                var dual = Probe(based, balance, manifest, new[] { new Point2(manifest.probeTargetOffset.x, manifest.probeTargetOffset.y) });
                Require(dual.PrimaryActions > 0 && dual.SecondaryActions > 0 && dual.Launches >= 2 && dual.TargetDamage[0] > single.TargetDamage[0], "D.actualTwoMountHits");
                report.ChassisPrimaryActions = dual.PrimaryActions; report.ChassisSecondaryActions = dual.SecondaryActions;
                report.Checks.Add("D: new two-mount chassis saves both weapons and both mounts perform production attacks");

                var existing = content.GetWeapon(manifest.existingWeaponId); var effect = content.GetEffect(manifest.changedEffectId);
                Equal(existing.Stats.Cost, manifest.expectedExistingWeaponCost, "E.existing.cost");
                Equal(existing.Stats.Damage, manifest.expectedExistingWeaponDamage, "E.existing.damage");
                Equal(effect.Duration, manifest.expectedEffectDuration, "E.effect.duration");
                var changed = Authored("acceptance.changed", "Acceptance changed balance", manifest.baselineChassisId, manifest.existingWeaponId, null, content);
                var changedPreview = new TowerPreview(changed, balance, MaterialTier.Pencil);
                Require(changedPreview.Valid, "E.preview: " + changedPreview.Reason);
                var pencil = balance.Material(MaterialTier.Pencil);
                long expectedExistingAssembly = (long)content.GetChassis(manifest.baselineChassisId).BaseCost + Math.Max(0, manifest.expectedExistingWeaponCost - existing.AssemblyCredit);
                float expectedExistingPrice = (float)Math.Ceiling(expectedExistingAssembly * (double)Math.Max(0, pencil.CostMultiplier));
                Equal(changedPreview.Cost, expectedExistingPrice, "E.preview.price");
                Equal(changedPreview.DrawingQuality, 100, "E.preview.quality");
                float expectedExistingHit = manifest.expectedExistingWeaponDamage * pencil.DamageMultiplier;
                Require(manifest.probeTargetHealth >= expectedExistingHit, "E.fixture: probe health must permit an unclipped existing-weapon hit");
                var existingHit = Probe(changed, balance, manifest, new[] { new Point2(manifest.probeTargetOffset.x, manifest.probeTargetOffset.y) });
                Equal(existingHit.Launches, 1, "E.actualAttack.launches");
                Equal(existingHit.TargetDamage[0], expectedExistingHit, "E.actualAttack.targetDamage");
                Equal(existingHit.Damage, expectedExistingHit, "E.actualAttack.attributedDamage");
                var existingTrial = TrialRunner.Run(changed, balance, manifest.trialScenario, manifest.trialBudget, manifest.seed);
                Require(existingTrial.Copies > 0 && existingTrial.Damage > 0, "E.trial: existing weapon produced no real damage");
                Equal(existingTrial.Cost, existingTrial.Copies * changedPreview.Cost, "E.trial.payment");
                report.EExpectedCost = (int)expectedExistingPrice; report.EPreviewCost = changedPreview.Cost;
                report.EExpectedHitDamage = expectedExistingHit; report.EProbeDamage = existingHit.TargetDamage[0];
                report.ETrialCopies = existingTrial.Copies; report.ETrialCost = existingTrial.Cost; report.ETrialDamage = existingTrial.Damage;
                var effectResult = ContentCombatProbe.Run(changed, balance, new Cell(manifest.probeCellX, manifest.probeCellY),
                    new[] { new Point2(manifest.probeTargetOffset.x, manifest.probeTargetOffset.y) }, manifest.probeTargetHealth,
                    manifest.probeStepSeconds, manifest.probeMaximumTicks, color: manifest.effectColor);
                float remaining = manifest.effectColor == InkColor.Blue ? effectResult.BlueRemaining[0] : manifest.effectColor == InkColor.Red ? effectResult.RedRemaining[0] : effectResult.GreenRemaining[0];
                Equal(remaining, manifest.expectedEffectDuration, "E.actualEffect.duration");
                Equal(balance.Waves[0].InkGrant, manifest.expectedWaveInkGrant, "E.wave.configuredGrant");
                Require(balance.PassiveInkPerSecond == 0 && balance.EarlyLaunchInkPerSecond == 0, "E.fixture: configure zero passive and early income for exact grant accounting");
                foreach (var enemy in balance.EnemyDefinitions) Require(enemy.Reward == 0, "E.fixture: configure zero enemy ink reward for exact grant accounting");
                var battle = Battle(restored, balance, manifest, manifest.expectedAssemblyCost, "A");
                report.BattleLaunches = battle.Launches; report.BattleDamage = battle.Damage;
                var existingBattle = Battle(changed, balance, manifest, changedPreview.Cost, "E");
                report.EPaidCost = existingBattle.Payment; report.EBattleLaunches = existingBattle.Launches;
                report.EBattleDamage = existingBattle.Damage; report.EActualWaveGrant = existingBattle.WaveGrant;
                report.Checks.Add("E: changed existing weapon agrees in preview price, exact isolated hit, trial payment/damage and real-wave construction/attacks; changed effect duration and exact completed-wave grant verified");
                report.Success = true;
            }
            catch (Exception error) { report.Error = error.GetType().Name + ": " + error.Message; }
            finally
            {
                if (directory != null)
                {
                    string absolute = Path.GetFullPath(directory);
                    string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(absolute).StartsWith("PaperContentSmoke-", StringComparison.Ordinal))
                    { try { Directory.Delete(absolute, true); report.TemporaryProfileRemoved = true; } catch (IOException) { } }
                }
            }
            return report;
        }
        static TowerBlueprint Authored(string id, string name, string chassisId, string weaponId, IEnumerable<ModuleSelection> modules, ContentCatalogSnapshot content, string secondWeaponId = null)
        {
            var draft = TowerBlueprint.NewDraft(id, name, chassisId, weaponId, secondWeaponId, modules: modules, content: content);
            var parts = new List<DrawingPart>();
            foreach (var template in DrawingTemplateCatalog.Create(draft)) parts.Add(new DrawingPart(template.BindingId, template.Contours));
            return draft.WithDrawing(new DrawingSnapshot(parts));
        }
        static ContentCombatProbeResult Probe(TowerBlueprint blueprint, GameBalance balance, ContentAcceptanceManifestAsset manifest, IReadOnlyList<Point2> targets)
            => ContentCombatProbe.Run(blueprint, balance, new Cell(manifest.probeCellX, manifest.probeCellY), targets,
                manifest.probeTargetHealth, manifest.probeStepSeconds, manifest.probeMaximumTicks);
        sealed class BattleResult
        {internal int Payment,Launches,WaveGrant;internal float Damage;}
        static BattleResult Battle(TowerBlueprint blueprint,GameBalance balance,ContentAcceptanceManifestAsset manifest,int expectedPrice,string check)
        {
            var battle=new GameSession(balance,manifest.seed,false);
            var cells=new[]{new Cell(battle.BaseCell.X+manifest.battleCellOffset.x,battle.BaseCell.Y+manifest.battleCellOffset.y)};
            var quote=battle.QuoteBlueprint(cells,blueprint,MaterialTier.Pencil,InkColor.None);
            Require(quote.Valid,check+".battle.quote: "+quote.Reason);Equal(quote.Cost,expectedPrice,check+".battle.quotePrice");
            int inkBefore=battle.Ink;
            Require(battle.TryBuildBlueprint(cells,blueprint,MaterialTier.Pencil,InkColor.None,out var reason),check+".battle.build: "+reason);
            int payment=inkBefore-battle.Ink;Equal(payment,expectedPrice,check+".battle.payment");
            int afterBuild=battle.Ink;battle.StartNextWave();
            for(int i=0;i<manifest.battleMaximumTicks&&battle.Phase==RunPhase.Assault;i++)battle.Tick(manifest.probeStepSeconds);
            Require(battle.Phase==RunPhase.Intermission||battle.Phase==RunPhase.Victory,check+".battle: first wave did not complete");
            int launches=battle.Statistics.Launches(blueprint.FirstWeaponId);float damage=battle.Statistics.Damage(blueprint.FirstWeaponId);
            Require(launches>0&&damage>0,check+".battle: no production hit attributed to "+blueprint.FirstWeaponId);
            int grant=battle.Ink-afterBuild;Equal(grant,manifest.expectedWaveInkGrant,check+".actualWave.grant");
            return new BattleResult{Payment=payment,Launches=launches,Damage=damage,WaveGrant=grant};
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Equal(float actual, float expected, string field)
        { if (float.IsNaN(expected) || float.IsInfinity(expected) || Math.Abs(actual - expected) > ComparisonTolerance) throw new InvalidOperationException(field + ": expected " + expected + ", actual " + actual); }
    }
    [Serializable] public sealed class ContentAcceptanceReport
    {
        public bool Success, TemporaryProfileRemoved;
        public string Manifest, Utc, UnityVersion, IsolatedProfile, Error;
        public int BattleLaunches, ChassisPrimaryActions, ChassisSecondaryActions;
        public float BattleDamage;
        public int EExpectedCost,EPreviewCost,EPaidCost,ETrialCopies,ETrialCost,EBattleLaunches,EActualWaveGrant;
        public float EExpectedHitDamage,EProbeDamage,ETrialDamage,EBattleDamage;
        public float[] VariantDamage;
        public List<string> Checks = new List<string>();
    }
}
