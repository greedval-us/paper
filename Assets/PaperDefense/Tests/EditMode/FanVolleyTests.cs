using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class FanVolleyTests
    {
        [Test] public void FixedBudgetSplitsEvenlyAndRemainderHasStableOrder()
        {
            var volley=new FanVolley(new[]{FanGuide.FromAngle(30),FanGuide.FromAngle(-30),FanGuide.FromAngle(0)});
            Assert.That(volley.Guides.Select(g=>g.AngleDegrees),Is.EqualTo(new[]{-30f,0,30f}).Within(.0001));
            Assert.That(volley.Allocate(6),Is.EqualTo(new[]{2,2,2}));Assert.That(volley.Allocate(8),Is.EqualTo(new[]{3,3,2}));
            Assert.That(new FanVolley(new[]{FanGuide.FromAngle(0)}).Allocate(6),Is.EqualTo(new[]{6}));
            Assert.Throws<ArgumentException>(()=>volley.Allocate(2));
        }
        [Test] public void CurvedGuideOwnsItsPointsButOnlyEndpointDeterminesStraightDirection()
        {
            var points=new[]{new Point2(0,0),new Point2(1,1),new Point2(0,2)};var guide=new FanGuide(points);
            points[2]=new Point2(2,2);Assert.That(guide.AngleDegrees,Is.EqualTo(0));Assert.That(guide.Points[2].X,Is.Zero);
            var volley=new FanVolley(new[]{guide});var right=volley.Direction(0,default,new Point2(1,0));
            Assert.That(right.X,Is.EqualTo(1).Within(.00001));Assert.That(right.Y,Is.Zero.Within(.00001));
            var turned=new FanVolley(new[]{FanGuide.FromAngle(30)}).Direction(0,default,new Point2(0,1));
            Assert.That(turned.X,Is.EqualTo(.5).Within(.00001));Assert.That(turned.Y,Is.EqualTo(Math.Sqrt(.75)).Within(.00001));
        }
        [Test] public void InvalidOrRepeatedDirectionsCannotAddPellets()
        {
            Assert.Throws<ArgumentException>(()=>new FanVolley(Array.Empty<FanGuide>()));
            Assert.Throws<ArgumentException>(()=>new FanVolley(new[]{FanGuide.FromAngle(0),FanGuide.FromAngle(1)}));
            Assert.Throws<ArgumentException>(()=>new FanGuide(new[]{new Point2(0,0),new Point2(float.NaN,1)}));
            Assert.Throws<ArgumentException>(()=>new FanGuide(new[]{new Point2(1,0),new Point2(1,2)}));
            Assert.Throws<ArgumentException>(()=>FanGuide.FromAngle(90));
        }
        [Test] public void WeaponConfigurationIsCopiedAndLimitsBothAnglesAndPelletCount()
        {
            var input=LegacyContentCatalog.CreateInput(GameBalance.CreateDefault());var fan=input.Weapons.Single(w=>w.Id=="weapon.fan");
            fan.VolleyPellets=5;fan.VolleyHitRadius=.22f;fan.Stats.ConeAngleDegrees=50;
            var copy=input.CreateCopy();fan.VolleyPellets=2;fan.VolleyHitRadius=.8f;
            var snapshot=new ContentCatalogSnapshot(copy,true);var weapon=snapshot.GetWeapon("weapon.fan");
            Assert.That(weapon.VolleyPellets,Is.EqualTo(5));Assert.That(weapon.VolleyHitRadius,Is.EqualTo(.22f));
            var valid=new FanVolley(new[]{FanGuide.FromAngle(-20),FanGuide.FromAngle(0),FanGuide.FromAngle(20)});
            Assert.DoesNotThrow(()=>valid.Validate(weapon));Assert.That(valid.Allocate(weapon.VolleyPellets),Is.EqualTo(new[]{2,2,1}));
            Assert.Throws<ArgumentException>(()=>new FanVolley(new[]{FanGuide.FromAngle(30)}).Validate(weapon));
            Assert.Throws<ArgumentException>(()=>valid.Validate(new ContentCatalogSnapshot(input,true).GetWeapon("weapon.fan")));
            copy.Weapons.Single(w=>w.Id=="weapon.fan").VolleyPellets=0;Assert.Throws<ArgumentException>(()=>new ContentCatalogSnapshot(copy,true));
        }
    }
}
