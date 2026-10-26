using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    internal static class FreeformUiTestDrawing
    {
        public static void Complete(DrawingEditSession session, float bodyHalfSize = .4f)
        {
            var design = session.Blueprint.WithFreeformDrawing(session.Snapshot).FreeformDesign;
            if (design.BodyArea < .06f)
                Assert.That(session.AddStroke(DrawingBindings.Base, new[] {
                    new DrawingPoint(-bodyHalfSize, -bodyHalfSize), new DrawingPoint(bodyHalfSize, -bodyHalfSize),
                    new DrawingPoint(bodyHalfSize, bodyHalfSize), new DrawingPoint(-bodyHalfSize, bodyHalfSize), new DrawingPoint(-bodyHalfSize, -bodyHalfSize) }, out _), Is.True);
            foreach (var mount in session.Blueprint.WeaponMounts)
            {
                if (design.GetBarrel(mount.SlotIndex)?.IsComplete == true) continue;
                Assert.That(session.AddStroke(DrawingBindings.Mount(mount.SlotIndex), new[] {
                    new DrawingPoint(0, 0), new DrawingPoint(0, .6f) }, out _), Is.True);
            }
            var completed=session.Blueprint.WithFreeformDrawing(session.Snapshot).FreeformDesign;
            Assert.That(completed.IsUsable,Is.True,"Shared UI fixture must remain complete and within the real construction ink budget: "+completed.InkUsed+"/"+completed.InkBudget);
        }
    }

}
