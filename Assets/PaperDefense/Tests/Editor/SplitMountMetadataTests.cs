using System;
using NUnit.Framework;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class SplitMountMetadataTests
    {
        // Deliberately small author data: the barrel mouth resolves to (15.5,112.5)
        // relative to the root attach, independently of the production transform.
        const string Fixture = @"{
            ""schemaVersion"":2,""revision"":""fixture"",
            ""foundations"":[{""id"":""base"",""sockets"":[
                {""slot"":0,""order"":0,""position"":{""x"":0.5,""y"":0.8},""diameter"":0.3}]}],
            ""weapons"":[{""id"":""gun"",""canvas"":{""x"":100,""y"":200},
                ""trim"":{""x"":0,""y"":0,""width"":100,""height"":200},
                ""attach"":{""x"":50,""y"":10},""muzzle"":{""x"":60,""y"":180},""diameter"":70}],
            ""pilots"":[{""chassisId"":""base"",""weaponId"":""gun"",""revision"":""split-fixture"",
                ""pivotOffset"":{""x"":0,""y"":46},
                ""muzzleFragment"":""barrels"",""muzzlePoint"":{""x"":70,""y"":150},
                ""fragments"":[
                    {""id"":""support"",""spriteId"":""support-sprite"",""canvas"":{""x"":100,""y"":200},
                        ""trim"":{""x"":0,""y"":0,""width"":100,""height"":200},
                        ""attach"":{""x"":20,""y"":20},""offset"":{""x"":0,""y"":0},
                        ""scale"":0.25,""radians"":0,""group"":0,""layer"":0},
                    {""id"":""cradle"",""spriteId"":""cradle-sprite"",""canvas"":{""x"":100,""y"":200},
                        ""trim"":{""x"":0,""y"":0,""width"":100,""height"":200},
                        ""attach"":{""x"":20,""y"":20},""offset"":{""x"":0,""y"":46},
                        ""scale"":0.25,""radians"":0,""group"":1,""layer"":1},
                    {""id"":""barrels"",""spriteId"":""barrels-sprite"",""canvas"":{""x"":100,""y"":200},
                        ""trim"":{""x"":0,""y"":0,""width"":100,""height"":200},
                        ""attach"":{""x"":20,""y"":20},""offset"":{""x"":3,""y"":80},
                        ""scale"":0.25,""radians"":0,""group"":2,""layer"":2}
                ]}]
        }";

        [Test]
        public void RealSplitMountCatalogueAndMinimalSchemaTwoRemainValid()
        {
            var asset = Resources.Load<TextAsset>("Art/NotebookMounts");
            Assert.That(asset, Is.Not.Null);
            Assert.DoesNotThrow(() => NotebookMountLayout.Validate(asset.text));
            Assert.DoesNotThrow(() => NotebookMountLayout.Validate(Fixture));
        }

        [TestCase("1e100")]
        [TestCase("-1e100")]
        public void NonFinitePivotIsRejected(string component)
        {
            string json = Fixture.Replace("\"pivotOffset\":{\"x\":0,\"y\":46}",
                "\"pivotOffset\":{\"x\":" + component + ",\"y\":46}");
            Assert.Throws<FormatException>(() => NotebookMountLayout.Validate(json));
        }

        [Test]
        public void MuzzleAtRotationPivotIsRejectedInsteadOfCollapsingAimAndRecoil()
        {
            string json = Fixture.Replace("\"pivotOffset\":{\"x\":0,\"y\":46}",
                "\"pivotOffset\":{\"x\":15.5,\"y\":112.5}");
            Assert.Throws<FormatException>(() => NotebookMountLayout.Validate(json));
        }

        [TestCase("\"schemaVersion\":2", "\"schemaVersion\":1")]
        [TestCase("\"chassisId\":\"base\"", "\"chassisId\":\"unknown\"")]
        [TestCase("\"weaponId\":\"gun\"", "\"weaponId\":\"unknown\"")]
        [TestCase("\"spriteId\":\"support-sprite\"", "\"spriteId\":\"\"")]
        [TestCase("\"group\":0,\"layer\":0", "\"group\":3,\"layer\":0")]
        [TestCase("\"group\":1,\"layer\":1", "\"group\":0,\"layer\":1")]
        [TestCase("\"group\":1,\"layer\":1", "\"group\":1,\"layer\":0")]
        [TestCase("\"muzzleFragment\":\"barrels\"", "\"muzzleFragment\":\"support\"")]
        [TestCase("\"muzzlePoint\":{\"x\":70,\"y\":150}", "\"muzzlePoint\":{\"x\":101,\"y\":150}")]
        public void BrokenSplitDefinitionIsRejected(string before, string after)
        {
            Assert.That(Fixture, Does.Contain(before), "The mutation must change its intended author field.");
            Assert.Throws<FormatException>(() => NotebookMountLayout.Validate(Fixture.Replace(before, after)));
        }
    }
}
