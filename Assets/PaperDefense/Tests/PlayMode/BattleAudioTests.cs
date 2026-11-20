#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    internal static class BattleAudioReflection
    {
        internal static Type Type(string name)
        {
            var type = typeof(PlayerGameRoot).Assembly.GetType("PaperDefense.Presentation." + name);
            Assert.That(type, Is.Not.Null, "Missing bounded battle-audio API: " + name);
            return type;
        }
        internal static object Call(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(item => item.Name == name && item.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, target.GetType().Name + "." + name);
            return method.Invoke(target, arguments);
        }
        internal static T Value<T>(object target, string name)
        {
            var property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, target.GetType().Name + "." + name);
            return (T)property.GetValue(target);
        }
        internal static object Cue(string name) => Enum.Parse(Type("BattleAudioCue"), name);
        internal static object Policy() => Activator.CreateInstance(Type("BattleAudioPolicy"));
        internal static bool Queue(object policy, string cue, double now = 10, float pan = 0)
            => (bool)Call(policy, "Queue", Cue(cue), now, pan);
        internal static object Take(object policy, double now = 10)
        {
            var method = policy.GetType().GetMethod("TryTake");
            Assert.That(method, Is.Not.Null);
            object[] arguments = { now, null };
            return (bool)method.Invoke(policy, arguments) ? arguments[1] : null;
        }
    }

    public sealed class BattleAudioPolicyTests
    {
        [Test]
        public void EveryCueHasAnOriginalShortMonoClipWithAudibleSamplesAndHeadroom()
        {
            foreach (string cue in Enum.GetNames(BattleAudioReflection.Type("BattleAudioCue")))
            {
                var clip = Resources.Load<AudioClip>("PaperDefenseAudio/" + cue);
                Assert.That(clip, Is.Not.Null, cue);
                Assert.That(clip.channels, Is.EqualTo(1), cue);
                Assert.That(clip.length, Is.InRange(.01f, 2f), cue);
                Assert.That(clip.samples, Is.GreaterThan(0), cue);
                var samples = new float[clip.samples];
                Assert.That(clip.GetData(samples, 0), Is.True, cue);
                float peak = samples.Max(value => Mathf.Abs(value));
                Assert.That(peak, Is.InRange(.001f, .49f), cue + " must be audible and leave mix headroom.");
            }
        }

        [TestCase(AttackKind.Single, "weapon.gun", "ShotSingle")]
        [TestCase(AttackKind.Line, "weapon.piercer", "ShotLine")]
        [TestCase(AttackKind.AreaProjectile, "core.mortar", "ShotMortar")]
        [TestCase(AttackKind.Cone, "core.fan", "ShotCone")]
        [TestCase(AttackKind.Ricochet, "core.ricochet", "ShotRicochet")]
        [TestCase(AttackKind.Radial, "weapon.spikes", "ShotRadial")]
        [TestCase(AttackKind.Single, "core.stapler", "ShotStapler")]
        public void OneActionHasOneSoundAndUsesCapturedWeaponFamily(AttackKind attack, string weapon, string expected)
        {
            var policyType = BattleAudioReflection.Type("BattleAudioPolicy");
            var classify = policyType.GetMethod("TryClassify", BindingFlags.Static | BindingFlags.Public);
            Assert.That(classify, Is.Not.Null);
            var events = new SimulationEventBuffer();
            foreach (var kind in new[] { SimulationEventKind.WeaponActionStarted, SimulationEventKind.Shot, SimulationEventKind.ProjectileLaunched })
                events.Publish(1, kind, new Point2(1, 2), new Point2(3, 4), (StructureKind)(-1), weaponId: weapon, attack: attack);
            foreach (var item in events)
            {
                object[] args = { item, null };
                bool accepted = (bool)classify.Invoke(null, args);
                Assert.That(accepted, Is.EqualTo(item.Kind == SimulationEventKind.WeaponActionStarted));
                if (accepted) Assert.That(args[1].ToString(), Is.EqualTo(expected));
            }
        }

        [Test]
        public void BurstIsBoundedCoalescedAndImportantCuesWinBeforeShots()
        {
            var policy = BattleAudioReflection.Policy();
            for (int i = 0; i < 2000; i++) BattleAudioReflection.Queue(policy, "ShotSingle");
            Assert.That(BattleAudioReflection.Value<int>(policy, "PendingCount"), Is.EqualTo(1));
            foreach (string cue in Enum.GetNames(BattleAudioReflection.Type("BattleAudioCue"))) BattleAudioReflection.Queue(policy, cue);
            Assert.That(BattleAudioReflection.Value<int>(policy, "PendingCount"), Is.LessThanOrEqualTo(16));
            BattleAudioReflection.Call(policy, "BeginFrame");
            var first = BattleAudioReflection.Take(policy);
            Assert.That(BattleAudioReflection.Value<object>(first, "Cue").ToString(), Is.EqualTo("Defeat"));
            int starts = 1;
            while (BattleAudioReflection.Take(policy) != null) starts++;
            Assert.That(starts, Is.EqualTo(3), "One catch-up render frame cannot start sixteen clips.");
        }

        [Test]
        public void CooldownUsesWallTimeEvenAcrossManySimulationFrames()
        {
            var policy = BattleAudioReflection.Policy();
            Assert.That(BattleAudioReflection.Queue(policy, "ShotSingle", 10), Is.True);
            Assert.That(BattleAudioReflection.Take(policy, 10), Is.Not.Null);
            for (int i = 0; i < 12; i++)
            {
                BattleAudioReflection.Call(policy, "BeginFrame");
                Assert.That(BattleAudioReflection.Queue(policy, "ShotSingle", 10.01), Is.False);
            }
            Assert.That(BattleAudioReflection.Queue(policy, "ShotSingle", 10.2), Is.True);
            Assert.That(BattleAudioReflection.Take(policy, 10.2), Is.Not.Null);
        }

        [Test]
        public void SilenceDropsPendingRequestsAndNewRunResetsCooldowns()
        {
            var policy = BattleAudioReflection.Policy();
            BattleAudioReflection.Queue(policy, "BaseHit"); BattleAudioReflection.Take(policy);
            BattleAudioReflection.Queue(policy, "ShotLine"); BattleAudioReflection.Call(policy, "Clear");
            Assert.That(BattleAudioReflection.Take(policy), Is.Null);
            Assert.That(BattleAudioReflection.Queue(policy, "BaseHit", 10.01), Is.False);
            BattleAudioReflection.Call(policy, "Reset");
            Assert.That(BattleAudioReflection.Queue(policy, "BaseHit", 10.01), Is.True);
        }

        [Test]
        public void OldInvalidAndOutOfRangeRequestsCannotProduceDelayedOrInvalidAudio()
        {
            var policy = BattleAudioReflection.Policy();
            Assert.That(BattleAudioReflection.Queue(policy, "ShotSingle", double.NaN), Is.False);
            Assert.That(BattleAudioReflection.Queue(policy, "ShotSingle", 10, float.NaN), Is.False);
            Assert.That(BattleAudioReflection.Queue(policy, "ShotSingle", 10, 99), Is.True);
            var request = BattleAudioReflection.Take(policy);
            Assert.That(BattleAudioReflection.Value<float>(request, "Pan"), Is.InRange(-.65f, .65f));
            BattleAudioReflection.Queue(policy, "ShotLine", 10);
            Assert.That(BattleAudioReflection.Take(policy, 11), Is.Null, "Old requests expire instead of catching up after a stall.");
        }
    }

    public sealed class BattleAudioTests : BattleHudThemeFixture
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameSession Session => (GameSession)typeof(RunController).GetField("session", Private).GetValue(game.Run);
        Component Audio()
        {
            var type = BattleAudioReflection.Type("PlayerBattleAudio");
            var component = root.GetComponentsInChildren<Component>(true).FirstOrDefault(type.IsInstanceOfType) ?? root.AddComponent(type);
            BattleAudioReflection.Call(component, "Initialize", game);
            BattleAudioReflection.Call(component, "OnApplicationFocus", true);
            var settings = BattleAudioReflection.Value<object>(game, "AudioSettings");
            BattleAudioReflection.Call(settings, "SetMuted", false);
            BattleAudioReflection.Call(settings, "SetVolume", .75f);
            return component;
        }
        static void Flush(Component audio) => BattleAudioReflection.Call(audio, "LateUpdate");
        void Publish(SimulationEventKind kind, AttackKind attack = AttackKind.Single)
        {
            Session.EventBuffer.Publish(Session.TickIndex, kind, new Point2(5, 5), new Point2(6, 5), StructureKind.Gun,
                weaponId: "core.gun", actionId: 1, attack: attack, simulationSeconds: Session.Elapsed);
            game.Run.StepSimulation();
        }

        [Test]
        public void RuntimeOwnsSixTwoDimensionalVoicesAndDoesNotMultiplyOnReinitialize()
        {
            var audio = Audio(); BattleAudioReflection.Call(audio, "Initialize", game);
            Assert.That(BattleAudioReflection.Value<int>(audio, "BoundedVoiceCount"), Is.EqualTo(6));
            var sources = audio.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources.Length, Is.EqualTo(6));
            foreach (var source in sources)
            {
                Assert.That(source.spatialBlend, Is.Zero);
                Assert.That(source.playOnAwake, Is.False);
                Assert.That(source.loop, Is.False);
            }
            Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(item => item.isActiveAndEnabled), Is.EqualTo(1));
        }

        [Test]
        public void ControllerEventsPlayOnceAndLateBindingDoesNotReplayHistory()
        {
            var audio = Audio(); StartBattle();
            Publish(SimulationEventKind.WeaponActionStarted); Publish(SimulationEventKind.Shot); Publish(SimulationEventKind.ProjectileLaunched);
            Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.EqualTo(1));
            Assert.That(BattleAudioReflection.Value<object>(audio, "LastCue").ToString(), Is.EqualTo("ShotSingle"));
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                source.clip == Resources.Load<AudioClip>("PaperDefenseAudio/ShotSingle") && source.volume > 0), Is.True);
            Flush(audio); BattleAudioReflection.Call(audio, "Initialize", game); Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.EqualTo(1));
        }

        [Test]
        public void PauseMenuFocusAndMuteDiscardPendingAudioWithoutCatchUp()
        {
            var audio = Audio(); StartBattle();
            Publish(SimulationEventKind.WeaponActionStarted); game.Run.TogglePause(); Flush(audio);
            game.Run.TogglePause(); Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.Zero);
            Publish(SimulationEventKind.BaseHit); game.Pause(); Flush(audio); game.Resume(); Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.Zero);
            Publish(SimulationEventKind.StructureDestroyed); BattleAudioReflection.Call(audio, "OnApplicationFocus", false);
            Flush(audio); BattleAudioReflection.Call(audio, "OnApplicationFocus", true); Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.Zero);
            Publish(SimulationEventKind.WeaponActionStarted);
            var settings = BattleAudioReflection.Value<object>(game, "AudioSettings");
            BattleAudioReflection.Call(settings, "SetMuted", true); Flush(audio);
            BattleAudioReflection.Call(settings, "SetMuted", false); Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.Zero);
            Assert.That(BattleAudioReflection.Value<int>(audio, "QueuedCueCount"), Is.Zero);
            Assert.That(BattleAudioReflection.Value<int>(audio, "ActiveVoiceCount"), Is.Zero);
        }

        [Test]
        public void WaveTransitionSoundsOnceAndARealLethalContactProducesOneResultCue()
        {
            var audio = Audio(); StartBattle(); game.Run.NextWave(); Flush(audio);
            Assert.That(BattleAudioReflection.Value<object>(audio, "LastCue").ToString(), Is.EqualTo("WaveStarted"));
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                source.clip == Resources.Load<AudioClip>("PaperDefenseAudio/WaveStarted")), Is.True);
            int before = BattleAudioReflection.Value<int>(audio, "PlayedCueCount"); Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.EqualTo(before));
            var rules = GameBalance.CreateDefault(); rules.BaseHealth = 7; rules.Enemy(EnemyKind.Doodle).Damage = 20;
            var prepared = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(new Point2(rules.Width / 2 + .5f, rules.Height / 2 + .5f), 1000) });
            typeof(RunController).GetMethod("Initialize", Private, null, new[] { typeof(GameSession) }, null).Invoke(game.Run, new object[] { prepared });
            game.Navigate(PlayerPage.Battle); game.Run.StepSimulation();
            Assert.That(prepared.Phase, Is.EqualTo(RunPhase.Defeat));
            game.Navigate(PlayerPage.Result); Flush(audio);
            Assert.That(BattleAudioReflection.Value<object>(audio, "LastCue").ToString(), Is.EqualTo("Defeat"));
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(source =>
                source.clip == Resources.Load<AudioClip>("PaperDefenseAudio/Defeat")), Is.True);
            int ended = BattleAudioReflection.Value<int>(audio, "PlayedCueCount");
            game.Navigate(PlayerPage.Result); Flush(audio); Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.EqualTo(ended));
        }

        [Test]
        public void NewSessionAndDisableClearVoicesAndSubscriptions()
        {
            var audio = Audio(); StartBattle(); Publish(SimulationEventKind.WeaponActionStarted);
            game.ReturnToPreparation(); Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "QueuedCueCount"), Is.Zero);
            Assert.That(BattleAudioReflection.Value<int>(audio, "ActiveVoiceCount"), Is.Zero);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.Zero);
            StartBattle(); ((Behaviour)audio).enabled = false; Publish(SimulationEventKind.BaseHit);
            ((Behaviour)audio).enabled = true; Flush(audio);
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.Zero);
        }

        [Test]
        public void ImportantCueUsesReservedVoiceAndThenPreemptsAShotWithoutExpandingThePool()
        {
            var audio = Audio(); StartBattle();
            double now = Time.realtimeSinceStartupAsDouble;
            void Play(string cue)
            {
                var request = Activator.CreateInstance(BattleAudioReflection.Type("BattleAudioRequest"),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new[] { BattleAudioReflection.Cue(cue), (object).2f }, null);
                BattleAudioReflection.Call(audio, "Play", request, now);
            }
            for (int i = 0; i < 6; i++) Play("ShotSingle");
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.EqualTo(5), "Ordinary shots cannot occupy the reserved warning voice.");
            Play("BaseHit"); Play("StructureDestroyed");
            Assert.That(BattleAudioReflection.Value<int>(audio, "PlayedCueCount"), Is.EqualTo(7));
            var sources = audio.GetComponentsInChildren<AudioSource>();
            Assert.That(sources.Length, Is.EqualTo(6));
            Assert.That(sources.Count(source => source.clip == Resources.Load<AudioClip>("PaperDefenseAudio/BaseHit")), Is.EqualTo(1));
            Assert.That(sources.Count(source => source.clip == Resources.Load<AudioClip>("PaperDefenseAudio/StructureDestroyed")), Is.EqualTo(1));
            var settings = BattleAudioReflection.Value<object>(game, "AudioSettings");
            BattleAudioReflection.Call(settings, "SetMuted", true);
            Assert.That(BattleAudioReflection.Value<int>(audio, "ActiveVoiceCount"), Is.Zero);
            Assert.That(sources.All(source => !source.isPlaying), Is.True);
        }
    }
}
#endif
