using NUnit.Framework;
using PaperDefense.Core;
namespace PaperDefense.Tests
{
    public sealed class WorkshopAssemblyStageTests
    {
        [Test] public void PartStageAllowsBlankDrawingButFinalValidationRequiresGeometry()
        {
            var balance=GameBalance.CreateDefault();var bp=new TowerBlueprint("stage","Stage",ChassisKind.Standard,StructureKind.Gun).AsFreeform();
            Assert.That(TowerCatalog.ValidateAssembly(bp,balance,out var reason),Is.True,reason);
            Assert.That(TowerCatalog.Validate(bp,balance,out _),Is.False);
        }
        [Test] public void PartStageStillRejectsTooManyMounts()
        {
            var balance=GameBalance.CreateDefault();var bp=new TowerBlueprint("stage","Stage","chassis.standard",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(1,"weapon.gun"),new WeaponMountSelection(2,"weapon.gun")}).AsFreeform();
            Assert.That(TowerCatalog.ValidateAssembly(bp,balance,out _),Is.False);
        }
    }
}
