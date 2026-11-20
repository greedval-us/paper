using System;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class MissionCheckpointProfileTests
    {
        static Type CheckpointType
        {
            get
            {
                var type = typeof(WorkshopProfile).Assembly.GetType("PaperDefense.Core.MissionCheckpoint");
                Assert.That(type, Is.Not.Null, "Missing immutable mission checkpoint envelope."); return type;
            }
        }
        static object Create(Guid id, string mission, int revision, int waves, string payload)
            => Activator.CreateInstance(CheckpointType, id, mission, revision, waves, payload);
        static object Checkpoint(WorkshopProfile profile)
        {
            var property = typeof(WorkshopProfile).GetProperty("MissionCheckpoint"); Assert.That(property, Is.Not.Null); return property.GetValue(profile);
        }
        static void Set(WorkshopProfile profile, object checkpoint)
        {
            var method = typeof(WorkshopProfile).GetMethod("SetMissionCheckpoint"); Assert.That(method, Is.Not.Null); method.Invoke(profile, new[] { checkpoint });
        }
        static T Value<T>(object value, string property) => (T)value.GetType().GetProperty(property).GetValue(value);
        static WorkshopProfile RoundTrip(WorkshopProfile profile)
            => WorkshopProfile.FromData(JsonUtility.FromJson<WorkshopProfileData>(JsonUtility.ToJson(profile.ToData())));

        [Test] public void OpaquePayloadRoundTripsWithProfileV11AndWorkingCopiesDoNotShareMutableState()
        {
            var id = Guid.NewGuid(); const string payload = "{ malformed battle payload: \\\"оставить как есть\\\" }";
            var saved = Create(id, "notebook-1-page-1", 1, 1, payload);
            var profile = new WorkshopProfile(); profile.GrantGold(19); Set(profile, saved);
            var copy = profile.CreateWorkingCopy(); Set(copy, null);
            Assert.That(Checkpoint(profile), Is.Not.Null); Assert.That(Checkpoint(copy), Is.Null);
            var restored = RoundTrip(profile);
            Assert.That(restored.ToData().Version, Is.EqualTo(11)); Assert.That(restored.Gold, Is.EqualTo(19));
            Assert.That(Value<Guid>(Checkpoint(restored), "RunId"), Is.EqualTo(id));
            Assert.That(Value<string>(Checkpoint(restored), "PayloadJson"), Is.EqualTo(payload), "The permanent profile never parses or normalizes battle payload JSON.");
        }

        [Test] public void MissingCheckpointInVersionTenMigratesWithoutInventingAnAttempt()
        {
            _ = CheckpointType;
            var source = new WorkshopProfile(); var data = source.ToData(); data.Version = 10;
            var field = data.GetType().GetField("MissionCheckpoint"); if (field != null) field.SetValue(data, null);
            var loaded = WorkshopProfile.FromData(data);
            Assert.That(Checkpoint(loaded), Is.Null); Assert.That(loaded.Loadout, Is.EqualTo(source.Loadout));
            Assert.That(loaded.ToData().Version, Is.EqualTo(11));
        }

        [Test] public void AbsentAndConsumedCheckpointsStayAbsentAfterActualJsonRoundTrips()
        {
            _ = CheckpointType;
            var profile = new WorkshopProfile(); Assert.That(Checkpoint(RoundTrip(profile)), Is.Null);
            Set(profile, Create(Guid.NewGuid(), "notebook-1-page-1", 1, 1, "{}"));
            Set(profile, null); Assert.That(Checkpoint(RoundTrip(profile)), Is.Null);
        }

        [TestCase("id")] [TestCase("mission")] [TestCase("revision")] [TestCase("zero-wave")] [TestCase("last-wave")] [TestCase("empty-payload")]
        public void InvalidCheckpointMetadataIsRejected(string invalid)
        {
            _ = CheckpointType;
            Guid id = invalid == "id" ? Guid.Empty : Guid.NewGuid();
            string mission = invalid == "mission" ? "unknown.mission" : "notebook-1-page-1";
            int revision = invalid == "revision" ? 0 : 1;
            int waves = invalid == "zero-wave" ? 0 : invalid == "last-wave" ? 4 : 1;
            string payload = invalid == "empty-payload" ? "" : "{}";
            var error = Assert.Throws<TargetInvocationException>(() => Create(id, mission, revision, waves, payload));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test] public void PayloadLimitCountsUtf8BytesRatherThanCharacters()
        {
            _ = CheckpointType;
            string payload = new string('я', 2 * 1024 * 1024 + 1);
            var error = Assert.Throws<TargetInvocationException>(() => Create(Guid.NewGuid(), "notebook-1-page-1", 1, 1, payload));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test] public void FutureEnvelopeMetadataProtectsTheSourceBeforeMalformedPayloadIsConsidered()
        {
            var profile = new WorkshopProfile(); Set(profile, Create(Guid.NewGuid(), "notebook-1-page-1", 1, 1, "not JSON"));
            var data = profile.ToData(); var field = data.GetType().GetField("MissionCheckpoint"); Assert.That(field, Is.Not.Null);
            object envelope = field.GetValue(data); envelope.GetType().GetField("Version").SetValue(envelope, 999);
            Assert.Throws<NotSupportedException>(() => WorkshopProfile.FromData(data));
        }
    }
}
